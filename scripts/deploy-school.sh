#!/usr/bin/env bash

set -Eeuo pipefail

# Todos os valores podem ser sobrescritos pelo ambiente do processo.
REPO_DIR="${REPO_DIR:-/var/www/RedeLabEscola}"
BACKEND_DIR="${BACKEND_DIR:-/var/www/redelab-server}"
WEBGL_DIR="${WEBGL_DIR:-/var/www/redelab-webgl}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/redelab}"
LOG_FILE="${LOG_FILE:-/var/log/redelab-deploy.log}"

API_SERVICE="${API_SERVICE:-redelab}"
WEBGL_SERVICE="${WEBGL_SERVICE:-redelab-webgl}"
DB_NAME="${DB_NAME:-redelab_escola}"

REMOTE_NAME="${REMOTE_NAME:-origin}"
EXPECTED_BRANCH="${EXPECTED_BRANCH:-main}"
API_HEALTH_URL="${API_HEALTH_URL:-}"
WEBGL_HEALTH_URL="${WEBGL_HEALTH_URL:-}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-15}"
HEALTH_TLS_INSECURE="${HEALTH_TLS_INSECURE:-true}"
ALLOW_NON_ROOT="${ALLOW_NON_ROOT:-false}"
PRESERVE_OWNERSHIP="${PRESERVE_OWNERSHIP:-true}"
NPM_BIN="${NPM_BIN:-npm}"
RSYNC_BIN="${RSYNC_BIN:-rsync}"
MYSQLDUMP_BIN="${MYSQLDUMP_BIN:-mysqldump}"
GZIP_BIN="${GZIP_BIN:-gzip}"
SYSTEMCTL_BIN="${SYSTEMCTL_BIN:-systemctl}"
CURL_BIN="${CURL_BIN:-curl}"

CHECK_MODE=false
CURRENT_STEP="Inicialização"
STEP_NUMBER=0
TOTAL_STEPS=9
LOG_ACTIVE=false

LOCAL_COMMIT=""
REMOTE_COMMIT=""
DEPLOYED_COMMIT=""
CURRENT_BRANCH=""
HAS_UPDATES=false
BACKEND_CHANGED=false
MIGRATIONS_CHANGED=false
WEBGL_CHANGED=false
WEBGL_RUNTIME_CHANGED=false
API_RESTARTED=false
WEBGL_RESTARTED=false
API_HEALTH_STATUS="não executado"
WEBGL_HEALTH_STATUS="não executado"
BACKUP_FILE=""
BACKUP_PART_FILE=""
BACKEND_STAGING_DIR=""
BACKEND_PREVIOUS_DIR=""
BACKEND_FAILED_DIR=""
BACKEND_PROMOTED=false
MIGRATIONS_EXECUTED=false
WEBGL_ROLLBACK_DIR=""

MYSQL_CONFIG_TMP=""
WEBGL_STAGING_DIR=""
declare -a CHANGED_FILES=()

timestamp() {
  date '+%Y%m%d_%H%M%S'
}

log() {
  printf '%s\n' "$*"
}

step() {
  STEP_NUMBER=$((STEP_NUMBER + 1))
  CURRENT_STEP="$1"
  printf '\n[%d/%d] %s\n' "$STEP_NUMBER" "$TOTAL_STEPS" "$CURRENT_STEP"
}

die() {
  log "ERRO: $*"
  return 1
}

cleanup() {
  if [[ -n "$MYSQL_CONFIG_TMP" && -f "$MYSQL_CONFIG_TMP" ]]; then
    rm -f -- "$MYSQL_CONFIG_TMP"
  fi
  if [[ -n "$BACKEND_STAGING_DIR" && -d "$BACKEND_STAGING_DIR" ]]; then
    rm -rf -- "$BACKEND_STAGING_DIR"
  fi
  if [[ -n "$BACKUP_PART_FILE" && -f "$BACKUP_PART_FILE" ]]; then
    rm -f -- "$BACKUP_PART_FILE"
  fi
  if [[ -n "$WEBGL_STAGING_DIR" && -d "$WEBGL_STAGING_DIR" ]]; then
    rm -rf -- "$WEBGL_STAGING_DIR"
  fi
}

