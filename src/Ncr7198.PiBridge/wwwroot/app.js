(() => {
  const $ = id => document.getElementById(id);
  const state = {
    mode: 'lines',
    bridgeUrl: window.location.origin,
    canAttemptPrint: false,
    printInProgress: false,
    logoData: null,
    webVersion: null,
    piVersion: null
  };
  const connectionForm = $('connection-form');
  const bridgeUrl = $('bridge-url');
  const form = $('receipt-form');
  const text = $('receipt-text');
  const lineNumbers = $('line-numbers');
  const copies = $('copies');
  const cut = $('cut');
  const compressed = $('compressed');
  const message = $('message');
  const preferenceIds = ['pre-lines', 'post-lines', 'compressed', 'cut', 'copies', 'logo-position'];
  const textLinesPerInch = 7.52;
  const printerDotsPerInch = 203;
  const drafts = {
    lines: text.value,
    content: 'Thanks for visiting Northstar Market. This Content mode sample is written as a paragraph so the bridge can automatically wrap it to the selected receipt width. Edit or replace this text, then preview the result before printing.'
  };

  function normalizeBridgeUrl(value) {
    const raw = value.trim();
    if (!raw) throw new Error('Enter the Pi bridge address and port.');
    const url = new URL(/^https?:\/\//i.test(raw) ? raw : `http://${raw}`);
    if (!['http:', 'https:'].includes(url.protocol)) throw new Error('Bridge URL must use http:// or https://.');
    return url.origin;
  }

  function loadBridgeUrl() {
    try { state.bridgeUrl = normalizeBridgeUrl(localStorage.getItem('ncr7198.bridgeUrl') || window.location.origin); }
    catch { state.bridgeUrl = window.location.origin; }
    bridgeUrl.value = state.bridgeUrl;
  }

  function apiUrl(path) {
    return `${state.bridgeUrl}${path}`;
  }

  function showVersions() {
    const web = state.webVersion || 'unknown';
    const pi = state.piVersion || 'unknown';
    const versions = $('versions');
    versions.textContent = `Web ${web} · Pi ${pi}`;
    versions.classList.toggle('mismatch', Boolean(state.webVersion && state.piVersion && state.webVersion !== state.piVersion));
  }

  async function loadWebVersion() {
    try {
      const response = await fetch('version.txt', { cache: 'no-store' });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      state.webVersion = (await response.text()).trim();
    } catch { state.webVersion = null; }
    showVersions();
  }

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
    if (mode !== state.mode) {
      drafts[state.mode] = text.value;
      state.mode = mode;
      text.value = drafts[mode];
      text.scrollTop = 0;
    }
    document.querySelectorAll('[data-mode]').forEach(b => b.classList.toggle('active', b.dataset.mode === mode));
    $('mode-help').textContent = mode === 'lines'
      ? 'Each textarea row becomes one literal width-limited line. Spaces and blank lines are preserved.'
      : 'Content is word-wrapped for print. Explicit line breaks are preserved, with no editor width limit.';
    syncOptions();
  }

  function syncLineNumbers() {
    const count = text.value.split('\n').length;
    lineNumbers.textContent = Array.from({ length: count }, (_, index) => index + 1).join('\n');
    lineNumbers.scrollTop = text.scrollTop;
  }

  function syncOptions() {
    const multiple = Number(copies.value) > 1;
    if (multiple) cut.checked = true;
    cut.disabled = multiple;
    $('cut-note').classList.toggle('hidden', !multiple);
    const width = compressed.checked ? 56 : 44;
    $('column-rule').textContent = state.mode === 'content' ? `${width} characters wide` : `${width}-character maximum`;
    $('preview-width').textContent = `${width} characters wide`;
    $('character-count').textContent = `${text.value.length.toLocaleString()} / 16,384`;
    syncLineNumbers();
  }

  function payload() {
    const value = text.value.replace(/\r\n/g, '\n');
    return {
      printId: $('print-id').value.trim() || null,
      prePrintLines: Number($('pre-lines').value),
      lines: state.mode === 'lines' ? value.split('\n') : null,
      content: state.mode === 'content' ? value : null,
      postPrintLines: Number($('post-lines').value),
      wrap: state.mode === 'content' ? 'word' : 'none',
      compressed: compressed.checked,
      cut: cut.checked,
      copies: Number(copies.value),
      logo: state.logoData,
      logoPosition: $('logo-position').value
    };
  }

  function showMessage(value, kind) {
    message.textContent = value;
    message.className = `message ${kind}`;
  }

  function setPrintAvailability(available, reason = '') {
    state.canAttemptPrint = available;
    const button = $('print-button');
    button.disabled = !available || state.printInProgress;
    button.title = available ? '' : reason;
  }

  function clearReceiptPreview() {
    $('preview-card').classList.add('hidden');
    $('preview').replaceChildren();
    $('paper-length').textContent = '';
  }

  function clearJsonPreview() {
    $('json-preview-card').classList.add('hidden');
    $('json-preview').textContent = '';
    $('copy-json-button').disabled = true;
    $('copy-json-button').textContent = 'Copy JSON';
  }

  function estimatePaperInches(lines) {
    const printed = lines.filter(line => line !== '[CUT]');
    const logoBands = printed.reduce((total, line) => {
      const match = /^\[LOGO: \d+x(\d+)\]$/.exec(line);
      return total + (match ? Math.ceil(Number(match[1]) / 24) : 0);
    }, 0);
    return (printed.length - logoBands) / textLinesPerInch + logoBands * 24 / printerDotsPerInch;
  }

  async function writeClipboard(value) {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(value);
      return;
    }

    const helper = document.createElement('textarea');
    helper.value = value;
    helper.setAttribute('readonly', '');
    helper.style.position = 'fixed';
    helper.style.opacity = '0';
    document.body.appendChild(helper);
    helper.select();
    const copied = document.execCommand('copy');
    helper.remove();
    if (!copied) throw new Error('The browser did not allow clipboard access.');
  }

  async function post(path) {
    const response = await fetch(apiUrl(path), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) });
    const body = await response.json().catch(() => ({ error: `HTTP ${response.status}` }));
    if (!response.ok) throw new Error(body.error || `HTTP ${response.status}`);
    return body;
  }

  async function run(button, action) {
    if (!form.reportValidity()) return;
    message.classList.add('hidden');
    if (button.id === 'print-button') state.printInProgress = true;
    button.disabled = true;
    try { await action(); }
    catch (error) { showMessage(error.message, 'error'); }
    finally {
      if (button.id === 'print-button') state.printInProgress = false;
      button.disabled = button.id === 'print-button' ? !state.canAttemptPrint : false;
    }
  }

  document.querySelectorAll('[data-mode]').forEach(button => button.addEventListener('click', () => setMode(button.dataset.mode)));
  [copies, compressed, text].forEach(control => control.addEventListener('input', syncOptions));
  text.addEventListener('scroll', syncLineNumbers);
  preferenceIds.forEach(id => $(id).addEventListener('change', savePreferences));
  $('logo').addEventListener('change', async event => {
    const input = event.currentTarget;
    const file = input.files[0];
    state.logoData = null;
    input.setCustomValidity('');
    if (!file) return;
    if (file.size > 8 * 1024 * 1024) {
      input.setCustomValidity('Logo images cannot exceed 8 MB.');
      input.reportValidity();
      return;
    }

    input.setCustomValidity('Reading logo image...');
    try {
      state.logoData = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(new Error('The logo image could not be read.'));
        reader.readAsDataURL(file);
      });
      input.setCustomValidity('');
    } catch (error) {
      input.setCustomValidity(error.message);
      input.reportValidity();
    }
  });

  connectionForm.addEventListener('submit', async event => {
    event.preventDefault();
    try {
      state.bridgeUrl = normalizeBridgeUrl(bridgeUrl.value);
      bridgeUrl.value = state.bridgeUrl;
      localStorage.setItem('ncr7198.bridgeUrl', state.bridgeUrl);
      setPrintAvailability(false, 'Checking the Pi bridge.');
      await checkHealth();
    } catch (error) {
      bridgeUrl.setCustomValidity(error.message);
      bridgeUrl.reportValidity();
    }
  });
  bridgeUrl.addEventListener('input', () => bridgeUrl.setCustomValidity(''));

  $('preview-button').addEventListener('click', event => {
    clearReceiptPreview();
    run(event.currentTarget, async () => {
      const lines = await post('/api/preview');
      const preview = $('preview');
      preview.replaceChildren(...lines.map(line => {
        const row = document.createElement('div');
        row.className = line === '[CUT]' ? 'preview-line cut' : line.startsWith('[LOGO:') ? 'preview-line logo' : 'preview-line';
        row.textContent = line;
        return row;
      }));
      $('paper-length').textContent = `Estimated paper: ~${estimatePaperInches(lines).toFixed(2)} in`;
      $('preview-card').classList.remove('hidden');
      $('preview-card').scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });

  $('json-preview-button').addEventListener('click', event => {
    clearJsonPreview();
    run(event.currentTarget, async () => {
      await post('/api/preview');
      $('json-preview').textContent = JSON.stringify(payload(), null, 2);
      $('copy-json-button').disabled = false;
      $('json-preview-card').classList.remove('hidden');
      $('json-preview-card').scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });

  $('copy-json-button').addEventListener('click', async event => {
    const button = event.currentTarget;
    try {
      await writeClipboard($('json-preview').textContent);
      button.textContent = 'Copied';
      setTimeout(() => { button.textContent = 'Copy JSON'; }, 1500);
    } catch (error) { showMessage(error.message, 'error'); }
  });

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
      const response = await fetch(apiUrl('/api/health'), { cache: 'no-store' });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const health = await response.json();
      state.piVersion = health.version || null;
      showVersions();
      if (health.transportMode === 'File') {
        $('health').textContent = 'Development file mode';
        $('health').className = 'status';
        setPrintAvailability(false, 'Select an online Pi bridge to print.');
      } else if (health.printerAvailable) {
        $('health').textContent = 'Pi + printer online';
        $('health').className = 'status ok';
        setPrintAvailability(true);
      } else {
        $('health').textContent = 'Pi online';
        $('health').className = 'status warn';
        setPrintAvailability(true);
      }
    } catch {
      state.piVersion = null;
      showVersions();
      $('health').textContent = 'Pi offline';
      $('health').className = 'status bad';
      setPrintAvailability(false, 'The Pi bridge is offline.');
    }
  }

  loadBridgeUrl();
  loadWebVersion();
  loadPreferences();
  setMode(state.mode);
  syncOptions();
  checkHealth();
  setInterval(checkHealth, 5000);
})();
