'use strict';

const $ = selector => document.querySelector(selector);
let token = '', epoch = 0, vehicles = [], overview = null, selected = null;
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
    const [metadata, attention, rows, status] = await Promise.all([
      api('/api/demo'), api('/api/operations/attention'), api('/api/vehicles'), api('/api/agent/status')
    ]);
    if (currentEpoch !== epoch) return;
    overview = attention; vehicles = rows; modelReady = status.configured;
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
  detail.append(sourceList(sources)); updateControls();
}
function setQuestion(question) {
  $('#question').value = question;
  $('#question').focus();
  $('#question-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}
function renderAnswer(result) {
  const box = el('div', 'message assistant'); box.append(el('span', 'speaker', 'PORTOPS AI'));
  if (result.status === 'refused') box.append(el('p', '', 'Deze agent kan alleen operationele gegevens onderzoeken. Hij voert geen wijzigingen of verzendingen uit.'));
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
    history.push({ question, answer: result.findings.map(x => x.text).join('\n').slice(0, 4000) }); history = history.slice(-6);
  } catch (error) {
    if (epoch === currentEpoch) pending.replaceWith(el('p', 'error', error.name === 'AbortError' ? 'Onderzoek gestopt.' : error.message));
  } finally {
    if (epoch === currentEpoch) { busy = false; requestController = null; updateControls(); chat.scrollTop = chat.scrollHeight; }
  }
});
