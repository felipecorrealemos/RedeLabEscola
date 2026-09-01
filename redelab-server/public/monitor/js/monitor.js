(() => {
  'use strict';

  const API_URL = '/api/monitor/alunos';
  const FEEDBACK_API_URL = '/api/monitor/feedback';
  const RECONNECT_DELAYS = [1000, 2000, 5000, 10000];
  const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
  const timeFormatter = new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  });

  const elements = {
    filterAll: document.querySelector('#filterAll'),
    filterOnline: document.querySelector('#filterOnline'),
    feedbackBody: document.querySelector('#feedbackBody'),
    feedbackLastUpdate: document.querySelector('#feedbackLastUpdate'),
    feedbackPanel: document.querySelector('#feedbackPanel'),
    feedbackState: document.querySelector('#feedbackState'),
    feedbackTab: document.querySelector('#feedbackTab'),
    feedbackTableContainer: document.querySelector('#feedbackTableContainer'),
    feedbackTypeFilter: document.querySelector('#feedbackTypeFilter'),
    feedbackUserFilter: document.querySelector('#feedbackUserFilter'),
    fullscreenButton: document.querySelector('#fullscreenButton'),
    interfaceState: document.querySelector('#interfaceState'),
    lastUpdate: document.querySelector('#lastUpdate'),
    noOnlineNotice: document.querySelector('#noOnlineNotice'),
    onlineStudents: document.querySelector('#onlineStudents'),
    realtimeStatus: document.querySelector('#realtimeStatus'),
    realtimeText: document.querySelector('#realtimeText'),
    studentsBody: document.querySelector('#studentsBody'),
    studentsPanel: document.querySelector('#studentsPanel'),
    studentsTab: document.querySelector('#studentsTab'),
    summaryGrid: document.querySelector('#summaryGrid'),
    tableContainer: document.querySelector('#tableContainer'),
    totalStudents: document.querySelector('#totalStudents'),
  };

  let socket = null;
  let reconnectAttempt = 0;
  let reconnectTimer = null;
  let refreshTimer = null;
  let requestController = null;
  let feedbackRequestController = null;
  let feedbackRefreshTimer = null;
  let activeView = 'students';
  let popovers = [];
  let currentData = null;
  let activeFilter = 'all';
  let renderRevision = 0;
  let pendingRealtimeUpdate = false;
  const pendingChangedUserIds = new Set();

  function setRealtimeState(state) {
    elements.realtimeStatus.classList.remove('is-connecting', 'is-disconnected');
    if (state === 'connected') {
      elements.realtimeText.textContent = 'Conectado';
      return;
    }
    elements.realtimeStatus.classList.add(
      state === 'connecting' ? 'is-connecting' : 'is-disconnected'
    );
    elements.realtimeText.textContent = state === 'connecting' ? 'Conectando' : 'Desconectado';
  }

  function websocketUrl() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    return `${protocol}//${window.location.host}/ws/monitor`;
  }

  function scheduleReconnect() {
    window.clearTimeout(reconnectTimer);
    const delay = RECONNECT_DELAYS[Math.min(reconnectAttempt, RECONNECT_DELAYS.length - 1)];
    reconnectAttempt += 1;
    reconnectTimer = window.setTimeout(connectWebSocket, delay);
  }

  function connectWebSocket() {
    window.clearTimeout(reconnectTimer);
    setRealtimeState(reconnectAttempt === 0 ? 'connecting' : 'disconnected');

    try {
      socket = new WebSocket(websocketUrl());
    } catch {
      setRealtimeState('disconnected');
      scheduleReconnect();
      return;
    }

    socket.addEventListener('open', () => {
      reconnectAttempt = 0;
      setRealtimeState('connected');
      scheduleRefresh(0);
    });
    socket.addEventListener('message', (event) => {
      try {
        const message = JSON.parse(event.data);
        if (message.type === 'monitor_ready') {
          scheduleRefresh(0);
          scheduleFeedbackRefresh(0);
        } else if (message.type === 'monitor_update') {
          if (message.reason === 'feedback_criado') {
            scheduleFeedbackRefresh(120);
          } else {
            scheduleRefresh(120, {
              realtime: true,
              idUsuario: message.id_usuario,
            });
          }
        }
      } catch {
        // Mensagens desconhecidas não afetam a visualização atual.
      }
    });
    socket.addEventListener('close', () => {
      setRealtimeState('disconnected');
      scheduleReconnect();
    });
    socket.addEventListener('error', () => {
      // O evento close cuida da reconexão e do estado visível.
    });
  }

  function showState(message, error = false) {
    elements.tableContainer.classList.add('d-none');
    elements.interfaceState.classList.remove('d-none');
    elements.interfaceState.classList.toggle('is-error', error);
    elements.interfaceState.replaceChildren();
    const icon = document.createElement('i');
    icon.className = error ? 'bi bi-exclamation-triangle-fill' : 'bi bi-people';
    icon.setAttribute('aria-hidden', 'true');
    const text = document.createElement('span');
    text.textContent = message;
    elements.interfaceState.append(icon, text);
  }

  function showLoading() {
    elements.tableContainer.classList.add('d-none');
    elements.interfaceState.classList.remove('d-none', 'is-error');
    elements.interfaceState.innerHTML =
      '<div class="spinner-border text-primary" aria-hidden="true"></div><span>Carregando alunos…</span>';
  }

  function showFeedbackState(message, error = false) {
    elements.feedbackTableContainer.classList.add('d-none');
    elements.feedbackState.classList.remove('d-none');
    elements.feedbackState.classList.toggle('is-error', error);
    elements.feedbackState.replaceChildren();
    const icon = document.createElement('i');
    icon.className = error ? 'bi bi-exclamation-triangle-fill' : 'bi bi-chat-left-text';
    icon.setAttribute('aria-hidden', 'true');
    const text = document.createElement('span');
    text.textContent = message;
    elements.feedbackState.append(icon, text);
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function missionDetails(aluno) {
    if (!aluno.missoes.length) {
      return '<div class="text-secondary small">Nenhuma missão ativa nesta fase.</div>';
    }
    const items = aluno.missoes
      .map((missao) => {
        const state = missao.concluida ? 'done' : 'pending';
        const icon = missao.concluida ? 'bi-check-circle-fill' : 'bi-circle';
        return `<li class="${state}"><i class="bi ${icon}" aria-hidden="true"></i><span>${escapeHtml(missao.nome)}</span></li>`;
      })
      .join('');
    return `<div class="mission-popover"><ul>${items}</ul></div>`;
  }

  function formatLastActivity(aluno) {
    if (aluno.online) return 'Agora';
    if (!aluno.ultimo_acesso) return 'Sem registro';
    const value = new Date(aluno.ultimo_acesso);
    if (Number.isNaN(value.getTime())) return 'Sem registro';

    const today = new Date();
    if (value.toDateString() === today.toDateString()) return timeFormatter.format(value);
    return dateFormatter.format(value);
  }

  function createCell(className, text) {
    const cell = document.createElement('td');
    if (className) cell.className = className;
    if (text !== undefined) cell.textContent = text;
    return cell;
  }

  function createStudentRow(aluno, animationClass = '') {
    const row = document.createElement('tr');
    row.dataset.userId = String(aluno.id_usuario);
    if (animationClass) row.classList.add(animationClass);

    const statusCell = createCell();
    const status = document.createElement('span');
    status.className = `status-badge ${aluno.online ? 'status-online' : 'status-offline'}`;
    const dot = document.createElement('span');
    dot.className = 'student-status-dot';
    dot.setAttribute('aria-hidden', 'true');
    const statusText = document.createElement('span');
    statusText.textContent = aluno.online ? 'Online' : 'Offline';
    status.append(dot, statusText);
    statusCell.append(status);

    const studentCell = createCell('student-name', aluno.nome);
    studentCell.title = aluno.nome;

    const phaseText = aluno.fase_atual
      ? `Fase ${aluno.fase_atual.id_fase} — ${aluno.fase_atual.nome}`
      : 'Sem fase ativa';
    const phaseCell = createCell('phase-name', phaseText);
    phaseCell.title = phaseText;

    const progressCell = createCell();
    const progressButton = document.createElement('button');
    progressButton.type = 'button';
    progressButton.className = 'progress-trigger';
    progressButton.setAttribute('aria-label', `Ver missões de ${aluno.nome}`);
    progressButton.dataset.bsToggle = 'popover';
    progressButton.dataset.bsPlacement = 'top';
    progressButton.dataset.bsHtml = 'true';
    progressButton.dataset.bsTitle = phaseText;
    progressButton.dataset.bsContent = missionDetails(aluno);

    const progress = document.createElement('span');
    progress.className = 'progress';
    progress.setAttribute('role', 'progressbar');
    progress.setAttribute('aria-valuenow', String(aluno.percentual));
    progress.setAttribute('aria-valuemin', '0');
    progress.setAttribute('aria-valuemax', '100');
    const progressBar = document.createElement('span');
    progressBar.className = 'progress-bar';
    progressBar.style.width = `${Math.max(0, Math.min(100, aluno.percentual))}%`;
    progress.append(progressBar);
    const percent = document.createElement('span');
    percent.className = 'progress-percent';
    percent.textContent = `${aluno.percentual}%`;
    progressButton.append(progress, percent);
    progressCell.append(progressButton);

    const missionsCell = createCell(
      'mission-count',
      `${aluno.missoes_concluidas} / ${aluno.total_missoes}`
    );
    const activityCell = createCell('last-activity', formatLastActivity(aluno));

    row.append(statusCell, studentCell, phaseCell, progressCell, missionsCell, activityCell);
    return row;
  }

  function updateSummary(summary) {
    elements.totalStudents.textContent = summary.alunos_cadastrados;
    elements.onlineStudents.textContent = summary.online;
    elements.noOnlineNotice.classList.toggle('d-none', summary.alunos_cadastrados === 0 || summary.online > 0);
  }

  function disposePopovers() {
    for (const popover of popovers) popover.dispose();
    popovers = [];
  }

  function initializePopovers() {
    if (!window.bootstrap?.Popover) return;
    popovers = Array.from(document.querySelectorAll('[data-bs-toggle="popover"]')).map(
      (trigger) =>
        new window.bootstrap.Popover(trigger, {
          container: 'body',
          trigger: 'hover focus',
          sanitize: true,
        })
    );
  }

  function renderVisibleRows(enteringIds = new Set(), changedIds = new Set()) {
    if (!currentData) return;
    disposePopovers();
    elements.studentsBody.replaceChildren();

    if (currentData.alunos.length === 0) {
      showState('Nenhum aluno cadastrado.');
      return;
    }

    const visibleStudents = activeFilter === 'online'
      ? currentData.alunos.filter((aluno) => aluno.online)
      : currentData.alunos;

    if (visibleStudents.length === 0) {
      showState('Nenhum aluno online.');
      return;
    }

    const fragment = document.createDocumentFragment();
    for (const aluno of visibleStudents) {
      let animationClass = '';
      if (activeFilter === 'online' && enteringIds.has(aluno.id_usuario)) {
        animationClass = 'student-row-enter';
      } else if (activeFilter === 'all' && changedIds.has(aluno.id_usuario)) {
        animationClass = 'student-row-status-update';
      }
      fragment.append(createStudentRow(aluno, animationClass));
    }
    elements.studentsBody.append(fragment);
    elements.interfaceState.classList.add('d-none');
    elements.tableContainer.classList.remove('d-none');
    initializePopovers();
  }

  function render(data, { animatePresence = false, changedIds = new Set() } = {}) {
    updateSummary(data.resumo);
    const updatedAt = new Date(data.atualizado_em);
    elements.lastUpdate.textContent = Number.isNaN(updatedAt.getTime())
      ? 'Dados atualizados'
      : `Atualizado às ${timeFormatter.format(updatedAt)}`;

    const previousOnlineIds = new Set(
      (currentData?.alunos || []).filter((aluno) => aluno.online).map((aluno) => aluno.id_usuario)
    );
    const nextOnlineIds = new Set(
      data.alunos.filter((aluno) => aluno.online).map((aluno) => aluno.id_usuario)
    );
    const enteringIds = new Set(
      [...nextOnlineIds].filter((idUsuario) => !previousOnlineIds.has(idUsuario))
    );
    const leavingIds = new Set(
      [...previousOnlineIds].filter((idUsuario) => !nextOnlineIds.has(idUsuario))
    );
    currentData = data;

    const revision = ++renderRevision;
    const shouldAnimateExit =
      animatePresence && activeFilter === 'online' && leavingIds.size > 0;
    if (shouldAnimateExit) {
      const leavingRows = [...leavingIds]
        .map((idUsuario) => elements.studentsBody.querySelector(`[data-user-id="${idUsuario}"]`))
        .filter(Boolean);
      if (leavingRows.length > 0) {
        leavingRows.forEach((row) => row.classList.add('student-row-exit'));
        window.setTimeout(() => {
          if (revision === renderRevision) renderVisibleRows(enteringIds, changedIds);
        }, 320);
        return;
      }
    }

    renderVisibleRows(animatePresence ? enteringIds : new Set(), changedIds);
  }

  async function loadStudents() {
    if (requestController) requestController.abort();
    const controller = new AbortController();
    requestController = controller;
    const updateOptions = {
      animatePresence: pendingRealtimeUpdate,
      changedIds: new Set(pendingChangedUserIds),
    };
    pendingRealtimeUpdate = false;
    pendingChangedUserIds.clear();
    try {
      const response = await fetch(API_URL, {
        headers: { Accept: 'application/json' },
        cache: 'no-store',
        signal: controller.signal,
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      if (!data || !data.resumo || !Array.isArray(data.alunos)) {
        throw new Error('Resposta inválida');
      }
      render(data, updateOptions);
    } catch (error) {
      if (error.name === 'AbortError') {
        pendingRealtimeUpdate ||= updateOptions.animatePresence;
        updateOptions.changedIds.forEach((idUsuario) => pendingChangedUserIds.add(idUsuario));
        return;
      }
      elements.lastUpdate.textContent = 'Não foi possível atualizar os dados';
      showState('API indisponível. Tentaremos novamente ao reconectar.', true);
    } finally {
      if (requestController === controller) requestController = null;
    }
  }

  function feedbackTypeLabel(tipo) {
    if (tipo === 'sugestao') return 'Sugestão';
    if (tipo === 'bug') return 'Bug';
    return 'Comentário';
  }

  function updateFeedbackPlayers(jogadores) {
    const selected = elements.feedbackUserFilter.value;
    const fragment = document.createDocumentFragment();
    const all = document.createElement('option');
    all.value = '';
    all.textContent = 'Todos os jogadores';
    fragment.append(all);
    for (const jogador of jogadores) {
      const option = document.createElement('option');
      option.value = String(jogador.id_usuario);
      option.textContent = jogador.nome;
      fragment.append(option);
    }
    elements.feedbackUserFilter.replaceChildren(fragment);
    if ([...elements.feedbackUserFilter.options].some((option) => option.value === selected)) {
      elements.feedbackUserFilter.value = selected;
    }
  }

  function renderFeedback(data) {
    updateFeedbackPlayers(data.jogadores);
    elements.feedbackBody.replaceChildren();
    const updatedAt = new Date(data.atualizado_em);
    elements.feedbackLastUpdate.textContent = Number.isNaN(updatedAt.getTime())
      ? 'Histórico atualizado'
      : `Atualizado às ${timeFormatter.format(updatedAt)}`;

    if (data.feedbacks.length === 0) {
      showFeedbackState('Nenhum feedback encontrado para os filtros selecionados.');
      return;
    }

    const fragment = document.createDocumentFragment();
    for (const feedback of data.feedbacks) {
      const row = document.createElement('tr');
      const playerCell = createCell('student-name', feedback.jogador);
      const typeCell = createCell();
      const type = document.createElement('span');
      type.className = `feedback-type feedback-type-${feedback.tipo}`;
      type.textContent = feedbackTypeLabel(feedback.tipo);
      typeCell.append(type);
      const commentCell = createCell('feedback-comment', feedback.comentario);
      const versionCell = createCell('', feedback.versao_jogo || '—');
      const sentAt = new Date(feedback.data_envio);
      const dateCell = createCell(
        'last-activity',
        Number.isNaN(sentAt.getTime()) ? 'Sem registro' : dateFormatter.format(sentAt)
      );
      row.append(playerCell, typeCell, commentCell, versionCell, dateCell);
      fragment.append(row);
    }
    elements.feedbackBody.append(fragment);
    elements.feedbackState.classList.add('d-none');
    elements.feedbackTableContainer.classList.remove('d-none');
  }

  async function loadFeedback() {
    feedbackRequestController?.abort();
    const controller = new AbortController();
    feedbackRequestController = controller;
    const params = new URLSearchParams();
    if (elements.feedbackTypeFilter.value) params.set('tipo', elements.feedbackTypeFilter.value);
    if (elements.feedbackUserFilter.value) {
      params.set('id_usuario', elements.feedbackUserFilter.value);
    }

    try {
      const suffix = params.size ? `?${params}` : '';
      const response = await fetch(`${FEEDBACK_API_URL}${suffix}`, {
        headers: { Accept: 'application/json' },
        cache: 'no-store',
        signal: controller.signal,
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      if (!data || !Array.isArray(data.feedbacks) || !Array.isArray(data.jogadores)) {
        throw new Error('Resposta inválida');
      }
      renderFeedback(data);
    } catch (error) {
      if (error.name === 'AbortError') return;
      elements.feedbackLastUpdate.textContent = 'Não foi possível atualizar o histórico';
      showFeedbackState('API de feedback indisponível. Tentaremos novamente.', true);
    } finally {
      if (feedbackRequestController === controller) feedbackRequestController = null;
    }
  }

  function scheduleFeedbackRefresh(delay = 100) {
    window.clearTimeout(feedbackRefreshTimer);
    feedbackRefreshTimer = window.setTimeout(loadFeedback, delay);
  }

  function scheduleRefresh(delay = 100, { realtime = false, idUsuario = null } = {}) {
    pendingRealtimeUpdate ||= realtime;
    const numericId = Number(idUsuario);
    if (Number.isInteger(numericId) && numericId > 0) pendingChangedUserIds.add(numericId);
    window.clearTimeout(refreshTimer);
    refreshTimer = window.setTimeout(loadStudents, delay);
  }

  function changeFilter(event) {
    if (!event.target.checked) return;
    activeFilter = event.target.value;
    renderRevision += 1;
    renderVisibleRows();
  }

  function changeView(view) {
    activeView = view;
    const feedbackVisible = activeView === 'feedback';
    elements.studentsPanel.classList.toggle('d-none', feedbackVisible);
    elements.summaryGrid.classList.toggle('d-none', feedbackVisible);
    elements.feedbackPanel.classList.toggle('d-none', !feedbackVisible);
    elements.studentsTab.classList.toggle('is-active', !feedbackVisible);
    elements.feedbackTab.classList.toggle('is-active', feedbackVisible);
    elements.studentsTab.setAttribute('aria-selected', String(!feedbackVisible));
    elements.feedbackTab.setAttribute('aria-selected', String(feedbackVisible));
    if (feedbackVisible) scheduleFeedbackRefresh(0);
  }

  function updateFullscreenButton() {
    const active = Boolean(document.fullscreenElement);
    const icon = elements.fullscreenButton.querySelector('i');
    const label = elements.fullscreenButton.querySelector('span');
    icon.className = active ? 'bi bi-fullscreen-exit' : 'bi bi-arrows-fullscreen';
    label.textContent = active ? 'Sair da tela cheia' : 'Tela cheia';
  }

  async function toggleFullscreen() {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await document.documentElement.requestFullscreen();
    } catch {
      // Navegadores podem bloquear fullscreen fora de uma ação direta do usuário.
    }
  }

  elements.fullscreenButton.addEventListener('click', toggleFullscreen);
  elements.studentsTab.addEventListener('click', () => changeView('students'));
  elements.feedbackTab.addEventListener('click', () => changeView('feedback'));
  elements.feedbackTypeFilter.addEventListener('change', () => scheduleFeedbackRefresh(0));
  elements.feedbackUserFilter.addEventListener('change', () => scheduleFeedbackRefresh(0));
  elements.filterAll.addEventListener('change', changeFilter);
  elements.filterOnline.addEventListener('change', changeFilter);
  document.addEventListener('fullscreenchange', updateFullscreenButton);
  window.addEventListener('beforeunload', () => {
    window.clearTimeout(reconnectTimer);
    window.clearTimeout(refreshTimer);
    window.clearTimeout(feedbackRefreshTimer);
    requestController?.abort();
    feedbackRequestController?.abort();
    socket?.close(1000, 'page_unload');
  });

  showLoading();
  loadStudents();
  connectWebSocket();
})();
