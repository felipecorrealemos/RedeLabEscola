#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_SCRIPT="$SCRIPT_DIR/deploy-school.sh"
TEST_COUNT=0

fail() {
  printf 'FALHOU: %s\n' "$*" >&2
  exit 1
}

assert_eq() {
  local expected="$1"
  local actual="$2"
  local label="$3"
  [[ "$actual" == "$expected" ]] || \
    fail "$label: esperado '$expected', recebido '$actual'"
}

assert_contains() {
  local haystack="$1"
  local needle="$2"
  local label="$3"
  [[ "$haystack" == *"$needle"* ]] || fail "$label: texto não encontrado: $needle"
}

pass() {
  TEST_COUNT=$((TEST_COUNT + 1))
  printf 'OK %d - %s\n' "$TEST_COUNT" "$1"
}

remove_temp_root() {
  local temp_root="$1"
  [[ "$temp_root" == /tmp/* || "$temp_root" == /var/tmp/* ]] || \
    fail "diretório temporário inesperado: $temp_root"
  cd "$SCRIPT_DIR"
  rm -rf -- "$temp_root"
}

# Importa somente as funções. main e os traps operacionais não são ativados.
source "$DEPLOY_SCRIPT"

test_documentation_only() {
  CHANGED_FILES=(
    "README.md"
    "DEPLOY_CHECKLIST.md"
    "redelab-server/README.md"
    "Build_WebGL/FAVICON.md"
  )
  classify_changed_files
  assert_eq false "$BACKEND_CHANGED" "backend em alteração documental"
  assert_eq false "$MIGRATIONS_CHANGED" "migration em alteração documental"
  assert_eq false "$WEBGL_CHANGED" "WebGL em alteração documental"
  assert_eq false "$WEBGL_RUNTIME_CHANGED" "runtime WebGL em alteração documental"
  BACKEND_STAGING_DIR=""
  prepare_backend_if_needed >/dev/null
  assert_eq "" "$BACKEND_STAGING_DIR" "staging para documentação"
  pass "documentação isolada não reinicia serviços"
}

test_backend_and_migration() {
  CHANGED_FILES=(
    "redelab-server/src/server.js"
    "redelab-server/database/migrations/20260901_001.sql"
  )
  classify_changed_files
  assert_eq true "$BACKEND_CHANGED" "backend"
  assert_eq true "$MIGRATIONS_CHANGED" "migration"
  assert_eq false "$WEBGL_CHANGED" "Build WebGL"
  assert_eq false "$WEBGL_RUNTIME_CHANGED" "runtime WebGL"
  pass "backend e migration são classificados"
}

test_webgl_classification() {
  CHANGED_FILES=("Build_WebGL/index.html" "Build_WebGL/Build/game.wasm.gz")
  classify_changed_files
  assert_eq false "$BACKEND_CHANGED" "backend para build"
  assert_eq true "$WEBGL_CHANGED" "Build WebGL"
  assert_eq false "$WEBGL_RUNTIME_CHANGED" "runtime WebGL para build"
  pass "Build WebGL é classificada separadamente"
}

test_webgl_runtime_classification() {
  CHANGED_FILES=("redelab-server/scripts/unity-webgl-auth-server.js")
  classify_changed_files
  assert_eq true "$BACKEND_CHANGED" "backend do servidor WebGL"
  assert_eq true "$WEBGL_RUNTIME_CHANGED" "runtime do servidor WebGL"
  assert_eq false "$WEBGL_CHANGED" "build para servidor WebGL"
  pass "script do servidor WebGL força restart seletivo"
}

test_env_reader_and_webgl_validation() {
  local temp_root env_file valid_build
  temp_root="$(mktemp -d)"
  env_file="$temp_root/.env"
  valid_build="$temp_root/Build_WebGL"
  mkdir -p "$valid_build/Build" "$valid_build/TemplateData"
  printf '%s\n' \
    '# comentário' \
    'PORT=3001' \
    'HTTPS_ENABLED="true"' \
    "DB_USER='servidor'" > "$env_file"
  printf 'html\n' > "$valid_build/index.html"
  printf 'dados\n' > "$valid_build/Build/game.data.gz"

  assert_eq 3001 "$(read_env_value "$env_file" PORT)" "porta do .env"
  assert_eq true "$(read_env_value "$env_file" HTTPS_ENABLED)" "aspas duplas do .env"
  assert_eq servidor "$(read_env_value "$env_file" DB_USER)" "aspas simples do .env"
  validate_webgl_build "$valid_build"
  if validate_webgl_build "$temp_root/inexistente" >/dev/null 2>&1; then
    fail "validação aceitou uma Build WebGL inexistente"
  fi

  remove_temp_root "$temp_root"
  pass "leitura segura do .env e validação estrutural da WebGL"
}

create_fake_backend_fixture() {
  local temp_root="$1"
  local fake_bin="$temp_root/fake-bin"
  REPO_DIR="$temp_root/repository"
  BACKEND_DIR="$temp_root/backend"
  mkdir -p "$REPO_DIR/redelab-server" "$BACKEND_DIR" "$fake_bin"
  printf '{"name":"staging-test"}\n' > "$REPO_DIR/redelab-server/package.json"
  printf '{"lockfileVersion":3}\n' > "$REPO_DIR/redelab-server/package-lock.json"
  printf 'novo\n' > "$REPO_DIR/redelab-server/app.js"
  printf 'antigo\n' > "$BACKEND_DIR/app.js"
  printf 'SEGREDO=nao-alterar\n' > "$BACKEND_DIR/.env"

  cat > "$fake_bin/rsync" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
args=("$@")
source_dir="${args[${#args[@]}-2]}"
target_dir="${args[${#args[@]}-1]}"
cp -a "${source_dir%/}/." "${target_dir%/}/"
rm -f -- "${target_dir%/}/.env"
rm -rf -- "${target_dir%/}/node_modules"
EOF

  cat > "$fake_bin/npm" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
printf '%s\n' "$*" >> "$FAKE_NPM_TRACE"
if [[ "${FAKE_NPM_FAIL_ON:-}" == "$1" ]]; then
  exit 42
fi
if [[ "$1" == ci ]]; then
  mkdir -p node_modules
fi
EOF
  chmod +x "$fake_bin/rsync" "$fake_bin/npm"

  RSYNC_BIN="$fake_bin/rsync"
  NPM_BIN="$fake_bin/npm"
  PRESERVE_OWNERSHIP=false
  BACKEND_CHANGED=true
  MIGRATIONS_CHANGED=false
  BACKEND_STAGING_DIR=""
  BACKEND_PREVIOUS_DIR=""
  BACKEND_FAILED_DIR=""
  BACKEND_PROMOTED=false
  MIGRATIONS_EXECUTED=false
  export FAKE_NPM_TRACE="$temp_root/npm-trace.log"
  export FAKE_NPM_FAIL_ON=""
}

test_backend_staging_failure_is_non_destructive() {
  local temp_root
  temp_root="$(mktemp -d)"
  create_fake_backend_fixture "$temp_root"
  export FAKE_NPM_FAIL_ON=test

  if prepare_backend_staging >/dev/null; then
    fail "staging foi aprovado apesar da falha simulada em npm test"
  fi
  assert_eq antigo "$(tr -d '\r\n' < "$BACKEND_DIR/app.js")" "backend após falha"
  assert_eq 'SEGREDO=nao-alterar' "$(tr -d '\r\n' < "$BACKEND_DIR/.env")" ".env após falha"
  assert_eq "" "$BACKEND_STAGING_DIR" "referência do staging após falha"
  if compgen -G "${BACKEND_DIR}.deploy.*" >/dev/null; then
    fail "staging permaneceu no disco após falha"
  fi

  remove_temp_root "$temp_root"
  pass "falha em npm test preserva backend e remove staging"
}

test_backend_staging_success_and_promotion() {
  local temp_root original_env previous_dir
  temp_root="$(mktemp -d)"
  create_fake_backend_fixture "$temp_root"
  original_env="$temp_root/original.env"
  cp -p -- "$BACKEND_DIR/.env" "$original_env"

  prepare_backend_staging >/dev/null
  assert_eq antigo "$(tr -d '\r\n' < "$BACKEND_DIR/app.js")" "backend antes da promoção"
  assert_eq novo "$(tr -d '\r\n' < "$BACKEND_STAGING_DIR/app.js")" "código no staging"
  cmp -s -- "$original_env" "$BACKEND_STAGING_DIR/.env" || fail ".env divergiu no staging"

  promote_backend >/dev/null
  previous_dir="$BACKEND_PREVIOUS_DIR"
  assert_eq novo "$(tr -d '\r\n' < "$BACKEND_DIR/app.js")" "backend promovido"
  assert_eq antigo "$(tr -d '\r\n' < "$previous_dir/app.js")" "backend anterior"
  cmp -s -- "$original_env" "$BACKEND_DIR/.env" || fail ".env divergiu após promoção"
  assert_eq true "$BACKEND_PROMOTED" "estado da promoção"
  assert_eq "" "$BACKEND_STAGING_DIR" "staging após promoção"

  remove_temp_root "$temp_root"
  pass "staging aprovado é promovido e preserva backend anterior e .env"
}

test_api_failure_rolls_back_only_without_migration() {
  local temp_root
  temp_root="$(mktemp -d)"
  create_fake_backend_fixture "$temp_root"
  SYSTEMCTL_BIN=true
  prepare_backend_staging >/dev/null
  promote_backend >/dev/null

  if abort_after_api_failure "falha simulada" >/dev/null; then
    fail "falha da API não encerrou o deploy com erro"
  fi
  assert_eq antigo "$(tr -d '\r\n' < "$BACKEND_DIR/app.js")" "backend restaurado"
  [[ -n "$BACKEND_FAILED_DIR" && -f "$BACKEND_FAILED_DIR/app.js" ]] || \
    fail "backend novo com falha não foi preservado"
  assert_eq novo "$(tr -d '\r\n' < "$BACKEND_FAILED_DIR/app.js")" "backend com falha"

  remove_temp_root "$temp_root"
  pass "falha da API restaura código anterior quando não houve migration"
}

test_api_failure_does_not_roll_back_after_migration() {
  local temp_root previous_dir
  temp_root="$(mktemp -d)"
  create_fake_backend_fixture "$temp_root"
  prepare_backend_staging >/dev/null
  promote_backend >/dev/null
  previous_dir="$BACKEND_PREVIOUS_DIR"
  MIGRATIONS_EXECUTED=true

  if abort_after_api_failure "falha simulada após migration" >/dev/null; then
    fail "falha pós-migration não encerrou o deploy com erro"
  fi
  assert_eq novo "$(tr -d '\r\n' < "$BACKEND_DIR/app.js")" "backend novo pós-migration"
  assert_eq antigo "$(tr -d '\r\n' < "$previous_dir/app.js")" "backend anterior preservado"
  assert_eq "" "$BACKEND_FAILED_DIR" "rollback indevido pós-migration"

  remove_temp_root "$temp_root"
  pass "falha pós-migration preserva versões sem rollback automático"
}

test_migration_backup_order() {
  local temp_root trace order
  temp_root="$(mktemp -d)"
  trace="$temp_root/order.log"
  MIGRATIONS_CHANGED=true
  MIGRATIONS_EXECUTED=false

  create_database_backup() {
    printf 'backup\n' >> "$trace"
    BACKUP_FILE="$temp_root/backup.sql.gz"
    printf 'dump\n' > "$BACKUP_FILE"
  }
  run_migrations_from_staging() {
    printf 'migration\n' >> "$trace"
    MIGRATIONS_EXECUTED=true
  }

  apply_migrations_with_backup
  order="$(paste -sd, "$trace")"
  assert_eq 'backup,migration' "$order" "ordem de backup e migration"
  assert_eq true "$MIGRATIONS_EXECUTED" "estado da migration"

  remove_temp_root "$temp_root"
  pass "migration só executa depois do backup aprovado"
}

test_check_mode_in_temporary_git_repositories() {
  local temp_root origin seed server developer before after output
  temp_root="$(mktemp -d)"
  origin="$temp_root/origin.git"
  seed="$temp_root/seed"
  server="$temp_root/server"
  developer="$temp_root/developer"

  cleanup_test_repository() {
    remove_temp_root "$temp_root"
  }
  trap cleanup_test_repository RETURN

  git init --bare --initial-branch=main "$origin" >/dev/null
  git clone --quiet "$origin" "$seed"
  git -C "$seed" config user.name "Teste Deploy"
  git -C "$seed" config user.email "deploy@example.invalid"
  mkdir -p "$seed/redelab-server/src" "$seed/Build_WebGL/Build" "$seed/Build_WebGL/TemplateData"
  printf 'baseline\n' > "$seed/README.md"
  printf 'baseline\n' > "$seed/redelab-server/src/server.js"
  printf 'baseline\n' > "$seed/Build_WebGL/index.html"
  printf 'baseline\n' > "$seed/Build_WebGL/Build/game.data.gz"
  printf 'baseline\n' > "$seed/Build_WebGL/TemplateData/style.css"
  git -C "$seed" add .
  git -C "$seed" commit --quiet -m baseline
  git -C "$seed" push --quiet origin main

  git clone --quiet "$origin" "$server"
  git clone --quiet "$origin" "$developer"
  git -C "$developer" config user.name "Teste Deploy"
  git -C "$developer" config user.email "deploy@example.invalid"
  mkdir -p "$developer/redelab-server/database/migrations"
  printf 'alterado\n' > "$developer/redelab-server/src/server.js"
  printf 'CREATE TABLE exemplo (id int);\n' > \
    "$developer/redelab-server/database/migrations/20260901_001.sql"
  printf 'nova build\n' > "$developer/Build_WebGL/index.html"
  git -C "$developer" add .
  git -C "$developer" commit --quiet -m update
  git -C "$developer" push --quiet origin main

  before="$(git -C "$server" rev-parse HEAD)"
  output="$(
    REPO_DIR="$server" \
    BACKEND_DIR="$temp_root/backend" \
    WEBGL_DIR="$temp_root/webgl" \
    BACKUP_DIR="$temp_root/backups" \
    LOG_FILE="$temp_root/deploy.log" \
    bash "$DEPLOY_SCRIPT" --check
  )"
  after="$(git -C "$server" rev-parse HEAD)"

  assert_eq "$before" "$after" "HEAD no modo --check"
  assert_contains "$output" "Backend seria atualizado: sim" "plano de backend"
  assert_contains "$output" "Migrations seriam executadas: sim" "plano de migration"
  assert_contains "$output" "Build WebGL seria atualizada: sim" "plano de WebGL"
  assert_contains "$output" "Modo --check concluído" "conclusão do check"
  [[ ! -e "$temp_root/backend" ]] || fail "--check criou BACKEND_DIR"
  [[ ! -e "$temp_root/webgl" ]] || fail "--check criou WEBGL_DIR"
  [[ ! -e "$temp_root/backups" ]] || fail "--check criou BACKUP_DIR"
  [[ ! -e "$temp_root/deploy.log" ]] || fail "--check criou LOG_FILE"

  printf 'mudança local\n' > "$server/dirty.txt"
  if REPO_DIR="$server" \
     BACKEND_DIR="$temp_root/backend" \
     WEBGL_DIR="$temp_root/webgl" \
     BACKUP_DIR="$temp_root/backups" \
     LOG_FILE="$temp_root/deploy.log" \
     bash "$DEPLOY_SCRIPT" --check >/dev/null 2>&1; then
    fail "--check aceitou working tree suja"
  fi

  trap - RETURN
  cleanup_test_repository
  pass "--check analisa remote sem alterar HEAD ou destinos operacionais"
}

test_documentation_only
test_backend_and_migration
test_webgl_classification
test_webgl_runtime_classification
test_env_reader_and_webgl_validation
test_backend_staging_failure_is_non_destructive
test_backend_staging_success_and_promotion
test_api_failure_rolls_back_only_without_migration
test_api_failure_does_not_roll_back_after_migration
test_migration_backup_order
test_check_mode_in_temporary_git_repositories

printf '\n%d testes de deploy aprovados.\n' "$TEST_COUNT"
