// Substitute only the native message transport; use the packaged xterm and the real bridge.
const transport = new EventTarget();
Object.defineProperty(window.chrome, 'webview', { value: transport });
transport.postMessage = msg => {
    if (msg.type === 'ready') {
        transport.dispatchEvent(new MessageEvent('message', { data: '\x1b[31mİş görüşü — çığ🌍\x1b[0m' }));
    } else if (msg.type === 'drained') {
        const row = term.buffer.active.getLine(0);
        if (row.translateToString(true) === 'İş görüşü — çığ🌍' && row.getCell(0).getFgColor() === 1) {
            document.body.dataset.verified = 'yes';
        }
    }
};
