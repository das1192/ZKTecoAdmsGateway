const KEY = () => sessionStorage.getItem('ak') || promptKey();
    function promptKey() {
  const k = prompt('Admin Key:') || '';
    if (k) sessionStorage.setItem('ak', k);
    return k;
}
const H = () => ({'X-Admin-Key': KEY()});

    function toast(msg, ok = true) {
  const t = document.getElementById('toast');
    t.textContent = msg;
    t.className = 'toast show ' + (ok ? 'ok' : 'err');
    clearTimeout(t._t);
  t._t = setTimeout(() => t.className = 'toast', 3500);
}

    function timeAgo(dt) {
  if (!dt) return 'Never';
    const s = Math.floor((Date.now() - new Date(dt)) / 1000);
    if (s < 5)  return 'Just now';
    if (s < 60) return s + 's ago';
    if (s < 3600) return Math.floor(s/60) + 'm ago';
    if (s < 86400) return Math.floor(s/3600) + 'h ago';
    return new Date(dt).toLocaleDateString();
}

    function fmtTime(dt) {
  if (!dt) return '—';
    return new Date(dt).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit',second:'2-digit'});
}

    // ── Toggle auto-pull ───────────────────────────────────────────────────────
    async function toggleAuto(sn, enable) {
  try {
    const r = await fetch(`/api/auto/${sn}/${enable ? 'on' : 'off'}`, {method:'POST', headers: H()});
    const d = await r.json();
    toast(d.message, d.success);
    setTimeout(load, 500);
  } catch(e) {toast('Request failed', false); }
}

    // ── Manual pull ────────────────────────────────────────────────────────────
    async function pull(sn, btn) {
        btn.disabled = true;
    const orig = btn.innerHTML;
    btn.innerHTML = '<span class="spin">↻</span> Pulling...';
    try {
    const r = await fetch(`/api/pull/${sn}`, {method:'POST', headers: H()});
    const d = await r.json();
    toast(d.message, d.success);
    if (d.success) setTimeout(load, 4000);
  } catch(e) {toast('Request failed', false); }
  setTimeout(() => {btn.disabled = false; btn.innerHTML = orig; }, 3000);
}

    // ── Pull all ────────────────────────────────────────────────────────────────
    async function pullAll() {
  try {
    const r = await fetch('/api/pull-all', {method:'POST', headers: H()});
    const d = await r.json();
    toast(d.message, d.success);
    setTimeout(load, 4000);
  } catch(e) {toast('Request failed', false); }
}

    // ── Render devices ─────────────────────────────────────────────────────────
    function renderDevices(devices) {
  const g = document.getElementById('devicesGrid');
    if (!devices.length) {
        g.innerHTML = `<div class="empty">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
        <rect x="2" y="3" width="20" height="14" rx="2"/>
        <path d="M8 21h8M12 17v4"/>
      </svg>
      <p>No devices configured in appsettings.json</p>
    </div>`;
    return;
  }

  g.innerHTML = devices.map(d => {
    const online = d.isOnline;
    const lastPull = d.recentPulls?.[0];
    const stamp = d.lastStamp > 0 ? `Stamp #${d.lastStamp}` : 'No data yet';

    return `
    <div class="device-card ${d.pullPending ? 'pending-row' : ''}">
        <div>
            <div class="device-info">
                <div class="device-icon ${online ? '' : 'offline'}">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                        <rect x="2" y="3" width="20" height="14" rx="2" />
                        <path d="M8 21h8M12 17v4" />
                    </svg>
                </div>
                <div class="device-meta">
                    <h3>
                        ${d.clientName}
                        <span class="client-tag">${d.clientId}</span>
                    </h3>
                    <div class="sn">${d.serialNumber}</div>
                </div>
            </div>
            <div class="device-stats">
                <span>
                    <span class="badge ${online ? 'online' : 'offline'}">
                        ${online ? '● Online' : '○ Offline'}
                    </span>
                </span>
                <span>Last seen: ${timeAgo(d.lastSeen)}</span>
                <span>${stamp}</span>
                ${d.pullPending ? '<span class="badge pending">⏳ Pull pending...</span>' : ''}
                ${lastPull ? `<span class="${lastPull.success ? 'auto-on' : ''}">${lastPull.success ? '✓' : '✗'} ${lastPull.recordCount} records ${timeAgo(lastPull.time)}</span>` : ''}
            </div>
        </div>

        <div class="device-controls">
            <!-- Auto Pull Toggle -->
            <div class="toggle-wrap">
                <span class="toggle-label">Auto</span>
                <label class="toggle ${d.autoPull ? 'on' : ''}">
                    <input type="checkbox" ${d.autoPull ? 'checked' : ''}
                        onchange="toggleAuto('${d.serialNumber}', this.checked)">
                        <div class="toggle-track"></div>
                        <div class="toggle-thumb"></div>
                </label>
                <span class="${d.autoPull ? 'auto-on' : 'auto-off'}">${d.autoPull ? 'ON' : 'OFF'}</span>
            </div>

            <!-- Manual Pull -->
            <button class="btn btn-primary btn-sm"
                onclick="pull('${d.serialNumber}', this)"
                ${!online ? 'disabled title="Device offline"' : ''}>
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                    <polyline points="7 10 12 15 17 10" />
                    <line x1="12" y1="15" x2="12" y2="3" />
                    <path d="M20 21H4" />
                </svg>
                Pull Now
            </button>
        </div>
    </div>`;
  }).join('');
}

    // ── Render activity ────────────────────────────────────────────────────────
    function renderActivity(logs) {
  const w = document.getElementById('activityWrap');
    if (!logs.length) {
        w.innerHTML = `<div class="empty">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
        <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/>
        <polyline points="14 2 14 8 20 8"/>
      </svg>
      <p>No activity yet — pull data from a device to see results here</p>
    </div>`;
    return;
  }

    w.innerHTML = `<table>
        <thead><tr>
            <th>Time</th>
            <th>Device</th>
            <th>Client</th>
            <th>Records</th>
            <th>Result</th>
            <th>Detail</th>
        </tr></thead>
        <tbody>
            ${logs.map(l => `
      <tr>
        <td class="mono">${fmtTime(l.time)}</td>
        <td class="mono">${l.serialNumber}</td>
        <td>${l.serialNumber}</td>
        <td style="font-weight:600;color:${l.recordCount > 0 ? 'var(--text)' : 'var(--muted)'}">${l.recordCount}</td>
        <td><span class="badge ${l.success ? 'success' : 'fail'}">${l.success ? '✓ OK' : '✗ Failed'}</span></td>
        <td class="mono" style="color:var(--muted);max-width:300px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"
          title="${l.message}">${l.message}</td>
      </tr>`).join('')}
        </tbody>
    </table>`;
}

    // ── Main load ──────────────────────────────────────────────────────────────
    let todayRecords = 0;

    async function load() {
  try {
    const r = await fetch('/api/status', {headers: H()});
    if (r.status === 401) {sessionStorage.removeItem('ak'); toast('Wrong admin key', false); return; }
    const data = await r.json();

    document.getElementById('statTotal').textContent   = data.totalDevices ?? '—';
    document.getElementById('statOnline').textContent  = data.onlineDevices ?? '—';

    const autoCount = (data.devices ?? []).filter(d => d.autoPull).length;
    document.getElementById('statAuto').textContent = autoCount;

    // Tally today's records from activity
    const today = new Date().toDateString();
    todayRecords = (data.recentActivity ?? [])
      .filter(l => l.success && new Date(l.time).toDateString() === today)
      .reduce((s, l) => s + l.recordCount, 0);
    document.getElementById('statRecords').textContent = todayRecords.toLocaleString();

    renderDevices(data.devices ?? []);
    renderActivity(data.recentActivity ?? []);

    document.getElementById('statusDot').style.background = 'var(--green)';
  } catch(e) {
        document.getElementById('statusDot').style.background = 'var(--red)';
    console.error(e);
  }
}

    async function loadAttendanceLog() {

         const date =
    document.getElementById('attendanceDate').value;

    if (!date)
    return;

    try {

             const r =
    await fetch(`/api/attendance-log/${date}`, {
        headers: H()
                 });

    const data =
    await r.json();

    renderAttendanceLog(data);

         } catch (e) {
        toast('Failed loading attendance log', false);
         }
     }



    function renderAttendanceLog(items) {

         const wrap =
    document.getElementById('attendanceWrap');

    if (!items.length) {

        wrap.innerHTML =
        `<div class="empty">
                <p>No attendance found for this date</p>
             </div>`;

    return;
         }

    wrap.innerHTML = `
    <table>
        <thead>
            <tr>
                <th>Received</th>
                <th>Device</th>
                <th>Client</th>
                <th>Employee</th>
                <th>Punch Time</th>
            </tr>
        </thead>
        <tbody>
            ${items.map(x => {

                const r = x.record;

                return `
                <tr>
                    <td class="mono">
                        ${fmtTime(x.time)}
                    </td>

                    <td class="mono">
                        ${x.serialNumber}
                    </td>

                    <td>
                        ${x.clientId}
                    </td>

                   <td>
                          ${r.pin ?? ''}
                    </td>

                    <td class="mono">
                        ${r.timestamp ?? ''}
                    </td>
                </tr>`;
            }).join('')}
        </tbody>
    </table>`;
     }








// ── Clock ──────────────────────────────────────────────────────────────────
setInterval(() => {
        document.getElementById('clock').textContent =
        new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}, 1000);

    // Auto-refresh every 10s
    load();
    setInterval(load, 10000);

    const today =
    new Date().toISOString().split('T')[0];

    document.getElementById('attendanceDate').value =
    today;

    loadAttendanceLog();
