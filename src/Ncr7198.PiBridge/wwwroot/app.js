(() => {
  const $ = id => document.getElementById(id);
  const state = { mode: localStorage.getItem('ncr7198.mode') || 'content' };
  const form = $('receipt-form');
  const text = $('receipt-text');
  const wrap = $('wrap');
  const copies = $('copies');
  const cut = $('cut');
  const compressed = $('compressed');
  const message = $('message');
  const preferenceIds = ['pre-lines', 'post-lines', 'wrap', 'compressed', 'cut', 'copies'];

  function loadPreferences() {
    try {
      const saved = JSON.parse(localStorage.getItem('ncr7198.preferences') || '{}');
      preferenceIds.forEach(id => {
        if (!(id in saved)) return;
        const control = $(id);
        if (control.type === 'checkbox') control.checked = Boolean(saved[id]);
        else control.value = saved[id];
      });
    } catch { /* Ignore damaged browser-local settings. */ }
  }

  function savePreferences() {
    const saved = {};
    preferenceIds.forEach(id => {
      const control = $(id);
      saved[id] = control.type === 'checkbox' ? control.checked : control.value;
    });
    localStorage.setItem('ncr7198.preferences', JSON.stringify(saved));
  }

  function setMode(mode) {
    state.mode = mode;
    localStorage.setItem('ncr7198.mode', mode);
    document.querySelectorAll('[data-mode]').forEach(b => b.classList.toggle('active', b.dataset.mode === mode));
    wrap.disabled = mode === 'lines';
    if (mode === 'lines') wrap.value = 'none';
    $('mode-help').textContent = mode === 'lines'
      ? 'Each textarea row becomes one literal line. Spaces and blank lines are preserved.'
      : 'Content can be word-wrapped. Explicit line breaks are preserved.';
  }

  function syncOptions() {
    const multiple = Number(copies.value) > 1;
    if (multiple) cut.checked = true;
    cut.disabled = multiple;
    $('cut-note').classList.toggle('hidden', !multiple);
    const width = compressed.checked ? 56 : 44;
    $('column-rule').textContent = `${width} columns`;
    $('preview-width').textContent = `${width}-column mode`;
    $('character-count').textContent = `${text.value.length.toLocaleString()} / 16,384`;
  }

  function payload() {
    const value = text.value.replace(/\r\n/g, '\n');
    return {
      printId: $('print-id').value.trim() || null,
      prePrintLines: Number($('pre-lines').value),
      lines: state.mode === 'lines' ? value.split('\n') : null,
      content: state.mode === 'content' ? value : null,
      postPrintLines: Number($('post-lines').value),
      wrap: wrap.value,
      compressed: compressed.checked,
      cut: cut.checked,
      copies: Number(copies.value)
    };
  }

  function showMessage(value, kind) {
    message.textContent = value;
    message.className = `message ${kind}`;
  }

  async function post(path) {
    const response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) });
    const body = await response.json().catch(() => ({ error: `HTTP ${response.status}` }));
    if (!response.ok) throw new Error(body.error || `HTTP ${response.status}`);
    return body;
  }

  async function run(button, action) {
    if (!form.reportValidity()) return;
    message.classList.add('hidden');
    button.disabled = true;
    try { await action(); }
    catch (error) { showMessage(error.message, 'error'); }
    finally { button.disabled = false; }
  }

  document.querySelectorAll('[data-mode]').forEach(button => button.addEventListener('click', () => setMode(button.dataset.mode)));
  [copies, compressed, text].forEach(control => control.addEventListener('input', syncOptions));
  preferenceIds.forEach(id => $(id).addEventListener('change', savePreferences));

  $('preview-button').addEventListener('click', event => run(event.currentTarget, async () => {
    const lines = await post('/api/preview');
    const preview = $('preview');
    preview.replaceChildren(...lines.map(line => {
      const row = document.createElement('div');
      row.className = line === '[CUT]' ? 'preview-line cut' : 'preview-line';
      row.textContent = line;
      return row;
    }));
    $('preview-card').classList.remove('hidden');
    $('preview-card').scrollIntoView({ behavior: 'smooth', block: 'start' });
  }));

  form.addEventListener('submit', event => {
    event.preventDefault();
    run($('print-button'), async () => {
      const result = await post('/api/print');
      const forced = result.cutForced ? ' Cut was forced because copies is greater than one.' : '';
      showMessage(`Print ${result.status}: ${result.copies} ${result.copies === 1 ? 'copy' : 'copies'} submitted.${forced}`, 'success');
    });
  });

  async function checkHealth() {
    try {
      const health = await fetch('/health').then(response => response.json());
      $('health').textContent = health.printerAvailable ? 'Printer ready' : 'Printer unavailable';
      $('health').className = `status ${health.printerAvailable ? 'ok' : 'bad'}`;
    } catch { $('health').textContent = 'Bridge unavailable'; $('health').className = 'status bad'; }
  }

  loadPreferences();
  setMode(state.mode);
  syncOptions();
  checkHealth();
})();