handle_error() {
  local line="$1"
  local exit_code="$2"
  trap - ERR
  printf '\n========================================\n' >&2
  printf 'Deploy RedeLab interrompido\n' >&2
  printf 'Etapa: %s\n' "$CURRENT_STEP" >&2
  printf 'Linha: %s\n' "$line" >&2
  printf 'Código de saída: %s\n' "$exit_code" >&2
  if [[ -n "$BACKUP_FILE" ]]; then
    printf 'Backup disponível: %s\n' "$BACKUP_FILE" >&2
  fi
  if [[ -n "$BACKEND_PREVIOUS_DIR" ]]; then
    printf 'Backend anterior: %s\n' "$BACKEND_PREVIOUS_DIR" >&2
  fi
  if [[ -n "$BACKEND_FAILED_DIR" ]]; then
    printf 'Backend que falhou: %s\n' "$BACKEND_FAILED_DIR" >&2
  fi
  if [[ "$LOG_ACTIVE" == true ]]; then
    printf 'Consulte o log: %s\n' "$LOG_FILE" >&2
  fi
  printf '========================================\n' >&2
  exit "$exit_code"
}

usage() {
  cat <<'EOF'
Uso:
  sudo ./scripts/deploy-school.sh [--check]

Opções:
  --check   Busca referências remotas e apresenta o plano sem alterar a
            working tree, backend, banco, Build WebGL ou serviços.
  -h, --help
            Exibe esta ajuda.
EOF
}

parse_arguments() {
  if (($# > 1)); then
    usage >&2
    die "Use no máximo uma opção."
  fi

  case "${1:-}" in
    "") ;;
    --check) CHECK_MODE=true ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage >&2
      die "Opção desconhecida: $1"
      ;;
  esac
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "Comando obrigatório não encontrado: $1"
}

