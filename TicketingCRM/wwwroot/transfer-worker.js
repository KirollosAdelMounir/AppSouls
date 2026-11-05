// Streaming download bridge only. No caching, background sync, credential storage or interception of application APIs.
const pending = new Map();
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('message', event => {
    const data = event.data, port = event.ports[0];
    if (!port || data?.type !== 'download-stream' || !/^[a-f0-9-]{36}$/.test(data.id) || !event.source?.url?.startsWith(self.location.origin + '/')) return;
    const entry = { port, name: String(data.name).slice(0, 180), total: data.total, controller: null, waiting: null };
    pending.set(data.id, entry);
    const expiry = entry.expiry = setTimeout(() => { pending.delete(data.id); port.close(); }, 60000);
    port.onmessage = message => {
        const value = message.data;
        if (value.type === 'write') {
            if (!entry.controller) { entry.waiting = value.bytes; return; }
            if (entry.controller.desiredSize > 0) { entry.controller.enqueue(value.bytes); port.postMessage('written'); }
            else entry.waiting = value.bytes; // At most one chunk: sender awaits acknowledgement.
        } else if (value.type === 'close') { entry.controller?.close(); port.postMessage('closed'); port.close(); clearTimeout(expiry); }
        else if (value.type === 'abort') { entry.controller?.error(new Error('Transfer aborted')); port.close(); clearTimeout(expiry); }
    };
    port.start(); port.postMessage('registered');
});
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);
    if (url.origin !== self.location.origin || !url.pathname.startsWith('/_appsouls-transfer/')) return;
    const id = url.pathname.split('/').pop(), entry = pending.get(id);
    if (!entry) { event.respondWith(new Response('Transfer expired', { status: 410 })); return; }
    clearTimeout(entry.expiry);
    pending.delete(id); // A transfer URL can be consumed only once.
    const stream = new ReadableStream({
        start(controller) { entry.controller = controller; },
        pull(controller) { if (entry.waiting) { controller.enqueue(entry.waiting); entry.waiting = null; entry.port.postMessage('written'); } },
        cancel() { entry.port.postMessage('cancelled'); entry.port.close(); }
    }, { highWaterMark: 1 });
    const headers = { 'Content-Type': 'application/octet-stream', 'Cache-Control': 'no-store',
        'Content-Disposition': `attachment; filename="AppSouls-download"; filename*=UTF-8''${encodeURIComponent(entry.name).replace(/'/g, '%27')}` };
    if (Number.isSafeInteger(entry.total) && entry.total >= 0) headers['Content-Length'] = String(entry.total);
    event.respondWith(new Response(stream, { headers }));
});
