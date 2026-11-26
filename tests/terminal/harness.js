// Substitute only the native message transport; use the packaged xterm and the real bridge.
const transport = new EventTarget();
Object.defineProperty(window.chrome, 'webview', { value: transport });
transport.postMessageWithAdditionalObjects = (msg, files) => {
    const names = files.map(file => file.name);
    if (msg.type === 'drop' && names.join('|') === 'İlk dosya.pdf|ikinci.udf') {
        document.body.dataset.dropVerified = 'yes';
    }
};
transport.postMessage = msg => {
    if (msg.type === 'ready') {
        const dataTransfer = new DataTransfer();
        for (const name of ['İlk dosya.pdf', 'ikinci.udf']) {
            dataTransfer.items.add(new File(['example'], name));
        }
        document.getElementById('terminal').dispatchEvent(
            new DragEvent('drop', { dataTransfer, bubbles: true, cancelable: true }));
        transport.dispatchEvent(new MessageEvent('message', { data: '\x1b[31mİş görüşü — çığ🌍\x1b[0m' }));
    } else if (msg.type === 'drained') {
        const row = term.buffer.active.getLine(0);
        if (row.translateToString(true) === 'İş görüşü — çığ🌍' && row.getCell(0).getFgColor() === 1) {
            document.body.dataset.verified = 'yes';
        }
    }
};
