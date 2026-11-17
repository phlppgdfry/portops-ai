// Read-only explorer over responses recorded from a local run of the real PortOps API (api-snapshot.json).
const $ = id => document.getElementById(id);
const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const t = iso => new Date(iso).toLocaleString('en-GB', { timeZone: 'Europe/Brussels', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
const PR = { Critical: 'bad', High: 'warn', Review: 'info' };
const ST = { Ready: 'ok', Blocked: 'bad', Conflicting: 'warn', Unknown: 'info', Stale: 'warn' };
const snap = await (await fetch('assets/api-snapshot.json')).json();
const items = snap.attention['24'].items;

function ev(list) {
  return list.map(e => `<li><code>${esc(e.id)}</code> ${esc(e.detail)} <span class="dim small">· ${esc(e.source)} · ${t(e.observedAt)}</span></li>`).join('');
}
function readiness(r) {
  return `<div class="po-ready"><div class="po-rh"><b>${r.kind === 'Pickup' ? 'Ready for pickup' : 'Ready for loading'}</b><span class="pill ${ST[r.state] || 'info'}">${esc(r.state)}</span></div>
    <p>${esc(r.explanation)}</p>${r.warnings?.length ? `<p class="po-warn">${r.warnings.map(esc).join('<br>')}</p>` : ''}<ul class="po-ev">${ev(r.evidence)}</ul></div>`;
}
function show(id) {
  document.querySelectorAll('#po-list button').forEach(b => b.setAttribute('aria-current', b.dataset.id === id));
  const v = snap.vehicles[id], d = $('po-detail');
  if (v.status !== 200) {
    d.innerHTML = `<div class="po-h"><h3 class="mono">${id}</h3><span class="pill bad">HTTP ${v.status}</span></div>
      <p>This vehicle belongs to another customer. The API answers <b>404 Not Found</b>, not 403, so Northstar cannot even learn that it exists.</p>
      <p class="dim small">Customer scope comes from the server-side token claim. Query parameters and headers cannot choose a different customer, and the agent's tools inherit the same scope.</p>`;
    return;
  }
  const a = items.find(i => i.vehicleId === id), b = v.body;
  d.innerHTML = `<div class="po-h"><h3 class="mono">${id} <span class="dim small">${esc(b.vehicle.bookingId)}</span></h3>${a ? `<span class="pill ${PR[a.priority]}">${a.priority}</span>` : '<span class="pill ok">No attention needed</span>'}</div>
    ${a ? `<p class="po-reason">${esc(a.reason)}</p>` : ''}
    <div class="po-two">${readiness(b.pickup)}${readiness(b.loading)}</div>
    ${v.procedures?.length ? `<details class="po-proc"><summary>Matched procedures (${v.procedures.length}) · synthetic, Dutch</summary>${v.procedures.map(p => `<p><b>${esc(p.title)}</b> <span class="dim small">v${p.version}</span><br>${esc(p.text)}</p>`).join('')}</details>` : ''}
    <div class="po-gate"><b>Next step: a human approves</b><ol><li>Dispatcher prepares a draft notification from these sources</li><li>A <em>separate</em> reviewer identity checks the exact version and content hash</li><li>Approval triggers one simulated delivery with an audit record; a changed draft needs a new version</li></ol></div>`;
}
$('po-list').innerHTML = items.map(i => `<button type="button" data-id="${i.vehicleId}"><span class="mono">${i.vehicleId}</span><span class="pill ${PR[i.priority]}">${i.priority}</span><small>${esc(i.reason)}</small></button>`).join('')
  + `<button type="button" data-id="DEMO-001"><span class="mono">DEMO-001</span><span class="pill ok">Ready</span><small>Excluded from the list: current evidence, no active hold.</small></button>`;
$('po-list').addEventListener('click', e => { const b = e.target.closest('button'); if (b) show(b.dataset.id); });
$('po-other').addEventListener('click', () => show('DEMO-007'));
show(items[0].vehicleId);
