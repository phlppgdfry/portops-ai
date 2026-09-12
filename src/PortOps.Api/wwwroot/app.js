'use strict';

const $ = selector => document.querySelector(selector);
let token = '', epoch = 0, vehicles = [], overview = null, selected = null;
let canApprove = false, proposalRows = [], proposalBusy = false, proposalFetch = 0;
let monitoringFetch = 0;
let history = [], modelReady = false, busy = false, requestController = null;
const date = value => value ? new Intl.DateTimeFormat('nl-BE', {
  timeZone: 'Europe/Brussels', dateStyle: 'medium', timeStyle: 'short'
}).format(new Date(value)) + ' · Brussel' : 'Niet bekend';
const stateNames = { Ready: 'Gereed', Blocked: 'Geblokkeerd', Unknown: 'Onbekend', Conflicting: 'Tegenstrijdig' };
const priorityNames = { Critical: 'Verstreken', High: 'Dringend', Review: 'Opvolgen' };
// UI elements are constructed with textContent. Neither model output nor sources become HTML.
function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}
async function api(path, options = {}) {
  const response = await fetch(path, { ...options, headers: {
    'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json', ...options.headers
  } });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    const messages = {
      401: 'Deze toegangscode is niet geldig. Meld je opnieuw aan.',
      403: 'Voor deze actie is een beoordelaarscode nodig.',
      409: 'Dit voorstel is verlopen, gewijzigd of al afgehandeld. Vernieuw het overzicht en controleer opnieuw.',
      429: 'Er loopt al een onderzoek of je hebt te snel opnieuw gevraagd. Probeer het straks nog eens.',
      503: 'De modelverbinding is nog niet ingesteld. Je kunt de terminalgegevens wel bekijken.',
      504: 'Het onderzoek duurde te lang. Er wordt geen gedeeltelijk antwoord getoond.'
    };
    throw new Error(messages[response.status] || problem.detail || 'De aanvraag is mislukt. Probeer opnieuw.');
  }
  return response.json();
}
function updateControls() {
  $('#send').disabled = !modelReady || busy;
  $('#question').disabled = !modelReady || busy;
  document.querySelectorAll('[data-question]').forEach(button => button.disabled = !modelReady || busy);
  $('#cancel').hidden = !busy;
  $('#question-form').setAttribute('aria-busy', String(busy));
}
async function loadWorkspace() {
  const currentEpoch = epoch;
  $('#refresh').disabled = true;
  try {
    const [metadata, attention, rows, status, proposals] = await Promise.all([
      api('/api/demo'), api('/api/operations/attention'), api('/api/vehicles'), api('/api/agent/status'), api('/api/proposals')
    ]);
    if (currentEpoch !== epoch) return;
    overview = attention; vehicles = rows; modelReady = status.configured;
    canApprove = metadata.canApprove; proposalRows = proposals;
    $('#role-label').textContent = canApprove ? 'Beoordelaar' : 'Operator';
    $('#monitoring-panel').hidden = !canApprove;
    $('#review-note').textContent = canApprove
      ? 'Controleer de volledige inhoud en bronnen. Goedkeuring registreert alleen een simulatie; er gaat geen e-mail uit.'
      : 'Je kunt concepten voorbereiden. Meld je met een aparte beoordelaarscode aan om ze goed te keuren of af te wijzen.';
    renderProposals();
    if (canApprove) loadMonitoring();
    $('#customer-label').textContent = metadata.customerId === 'northstar' ? 'Northstar Motors' : 'Harborline Motors';
    $('#scenario-time').textContent = date(metadata.scenarioTime);
    $('#scenario-time').dateTime = metadata.scenarioTime;
    $('#total').textContent = attention.vehiclesInScope;
    $('#attention-total').textContent = attention.items.length;
    $('#critical-total').textContent = attention.items.filter(x => x.priority === 'Critical').length;
    $('#model-status').textContent = modelReady ? 'Model ingesteld' : 'Niet verbonden';
    $('#model-status').classList.toggle('connected', modelReady);
    $('#model-note').textContent = modelReady
      ? 'De agent raadpleegt je operationele gegevens. Controleer zijn uitleg aan de hand van de bronnen.'
      : 'De modelverbinding is nog niet ingesteld. Je kunt de voertuigen en hun bronnen al onderzoeken.';
    $('#data-error').textContent = '';
    $('#login').hidden = true; $('#workspace').hidden = false; $('#logout').hidden = false;
    renderVehicles();
    selected = vehicles.some(x => x.vehicle.id === selected) ? selected : attention.items[0]?.vehicleId || vehicles[0]?.vehicle.id;
    if (selected) selectVehicle(selected);
    updateControls();
  } finally { if (currentEpoch === epoch) $('#refresh').disabled = false; }
}
function renderVehicles() {
  const list = $('#vehicle-list'); list.replaceChildren();
  const orderedIds = [...overview.items.map(x => x.vehicleId), ...vehicles.map(x => x.vehicle.id)];
  for (const id of new Set(orderedIds)) {
    const investigation = vehicles.find(x => x.vehicle.id === id);
    const item = overview.items.find(x => x.vehicleId === id);
    const button = el('button', 'vehicle-row'); button.type = 'button'; button.dataset.vehicle = id;
    const copy = el('span'); copy.append(el('span', 'vehicle-id', id));
    const actualReason = investigation.vehicle.activeHolds[0]?.reason
      || (investigation.loading.state === 'Conflicting' || investigation.pickup.state === 'Conflicting' ? 'Bronnen spreken elkaar tegen'
        : investigation.loading.state === 'Unknown' || investigation.pickup.state === 'Unknown' ? 'Onvoldoende actuele statusgegevens'
          : 'Status of bronkwaliteit opvolgen');
    copy.append(el('span', 'reason', item ? actualReason : 'Geen openstaande aandachtspunten'));
    button.append(copy, el('span', `badge ${item?.priority || 'Ready'}`, item ? priorityNames[item.priority] : 'Gereed'));
    button.addEventListener('click', () => selectVehicle(id)); list.append(button);
  }
  if (vehicles.length === 0) list.append(el('p', 'muted inset', 'Geen voertuigen gevonden binnen jouw klantomgeving.'));
}
function sourceList(sources, title = `Bronnen bekijken (${sources.length})`) {
  const details = el('details'); details.append(el('summary', '', title));
  for (const source of sources) {
    const row = el('div', 'source'); row.append(el('strong', '', source.id), el('p', '', source.detail));
    row.append(el('time', '', `${source.source} · ${date(source.observedAt)}`)); details.append(row);
  }
  return details;
}
function selectVehicle(id) {
  selected = id;
  document.querySelectorAll('[data-vehicle]').forEach(button => {
    const active = button.dataset.vehicle === id;
    button.classList.toggle('selected', active); button.setAttribute('aria-pressed', String(active));
  });
  const result = vehicles.find(x => x.vehicle.id === id);
  const detail = $('#vehicle-detail'); detail.replaceChildren();
  if (!result) return;
  const top = el('div', 'detail-top'); top.append(el('h3', '', id));
  const investigate = el('button', 'quiet', 'Vraag waarom ↗');
  investigate.dataset.question = `Onderzoek ${id}. Waarom vraagt dit voertuig aandacht en wat is nog onbekend?`;
  investigate.addEventListener('click', () => setQuestion(investigate.dataset.question)); top.append(investigate); detail.append(top);
  const grid = el('div', 'readiness-grid');
  for (const [label, assessment] of [['Afhaling / RFP', result.pickup], ['Laden', result.loading]]) {
    const box = el('div'); box.append(el('strong', '', label), el('span', `badge ${assessment.state}`, stateNames[assessment.state])); grid.append(box);
  }
  detail.append(grid);
  for (const hold of result.vehicle.activeHolds) {
    const block = el('div', 'hold'); block.append(el('p', '', hold.reason), el('small', '', `Opvolging: ${hold.owner}`));
    block.append(el('small', '', hold.estimatedCompletion ? `Geschatte afronding: ${date(hold.estimatedCompletion)}. Dit is geen vrijgave.` : 'Afronding: onbekend. Er is geen afhaaltijd bevestigd.')); detail.append(block);
  }
  for (const warning of new Set([...result.pickup.warnings, ...result.loading.warnings])) detail.append(el('p', 'warning', warning));
  const sources = [...new Map([...result.pickup.evidence, ...result.loading.evidence,
    ...result.vehicle.events.map(event => event.evidence)].map(source => [source.id, source])).values()];
  detail.append(sourceList(sources));
  const draft = el('button', 'quiet draft-button', 'Conceptbericht voorbereiden');
  draft.type = 'button'; draft.disabled = proposalBusy;
  draft.addEventListener('click', () => proposalAction('/api/proposals', { vehicleId: id }));
  detail.append(draft);
  const procedureBox = el('div', 'procedure-box'); procedureBox.append(el('p', 'muted', 'Werkinstructies ophalen…'));
  detail.append(procedureBox);
  const selectionEpoch = epoch;
  api(`/api/vehicles/${encodeURIComponent(id)}/procedures`).then(procedures => {
    if (selectionEpoch !== epoch || selected !== id) return;
    procedureBox.replaceChildren();
    const details = el('details'); details.append(el('summary', '', `Fictieve werkinstructies (${procedures.length})`));
    for (const procedure of procedures) {
      const block = el('div', 'source'); block.append(el('strong', '', `${procedure.title} · v${procedure.version}`), el('p', '', procedure.text));
      block.append(el('small', 'muted', `${procedure.evidence.id} · ${date(procedure.evidence.observedAt)}`)); details.append(block);
    }
    procedureBox.append(details);
  }).catch(error => { if (selectionEpoch === epoch && selected === id) procedureBox.replaceChildren(el('p', 'error', error.message)); });
  updateControls();
}
function setQuestion(question) {
  $('#question').value = question;
  $('#question').focus();
  $('#question-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}
function renderAnswer(result) {
  const box = el('div', 'message assistant'); box.append(el('span', 'speaker', 'PORTOPS AI'));
  if (result.status === 'refused') box.append(el('p', '', 'Deze agent kan alleen operationele gegevens onderzoeken. Hij kan concepten voorbereiden; goedkeuren en verzenden zijn geen agenttools.'));
  else if (result.status === 'insufficient_evidence' && result.findings.length === 0)
    box.append(el('p', '', 'Er zijn onvoldoende toegankelijke gegevens om deze vraag te beantwoorden.'));
  for (const finding of result.findings) {
    const item = el('div', 'finding'); item.append(el('p', '', finding.text));
    const cited = result.evidence.filter(source => finding.evidenceIds.includes(source.id));
    item.append(sourceList(cited, `Bekijk ${cited.length === 1 ? 'bron' : 'bronnen'} · ${cited.map(x => x.id).join(', ')}`)); box.append(item);
  }
  if (result.unknowns.length) {
    box.append(el('p', 'muted', 'Nog niet vastgesteld:'));
    const list = el('ul'); for (const unknown of result.unknowns) list.append(el('li', '', unknown)); box.append(list);
  }
  const trace = el('details', 'trace'); trace.append(el('summary', '', `${result.tools.length} toolcalls · ${(result.durationMs / 1000).toFixed(1)} s · Onderzoeksdetails`));
  for (const tool of result.tools) trace.append(el('div', '', `${tool.name} — ${tool.status} — ${tool.durationMs} ms`));
  trace.append(el('div', '', result.usage ? `Tokens: ${result.usage.inputTokens} in / ${result.usage.outputTokens} uit` : 'Tokenverbruik niet gerapporteerd.'));
  trace.append(el('div', '', `Trace: ${result.traceId}`)); box.append(trace);
  return box;
}
$('#login-form').addEventListener('submit', async event => {
  event.preventDefault(); epoch++; const currentEpoch = epoch;
  token = $('#access-token').value.trim(); $('#login-error').textContent = ''; $('#connect').disabled = true;
  try { await loadWorkspace(); $('#access-token').value = ''; }
  catch (error) { if (currentEpoch === epoch) { token = ''; $('#login-error').textContent = error.message; } }
  finally { $('#connect').disabled = false; }
});
$('#refresh').addEventListener('click', () => loadWorkspace().catch(error => $('#data-error').textContent = error.message));
$('#logout').addEventListener('click', () => {
  epoch++; requestController?.abort(); token = ''; history = []; vehicles = []; overview = null; selected = null;
  $('#export-monitoring').disabled = false; $('#refresh-monitoring').disabled = false;
  $('#monitoring-panel').hidden = true; $('#monitoring-panel').open = false; $('#monitoring-traces').replaceChildren(); $('#monitoring-metrics').replaceChildren(); $('#monitoring-error').textContent = ''; $('#monitoring-window').textContent = ''; $('#monitoring-model').textContent = '';
  proposalRows = []; canApprove = false; proposalBusy = false; $('#proposal-list').replaceChildren(); $('#proposal-error').textContent = ''; $('#role-label').textContent = '';
  modelReady = false; busy = false; $('#workspace').hidden = true; $('#login').hidden = false; $('#logout').hidden = true;
  $('#chat').replaceChildren(); $('#vehicle-list').replaceChildren(); $('#vehicle-detail').replaceChildren();
  $('#question').value = ''; $('#access-token').value = ''; $('#customer-label').textContent = 'Onafhankelijke portfolio-demo'; updateControls();
});
document.querySelectorAll('.suggestions button').forEach(button => button.addEventListener('click', () => setQuestion(button.dataset.question)));
$('#cancel').addEventListener('click', () => requestController?.abort());
$('#question-form').addEventListener('submit', async event => {
  event.preventDefault(); if (busy || !modelReady) return;
  const question = $('#question').value.trim(); if (!question) return;
  const currentEpoch = epoch; busy = true; updateControls(); requestController = new AbortController();
  const chat = $('#chat'); chat.querySelector('.welcome')?.remove();
  chat.append(el('div', 'message user', question)); $('#question').value = '';
  const pending = el('div', 'pending', 'Operationele bronnen onderzoeken…'); chat.append(pending); chat.scrollTop = chat.scrollHeight;
  try {
    const result = await api('/api/agent/investigate', { method: 'POST', body: JSON.stringify({ message: question, history }), signal: requestController.signal });
    if (epoch !== currentEpoch) return;
    pending.replaceWith(renderAnswer(result));
    await loadProposals();
    if (epoch !== currentEpoch) return;
    history.push({ question, answer: result.findings.map(x => x.text).join('\n').slice(0, 4000) }); history = history.slice(-6);
  } catch (error) {
    if (epoch === currentEpoch) {
      pending.replaceWith(el('p', 'error', error.name === 'AbortError' ? 'Onderzoek gestopt. Controleer Actievoorstellen op reeds gemaakte concepten.' : error.message));
      await loadProposals();
    }
  } finally {
    if (epoch === currentEpoch) { busy = false; requestController = null; updateControls(); chat.scrollTop = chat.scrollHeight; }
  }
});

const proposalStates = { draft: 'Te controleren', expired: 'Verlopen', executed: 'Simulatie vastgelegd', rejected: 'Afgewezen' };
async function loadProposals() {
  const currentEpoch = epoch, fetchId = ++proposalFetch;
  try {
    const rows = await api('/api/proposals');
    if (currentEpoch !== epoch) return;
    if (fetchId !== proposalFetch) return;
    proposalRows = rows; renderProposals();
  } catch (error) { if (currentEpoch === epoch) $('#proposal-error').textContent = error.message; }
}
function renderProposals() {
  const list = $('#proposal-list'); list.replaceChildren();
  if (!proposalRows.length) list.append(el('p', 'muted', 'Er zijn nog geen voorstellen. Kies een voertuig en maak een conceptbericht.'));
  for (const proposal of proposalRows) {
    const card = el('article', 'proposal-card');
    const heading = el('div', 'detail-top'); heading.append(el('h3', '', `${proposal.vehicleId} · versie ${proposal.version}`), el('span', 'badge', proposalStates[proposal.status] || proposal.status));
    card.append(heading, el('p', 'muted', `Simulatieadres: ${proposal.destination}`), el('strong', '', proposal.subject));
    const content = el('details', 'proposal-content'); content.append(el('summary', '', 'Volledig concept en bronnen controleren'), el('pre', 'proposal-body', proposal.body), sourceList(proposal.evidence));
    card.append(content);
    card.append(el('p', 'muted', `Aangemaakt: ${date(proposal.createdAt)} · Geldig tot: ${date(proposal.expiresAt)} (werkelijke tijd)`));
    const audit = el('details'); audit.append(el('summary', '', `Auditlog (${proposal.audit.length})`));
    const eventLabels = { created: 'Concept aangemaakt', refreshed: 'Nieuwe versie aangemaakt', approved: 'Goedgekeurd', rejected: 'Afgewezen', simulated_delivery: 'Verzending gesimuleerd' };
    for (const entry of proposal.audit) audit.append(el('p', 'muted', `${eventLabels[entry.event] || entry.event} · ${entry.actorId} · v${entry.version} · ${date(entry.at)}`));
    card.append(audit);
    if (proposal.delivery) card.append(el('p', 'delivery-receipt', `Simulatiebewijs: ${proposal.delivery.id}. Geen e-mail verzonden.`));
    if (proposal.status === 'draft' || proposal.status === 'expired') {
      const controls = el('div', 'proposal-controls');
      const refresh = el('button', 'quiet', 'Actualiseren → nieuwe versie'); refresh.disabled = proposalBusy;
      refresh.addEventListener('click', () => proposalAction(`/api/proposals/${proposal.id}/refresh`, { version: proposal.version })); controls.append(refresh);
      if (canApprove && proposal.status === 'draft') {
        const label = el('label', 'review-check'); const check = el('input'); check.type = 'checkbox'; check.disabled = proposalBusy;
        label.append(check, el('span', '', `Ik heb de inhoud en bronnen van versie ${proposal.version} gecontroleerd.`));
        controls.append(label);
        const approve = el('button', 'primary', 'Goedkeuren en verzending simuleren'); approve.disabled = true;
        check.addEventListener('change', () => approve.disabled = !check.checked || proposalBusy);
        approve.addEventListener('click', () => proposalAction(`/api/proposals/${proposal.id}/approve`, { version: proposal.version, payloadHash: proposal.payloadHash }));
        const reject = el('button', 'quiet', 'Afwijzen'); reject.disabled = proposalBusy;
        reject.addEventListener('click', () => proposalAction(`/api/proposals/${proposal.id}/reject`, { version: proposal.version, payloadHash: proposal.payloadHash }));
        controls.append(approve, reject);
      }
      card.append(controls);
    }
    list.append(card);
  }
}
async function proposalAction(path, payload) {
  if (proposalBusy) return;
  const currentEpoch = epoch;
  proposalBusy = true; $('#proposal-error').textContent = ''; renderProposals();
  document.querySelectorAll('.draft-button').forEach(button => button.disabled = true);
  try {
    await api(path, { method: 'POST', body: JSON.stringify(payload) });
    if (currentEpoch !== epoch) return;
    await loadProposals();
    if (currentEpoch !== epoch) return;
    $('#proposals-heading').scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    if (currentEpoch === epoch) { $('#proposal-error').textContent = error.message; await loadProposals(); }
  } finally {
    if (currentEpoch === epoch) {
      proposalBusy = false; renderProposals();
      document.querySelectorAll('.draft-button').forEach(button => button.disabled = false);
    }
  }
}
$('#refresh-proposals').addEventListener('click', loadProposals);

const operationNames = {
  'api.request': 'API-aanvraag', 'agent.investigate': 'Agentonderzoek', 'model.responses': 'Modelaanroep',
  'tool.execute': 'Operationele tool', 'procedure.search': 'Procedures zoeken', 'proposal.create': 'Concept voorbereiden',
  'proposal.refresh': 'Concept actualiseren', 'proposal.approve': 'Goedkeuring controleren',
  'proposal.reject': 'Voorstel afwijzen', 'proposal.persist': 'Voorstel verwerken en opslaan'
};
const ms = value => value === null ? 'Niet gemeten' : `${value.toFixed(1)} ms`;
async function loadMonitoring() {
  if (!canApprove) return;
  const currentEpoch = epoch, requestId = ++monitoringFetch;
  $('#refresh-monitoring').disabled = true; $('#monitoring-error').textContent = '';
  try {
    const result = await api('/api/monitoring');
    if (currentEpoch !== epoch || requestId !== monitoringFetch) return;
    $('#monitoring-window').textContent = `Maximaal ${result.capacity} afgeronde aanvragen uit de laatste ${result.windowMinutes} minuten. Meetvenster vanaf ${date(result.windowStart)}. Het overzicht wordt leeggemaakt bij een serverherstart.`;
    const metrics = $('#monitoring-metrics'); metrics.replaceChildren();
    for (const [label, value] of [['Aanvragen', result.requests], ['HTTP 4xx / 5xx', `${result.clientErrors} / ${result.serverErrors}`], ['Gemiddelde', ms(result.averageMs)], ['95e percentiel', ms(result.p95Ms)]]) {
      const box = el('div'); box.append(el('span', '', label), el('strong', '', String(value))); metrics.append(box);
    }
    $('#monitoring-model').textContent = `${result.modelCalls} gemeten modelcalls · ${result.toolCalls} toolcalls · ` +
      (result.inputTokens === null || result.outputTokens === null ? 'Tokenverbruik niet of onvolledig gemeten.' : `Tokens: ${result.inputTokens} in / ${result.outputTokens} uit.`) +
      (modelReady ? '' : ' De modelverbinding staat nog uit.') +
      (result.traces.some(trace => trace.truncated) ? ' Sommige traces zijn ingekort; staptellingen zijn onvolledig.' : '');
    const list = $('#monitoring-traces'); list.replaceChildren();
    if (!result.traces.length) list.append(el('p', 'muted', 'Nog geen afgeronde aanvragen. Bekijk een voertuig of bereid een concept voor en vernieuw dit overzicht.'));
    else list.append(el('p', 'muted', `Laatste ${Math.min(20, result.traces.length)} van ${result.traces.length} bewaarde aanvragen. HTTP 4xx telt onder andere afgewezen of ongeldige aanvragen; HTTP 5xx telt server- en providerproblemen.`));
    for (const trace of result.traces.slice(0, 20)) {
      const block = el('details', 'request-trace');
      block.append(el('summary', '', `${trace.method} ${trace.route} · HTTP ${trace.statusCode} · ${ms(trace.durationMs)}`));
      block.append(el('p', 'muted', `${date(trace.startedAt)} · Trace ${trace.traceId} · Aanvraag ${trace.requestId}`));
      if (trace.truncated) block.append(el('p', 'warning', 'Deze trace is ingekort vanwege de limiet op het aantal stappen.'));
      const table = el('table', 'trace-table'); const head = el('tr');
      for (const label of ['Stap', 'Start', 'Duur', 'Resultaat']) head.append(el('th', '', label));
      const thead = el('thead'); thead.append(head); table.append(thead);
      const body = el('tbody');
      for (const step of trace.steps) {
        const row = el('tr');
        const label = `${operationNames[step.operation] || step.operation}${step.attributes['tool.name'] ? ` · ${step.attributes['tool.name']}` : ''}`;
        const cell = el('td', '', label); cell.append(el('small', 'span-parent', `Span ${step.spanId}${step.parentSpanId ? ` · onder ${step.parentSpanId}` : ' · hoofdaanvraag'}`));
        row.append(cell, el('td', '', ms(step.offsetMs)), el('td', '', ms(step.durationMs)), el('td', step.status === 'error' ? 'error' : '',
          step.attributes['error.type'] || step.attributes['tool.status'] || step.attributes['proposal.outcome'] || (step.status === 'error' ? 'Fout' : 'OK')));
        body.append(row);
      }
      table.append(body); const overflow = el('div', 'trace-table-wrap'); overflow.append(table); block.append(overflow); list.append(block);
    }
  } catch (error) { if (currentEpoch === epoch && requestId === monitoringFetch) $('#monitoring-error').textContent = error.message; }
  finally { if (currentEpoch === epoch && requestId === monitoringFetch) $('#refresh-monitoring').disabled = false; }
}
$('#refresh-monitoring').addEventListener('click', loadMonitoring);
$('#monitoring-panel').addEventListener('toggle', () => { if ($('#monitoring-panel').open) loadMonitoring(); });
$('#export-monitoring').addEventListener('click', async () => {
  const currentEpoch = epoch; $('#export-monitoring').disabled = true;
  try {
    const result = await api('/api/monitoring/export');
    if (currentEpoch !== epoch) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(result, null, 2)], { type: 'application/json' }));
    const link = el('a'); link.href = url; link.download = 'portops-monitoring.json'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (error) { if (currentEpoch === epoch) $('#monitoring-error').textContent = error.message; }
  finally { if (currentEpoch === epoch) $('#export-monitoring').disabled = false; }
});
