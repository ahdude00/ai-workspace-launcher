const term = new Terminal({ screenReaderMode: true, allowProposedApi: false });
const fit = new FitAddon.FitAddon();
term.loadAddon(fit);
const container = document.getElementById('terminal');
term.open(container);
const bridge = window.chrome.webview;
term.onData(data => bridge.postMessage({ type: 'input', data }));
term.onBinary(data => bridge.postMessage({ type: 'binary', data }));
term.onResize(({ cols, rows }) => bridge.postMessage({ type: 'resize', cols, rows }));
bridge.addEventListener('message', event => {
    term.write(event.data, () => bridge.postMessage({ type: 'drained' }));
});
new ResizeObserver(() => fit.fit()).observe(container);
fit.fit();
bridge.postMessage({ type: 'ready', cols: term.cols, rows: term.rows });
term.focus();