validate_path() {
  local label="$1"
  local value="$2"
  [[ "$value" == /* ]] || die "$label deve ser um caminho absoluto: $value"
  [[ "$value" != "/" ]] || die "$label não pode apontar para a raiz do sistema."
}

initialize_log() {
  local log_parent
  log_parent="$(dirname -- "$LOG_FILE")"
  install -d -m 0750 -- "$log_parent"
  touch -- "$LOG_FILE"
  exec > >(tee -a "$LOG_FILE") 2>&1
  LOG_ACTIVE=true
  log ""
  log "===== Deploy iniciado em $(date --iso-8601=seconds) ====="
}

validate_environment() {
  validate_path REPO_DIR "$REPO_DIR"
  validate_path BACKEND_DIR "$BACKEND_DIR"
  validate_path WEBGL_DIR "$WEBGL_DIR"
  validate_path BACKUP_DIR "$BACKUP_DIR"
  validate_path LOG_FILE "$LOG_FILE"

  [[ "$REPO_DIR" != "$BACKEND_DIR" ]] || die "REPO_DIR e BACKEND_DIR devem ser diferentes."
  [[ "$REPO_DIR" != "$WEBGL_DIR" ]] || die "REPO_DIR e WEBGL_DIR devem ser diferentes."
  [[ "$BACKEND_DIR" != "$WEBGL_DIR" ]] || die "BACKEND_DIR e WEBGL_DIR devem ser diferentes."
  [[ -d "$REPO_DIR" ]] || die "Repositório não encontrado: $REPO_DIR"
  [[ -d "$REPO_DIR/.git" ]] || die "REPO_DIR não é um clone Git: $REPO_DIR"

  require_command git
  if [[ "$CHECK_MODE" == false ]]; then
    if [[ "$ALLOW_NON_ROOT" != true && "$EUID" -ne 0 ]]; then
      die "Execute o deploy real com sudo. Use --check para uma validação sem alterações."
    fi
    require_command "$NPM_BIN"
    require_command "$RSYNC_BIN"
    require_command "$MYSQLDUMP_BIN"
    require_command "$GZIP_BIN"
    require_command "$CURL_BIN"
    require_command "$SYSTEMCTL_BIN"
  require_command install
  require_command mktemp
  require_command cmp
  require_command find
  require_command grep
  require_command chown
  require_command chmod
    initialize_log
  fi
}

validate_git_and_fetch() {
  local status
  cd "$REPO_DIR"
  status="$(git status --porcelain --untracked-files=normal)"
  if [[ -n "$status" ]]; then
    log "O clone possui mudanças locais. Corrija-as antes do deploy:"
    log "$status"
    return 1
  fi

  CURRENT_BRANCH="$(git branch --show-current)"
  [[ "$CURRENT_BRANCH" == "$EXPECTED_BRANCH" ]] || \
    die "Branch atual '$CURRENT_BRANCH'; esperada '$EXPECTED_BRANCH'."

  git remote get-url "$REMOTE_NAME" >/dev/null 2>&1 || \
    die "Remote '$REMOTE_NAME' não está configurado."

  LOCAL_COMMIT="$(git rev-parse HEAD)"
  log "Buscando $REMOTE_NAME/$EXPECTED_BRANCH..."
  git fetch --quiet "$REMOTE_NAME" "$EXPECTED_BRANCH"
  REMOTE_COMMIT="$(git rev-parse "$REMOTE_NAME/$EXPECTED_BRANCH")"

  if [[ "$LOCAL_COMMIT" == "$REMOTE_COMMIT" ]]; then
    HAS_UPDATES=false
    CHANGED_FILES=()
    return
  fi

  git merge-base --is-ancestor "$LOCAL_COMMIT" "$REMOTE_COMMIT" || \
    die "HEAD não pode avançar por fast-forward até $REMOTE_NAME/$EXPECTED_BRANCH."

  HAS_UPDATES=true
  mapfile -t CHANGED_FILES < <(git diff --name-only "$LOCAL_COMMIT" "$REMOTE_COMMIT")
  ((${#CHANGED_FILES[@]} > 0)) || die "Commits diferentes, mas nenhum arquivo alterado foi identificado."
}

is_documentation_file() {
  local path="$1"
  local name="${path##*/}"
  case "$path" in
    Docs/*|docs/*|documentation/*|DEPLOY_CHECKLIST.md) return 0 ;;
  esac
  case "$name" in
    README|README.*|*.md|*.rst) return 0 ;;
  esac
  return 1
}

classify_changed_files() {
  local path
  BACKEND_CHANGED=false
  MIGRATIONS_CHANGED=false
  WEBGL_CHANGED=false
  WEBGL_RUNTIME_CHANGED=false

  for path in "${CHANGED_FILES[@]}"; do
    if is_documentation_file "$path"; then
      continue
    fi

    case "$path" in
      redelab-server/database/migrations/*)
        MIGRATIONS_CHANGED=true
        BACKEND_CHANGED=true
        ;;
      redelab-server/scripts/unity-webgl-auth-server.js|\
      redelab-server/src/config/https.js|\
      redelab-server/package.json|\
      redelab-server/package-lock.json)
        BACKEND_CHANGED=true
        WEBGL_RUNTIME_CHANGED=true
        ;;
      redelab-server/*)
        BACKEND_CHANGED=true
        ;;
      Build_WebGL/*)
        WEBGL_CHANGED=true
        ;;
    esac
  done
}

yes_no() {
  [[ "$1" == true ]] && printf 'sim' || printf 'não'
}

print_change_plan() {
  log "Branch atual:  $CURRENT_BRANCH"
  log "Commit local:  $LOCAL_COMMIT"
  log "Commit remoto: $REMOTE_COMMIT"

  if [[ "$HAS_UPDATES" == false ]]; then
    log "Atualizações disponíveis: não"
    return
  fi

  log "Atualizações disponíveis: sim"
  log "Arquivos que mudariam:"
  printf '  %s\n' "${CHANGED_FILES[@]}"
  log ""
  log "Backend seria atualizado: $(yes_no "$BACKEND_CHANGED")"
  log "Migrations seriam executadas: $(yes_no "$MIGRATIONS_CHANGED")"
  log "Build WebGL seria atualizada: $(yes_no "$WEBGL_CHANGED")"
  log "Serviço $API_SERVICE seria reiniciado: $(yes_no "$BACKEND_CHANGED")"
  if [[ "$WEBGL_CHANGED" == true || "$WEBGL_RUNTIME_CHANGED" == true ]]; then
    log "Serviço $WEBGL_SERVICE seria reiniciado: sim"
  else
    log "Serviço $WEBGL_SERVICE seria reiniciado: não"
  fi
}

pull_fast_forward() {
  cd "$REPO_DIR"
  git pull --ff-only "$REMOTE_NAME" "$EXPECTED_BRANCH"
  DEPLOYED_COMMIT="$(git rev-parse HEAD)"
  [[ "$DEPLOYED_COMMIT" == "$REMOTE_COMMIT" ]] || \
    die "O commit após o pull não corresponde ao commit remoto analisado."
  [[ -z "$(git status --porcelain --untracked-files=normal)" ]] || \
    die "A working tree ficou suja após o pull."
}

read_env_value() {
  local file="$1"
  local key="$2"
  local line value
  [[ -f "$file" ]] || return 1

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    line="${line#"${line%%[![:space:]]*}"}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    if [[ "$line" == "$key="* ]]; then
      value="${line#*=}"
      value="${value#"${value%%[![:space:]]*}"}"
      value="${value%"${value##*[![:space:]]}"}"
      if [[ ${#value} -ge 2 ]]; then
        if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]] || \
           [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
          value="${value:1:${#value}-2}"
        fi
      fi
      printf '%s' "$value"
      return 0
    fi
  done < "$file"
  return 1
}

create_database_backup() {
  local env_file="$BACKEND_DIR/.env"
  local db_host db_port db_user db_password
  [[ -f "$env_file" ]] || die "O .env real do backend não foi encontrado em $env_file."

  db_host="${DB_HOST:-$(read_env_value "$env_file" DB_HOST || true)}"
  db_port="${DB_PORT:-$(read_env_value "$env_file" DB_PORT || true)}"
  db_user="${DB_USER:-$(read_env_value "$env_file" DB_USER || true)}"
  db_password="${DB_PASSWORD:-$(read_env_value "$env_file" DB_PASSWORD || true)}"
  db_host="${db_host:-localhost}"
  db_port="${db_port:-3306}"
  [[ -n "$db_user" ]] || die "DB_USER não foi encontrado no ambiente nem no .env."

  install -d -m 0750 -- "$BACKUP_DIR"
  MYSQL_CONFIG_TMP="$(mktemp "$BACKUP_DIR/.mysqldump.XXXXXX.cnf")"
  chmod 0600 "$MYSQL_CONFIG_TMP"
  {
    printf '[client]\n'
    printf 'host=%s\n' "$db_host"
    printf 'port=%s\n' "$db_port"
    printf 'user=%s\n' "$db_user"
    if [[ -n "$db_password" ]]; then
      printf 'password=%s\n' "$db_password"
    fi
  } > "$MYSQL_CONFIG_TMP"

  BACKUP_FILE="$BACKUP_DIR/${DB_NAME}_$(timestamp).sql.gz"
  BACKUP_PART_FILE="${BACKUP_FILE}.part"
  rm -f -- "$BACKUP_PART_FILE"
  "$MYSQLDUMP_BIN" \
    --defaults-extra-file="$MYSQL_CONFIG_TMP" \
    --single-transaction \
    --quick \
    --routines \
    --triggers \
    --databases "$DB_NAME" | "$GZIP_BIN" -c > "$BACKUP_PART_FILE"
  [[ -s "$BACKUP_PART_FILE" ]] || die "O backup foi criado vazio: $BACKUP_PART_FILE"
  mv -- "$BACKUP_PART_FILE" "$BACKUP_FILE"
  BACKUP_PART_FILE=""
  [[ -s "$BACKUP_FILE" ]] || die "Não foi possível validar o backup: $BACKUP_FILE"
  log "Backup criado: $BACKUP_FILE"
}

discard_backend_staging() {
  if [[ -n "$BACKEND_STAGING_DIR" && -d "$BACKEND_STAGING_DIR" ]]; then
    rm -rf -- "$BACKEND_STAGING_DIR"
  fi
  BACKEND_STAGING_DIR=""
}

prepare_backend_staging() {
  local source_dir="$REPO_DIR/redelab-server"
  local env_file="$BACKEND_DIR/.env"
  local staging_env
  [[ -d "$source_dir" ]] || die "Backend não encontrado no repositório: $source_dir"
  [[ -f "$source_dir/package.json" && -f "$source_dir/package-lock.json" ]] || \
    die "package.json/package-lock.json ausentes no backend do repositório."
  [[ -d "$BACKEND_DIR" ]] || \
    die "BACKEND_DIR não existe. A primeira instalação não é feita por este script."
  [[ -f "$env_file" ]] || die "O .env real do backend não existe: $env_file"

  BACKEND_STAGING_DIR="$(mktemp -d "${BACKEND_DIR}.deploy.XXXXXX")"
  staging_env="$BACKEND_STAGING_DIR/.env"
  "$RSYNC_BIN" -a \
    --exclude='.env' \
    --exclude='node_modules/' \
    --exclude='.git/' \
    --exclude='coverage/' \
    --exclude='tmp/' \
    --exclude='temp/' \
    --exclude='*.log' \
    "$source_dir/" "$BACKEND_STAGING_DIR/"
  cp -p -- "$env_file" "$staging_env"

  if ! cmp -s -- "$env_file" "$staging_env"; then
    discard_backend_staging
    log "O .env copiado para o staging não corresponde ao arquivo real."
    return 1
  fi

  cd "$BACKEND_STAGING_DIR"
  if ! "$NPM_BIN" ci; then
    cd "$REPO_DIR"
    discard_backend_staging
    log "npm ci falhou no staging; o backend publicado permanece intacto."
    return 1
  fi
  if ! "$NPM_BIN" test; then
    cd "$REPO_DIR"
    discard_backend_staging
    log "npm test falhou no staging; o backend publicado permanece intacto."
    return 1
  fi
  if ! cmp -s -- "$env_file" "$staging_env"; then
    cd "$REPO_DIR"
    discard_backend_staging
    log "O .env do staging foi alterado durante a preparação; deploy abortado."
    return 1
  fi

  if [[ "$PRESERVE_OWNERSHIP" == true ]]; then
    chown -R --reference="$BACKEND_DIR" "$BACKEND_STAGING_DIR"
    chmod --reference="$BACKEND_DIR" "$BACKEND_STAGING_DIR"
    chown --reference="$env_file" "$staging_env"
    chmod --reference="$env_file" "$staging_env"
  fi
  log "Backend validado no staging: $BACKEND_STAGING_DIR"
}

prepare_backend_if_needed() {
  if [[ "$BACKEND_CHANGED" == true ]]; then
    prepare_backend_staging
  else
    log "Backend sem alterações executáveis; staging não criado."
  fi
}

run_migrations_from_staging() {
  [[ -n "$BACKEND_STAGING_DIR" && -d "$BACKEND_STAGING_DIR" ]] || \
    die "Staging validado não encontrado para executar migrations."
  cd "$BACKEND_STAGING_DIR"
  if ! "$NPM_BIN" run migrate; then
    log "Migration falhou; backend publicado não foi promovido."
    return 1
  fi
  MIGRATIONS_EXECUTED=true
}

apply_migrations_with_backup() {
  if [[ "$MIGRATIONS_CHANGED" == true ]]; then
    create_database_backup
    run_migrations_from_staging
  else
    log "Nenhuma migration alterada; backup de banco não necessário."
  fi
}

promote_backend() {
  local previous_suffix
  if [[ "$BACKEND_CHANGED" != true ]]; then
    log "Backend sem alterações para promover."
    return 0
  fi
  [[ -n "$BACKEND_STAGING_DIR" && -d "$BACKEND_STAGING_DIR" ]] || \
    die "Staging do backend não está pronto para promoção."
  [[ -d "$BACKEND_DIR" ]] || die "Backend publicado desapareceu antes da promoção."

  previous_suffix="$(timestamp)"
  BACKEND_PREVIOUS_DIR="${BACKEND_DIR}.previous.${previous_suffix}"
  [[ ! -e "$BACKEND_PREVIOUS_DIR" ]] || \
    die "Destino do backend anterior já existe: $BACKEND_PREVIOUS_DIR"

  mv -- "$BACKEND_DIR" "$BACKEND_PREVIOUS_DIR"
  if ! mv -- "$BACKEND_STAGING_DIR" "$BACKEND_DIR"; then
    if [[ -d "$BACKEND_PREVIOUS_DIR" && ! -e "$BACKEND_DIR" ]]; then
      mv -- "$BACKEND_PREVIOUS_DIR" "$BACKEND_DIR"
      BACKEND_PREVIOUS_DIR=""
    fi
    log "Falha ao promover o staging; backend anterior restaurado."
    return 1
  fi
  BACKEND_STAGING_DIR=""
  BACKEND_PROMOTED=true
  log "Backend promovido atomicamente para $BACKEND_DIR"
  log "Backend anterior preservado em $BACKEND_PREVIOUS_DIR"
}

restore_previous_backend_after_api_failure() {
  local failed_suffix
  if [[ "$BACKEND_PROMOTED" != true || "$MIGRATIONS_EXECUTED" == true ]]; then
    return 1
  fi
  [[ -d "$BACKEND_DIR" && -d "$BACKEND_PREVIOUS_DIR" ]] || return 1

  failed_suffix="$(timestamp)"
  BACKEND_FAILED_DIR="${BACKEND_DIR}.failed.${failed_suffix}"
  [[ ! -e "$BACKEND_FAILED_DIR" ]] || return 1
  mv -- "$BACKEND_DIR" "$BACKEND_FAILED_DIR" || return 1
  if ! mv -- "$BACKEND_PREVIOUS_DIR" "$BACKEND_DIR"; then
    mv -- "$BACKEND_FAILED_DIR" "$BACKEND_DIR" || true
    BACKEND_FAILED_DIR=""
    return 1
  fi
  BACKEND_PREVIOUS_DIR=""
  BACKEND_PROMOTED=false
  log "Backend anterior restaurado após falha da API."
  if "$SYSTEMCTL_BIN" restart "$API_SERVICE"; then
    log "Serviço $API_SERVICE reiniciado com o backend anterior."
  else
    log "ATENÇÃO: o restart do backend anterior também falhou."
  fi
  return 0
}

abort_after_api_failure() {
  local reason="$1"
  log "Falha após promover o backend: $reason"
  if [[ "$MIGRATIONS_EXECUTED" == true ]]; then
    log "Migrations foram executadas; nenhum rollback automático de código ou SQL será feito."
    log "Backend atual: $BACKEND_DIR"
    log "Backend anterior: ${BACKEND_PREVIOUS_DIR:-indisponível}"
    log "Backup do banco: ${BACKUP_FILE:-indisponível}"
  elif ! restore_previous_backend_after_api_failure; then
    log "Não foi possível restaurar automaticamente o backend anterior."
  fi
  return 1
}

validate_webgl_build() {
  local directory="$1"
  [[ -f "$directory/index.html" ]] || die "Build WebGL sem index.html: $directory"
  [[ -d "$directory/Build" ]] || die "Build WebGL sem diretório Build/: $directory"
  [[ -d "$directory/TemplateData" ]] || die "Build WebGL sem diretório TemplateData/: $directory"
  find "$directory/Build" -mindepth 1 -type f -print -quit | grep -q . || \
    die "O diretório Build/ está vazio: $directory"
}

update_webgl() {
  local source_dir="$REPO_DIR/Build_WebGL"
  local parent_dir rollback_suffix
  validate_webgl_build "$source_dir"

  parent_dir="$(dirname -- "$WEBGL_DIR")"
  [[ -d "$parent_dir" ]] || die "Diretório pai da publicação WebGL não existe: $parent_dir"
  [[ -d "$WEBGL_DIR" ]] || \
    die "WEBGL_DIR não existe. A primeira instalação não é feita por este script."
  WEBGL_STAGING_DIR="$(mktemp -d "${WEBGL_DIR}.new.XXXXXX")"
  "$RSYNC_BIN" -a --exclude='*.log' --exclude='tmp/' --exclude='temp/' \
    "$source_dir/" "$WEBGL_STAGING_DIR/"
  validate_webgl_build "$WEBGL_STAGING_DIR"
  if [[ "$PRESERVE_OWNERSHIP" == true ]]; then
    chown --reference="$WEBGL_DIR" "$WEBGL_STAGING_DIR"
    chmod --reference="$WEBGL_DIR" "$WEBGL_STAGING_DIR"
  fi

  if [[ -e "$WEBGL_DIR" ]]; then
    rollback_suffix="$(timestamp)"
    WEBGL_ROLLBACK_DIR="${WEBGL_DIR}.previous.${rollback_suffix}"
    [[ ! -e "$WEBGL_ROLLBACK_DIR" ]] || die "Destino de rollback já existe: $WEBGL_ROLLBACK_DIR"
    mv -- "$WEBGL_DIR" "$WEBGL_ROLLBACK_DIR"
  fi

  if ! mv -- "$WEBGL_STAGING_DIR" "$WEBGL_DIR"; then
    if [[ -n "$WEBGL_ROLLBACK_DIR" && -d "$WEBGL_ROLLBACK_DIR" && ! -e "$WEBGL_DIR" ]]; then
      mv -- "$WEBGL_ROLLBACK_DIR" "$WEBGL_DIR"
      WEBGL_ROLLBACK_DIR=""
    fi
    die "Não foi possível promover a nova Build WebGL."
  fi
  WEBGL_STAGING_DIR=""
  log "Build WebGL publicada de forma atômica em $WEBGL_DIR"
  if [[ -n "$WEBGL_ROLLBACK_DIR" ]]; then
    log "Build anterior preservada para rollback manual: $WEBGL_ROLLBACK_DIR"
  fi
}

restart_services() {
  if [[ "$BACKEND_CHANGED" == true || "$MIGRATIONS_CHANGED" == true ]]; then
    if ! "$SYSTEMCTL_BIN" restart "$API_SERVICE"; then
      abort_after_api_failure "não foi possível reiniciar o serviço $API_SERVICE"
    fi
    API_RESTARTED=true
  fi

  if [[ "$WEBGL_CHANGED" == true || "$WEBGL_RUNTIME_CHANGED" == true ]]; then
    "$SYSTEMCTL_BIN" restart "$WEBGL_SERVICE"
    WEBGL_RESTARTED=true
  fi
}

derive_health_urls() {
  local env_file="$BACKEND_DIR/.env"
  local api_port api_https webgl_port webgl_https api_protocol webgl_protocol
  if [[ -z "$API_HEALTH_URL" ]]; then
    api_port="$(read_env_value "$env_file" PORT || true)"
    api_https="$(read_env_value "$env_file" HTTPS_ENABLED || true)"
    api_port="${api_port:-3000}"
    api_protocol=http
    [[ "${api_https,,}" == true ]] && api_protocol=https
    API_HEALTH_URL="${api_protocol}://localhost:${api_port}/api/health"
  fi

  if [[ -z "$WEBGL_HEALTH_URL" ]]; then
    webgl_port="$(read_env_value "$env_file" UNITY_WEBGL_PORT || true)"
    webgl_https="$(read_env_value "$env_file" UNITY_WEBGL_HTTPS_ENABLED || true)"
    webgl_port="${webgl_port:-8081}"
    webgl_protocol=http
    [[ "${webgl_https,,}" == true ]] && webgl_protocol=https
    WEBGL_HEALTH_URL="${webgl_protocol}://localhost:${webgl_port}/healthz"
  fi
}

health_request() {
  local url="$1"
  local -a options=(--silent --show-error --fail --max-time "$HEALTH_TIMEOUT")
  if [[ "$HEALTH_TLS_INSECURE" == true && "$url" == https://* ]]; then
    options+=(-k)
  fi
  "$CURL_BIN" "${options[@]}" "$url" >/dev/null
}

validate_restarted_services() {
  derive_health_urls
  if [[ "$API_RESTARTED" == true ]]; then
    if ! "$SYSTEMCTL_BIN" is-active --quiet "$API_SERVICE"; then
      abort_after_api_failure "o serviço $API_SERVICE não está active"
    fi
    if ! health_request "$API_HEALTH_URL"; then
      abort_after_api_failure "health check da API falhou: $API_HEALTH_URL"
    fi
    API_HEALTH_STATUS="OK"
  fi

  if [[ "$WEBGL_RESTARTED" == true ]]; then
    "$SYSTEMCTL_BIN" is-active --quiet "$WEBGL_SERVICE" || \
      die "O serviço $WEBGL_SERVICE não está active."
    health_request "$WEBGL_HEALTH_URL" || \
      die "O servidor WebGL não passou no health check: $WEBGL_HEALTH_URL"
    WEBGL_HEALTH_STATUS="OK"
  fi
}

status_word() {
  local changed="$1"
  local changed_word="$2"
  [[ "$changed" == true ]] && printf '%s' "$changed_word" || printf 'sem alterações'
}

print_summary() {
  log ""
  log "========================================"
  log "Deploy RedeLab concluído"
  log "========================================"
  log "Commit anterior: $LOCAL_COMMIT"
  log "Commit atual:    ${DEPLOYED_COMMIT:-$REMOTE_COMMIT}"
  log ""
  log "Backend:    $(status_word "$BACKEND_CHANGED" atualizado)"
  log "Migration:  $(status_word "$MIGRATIONS_CHANGED" aplicada)"
  log "WebGL:      $(status_word "$WEBGL_CHANGED" atualizada)"
  log ""
  log "Backup:"
  log " ${BACKUP_FILE:-não necessário}"
  if [[ -n "$BACKEND_PREVIOUS_DIR" ]]; then
    log "Backend anterior:"
    log " $BACKEND_PREVIOUS_DIR"
  fi
  if [[ -n "$WEBGL_ROLLBACK_DIR" ]]; then
    log "Rollback WebGL:"
    log " $WEBGL_ROLLBACK_DIR"
  fi
  log ""
  log "Serviços:"
  log " $API_SERVICE  $([[ "$API_RESTARTED" == true ]] && printf 'active' || printf 'não reiniciado')"
  log " $WEBGL_SERVICE  $([[ "$WEBGL_RESTARTED" == true ]] && printf 'active' || printf 'não reiniciado')"
  log ""
  log "Health:"
  log " API     $API_HEALTH_STATUS"
  log " WebGL   $WEBGL_HEALTH_STATUS"
  log "========================================"
}

main() {
  trap 'exit_code=$?; handle_error "$LINENO" "$exit_code"' ERR
  trap cleanup EXIT
  parse_arguments "$@"

  step "Validando ambiente..."
  validate_environment

  step "Verificando Git..."
  validate_git_and_fetch

  step "Analisando alterações..."
  classify_changed_files
  print_change_plan
  if [[ "$HAS_UPDATES" == false ]]; then
    log "RedeLab já está atualizado."
    exit 0
  fi
  if [[ "$CHECK_MODE" == true ]]; then
    log ""
    log "Modo --check concluído: nenhuma alteração operacional foi realizada."
    exit 0
  fi

  pull_fast_forward

  step "Preparando e testando o backend em staging..."
  prepare_backend_if_needed

  step "Protegendo o banco e executando migrations..."
  apply_migrations_with_backup

  step "Promovendo o backend validado..."
  promote_backend

  step "Atualizando Build WebGL..."
  if [[ "$WEBGL_CHANGED" == true ]]; then
    update_webgl
  else
    log "Build WebGL sem alterações."
  fi

  step "Reiniciando serviços afetados..."
  restart_services

  step "Validando serviços e apresentando resumo..."
  validate_restarted_services
  print_summary
  log "===== Deploy concluído em $(date --iso-8601=seconds) ====="
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
