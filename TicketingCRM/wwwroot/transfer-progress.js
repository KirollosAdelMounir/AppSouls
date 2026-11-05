// Shared transport UI. All sizes use decimal SI units; exact byte counts remain visible.
import {formatBytes} from './transfer-format.js';
export {formatBytes};
const transferState=window.__appSoulsTransferState ||= { transfers:new Map(),bound:false,timer:null };
const transfers = transferState.transfers;
async function browserStream(name, total, signal) {
    const registration = await navigator.serviceWorker.register('/transfer-worker.js', { scope: '/' });
    await navigator.serviceWorker.ready;
    const id = crypto.randomUUID(), channel = new MessageChannel(), waiting = [];
    const next = () => new Promise((resolve, reject) => waiting.push({ resolve, reject }));
    channel.port1.onmessage = event => {
        if(event.data==='cancelled'){while(waiting.length)waiting.shift().reject(new DOMException('Download cancelled','AbortError'));return;}
        waiting.shift()?.resolve(event.data);
    };
    channel.port1.start();
    const ready = next();
    registration.active.postMessage({ type: 'download-stream', id, name, total }, [channel.port2]);
    await ready;
    const frame = document.createElement('iframe'); frame.hidden = true; frame.src = `/_appsouls-transfer/${id}`; document.body.append(frame);
    const abort=()=>{channel.port1.postMessage({type:'abort'});while(waiting.length)waiting.shift().reject(new DOMException('Download cancelled','AbortError'));frame.remove();};
    signal.addEventListener('abort',abort,{once:true});
    return {
        async write(bytes) { const ack=next();channel.port1.postMessage({type:'write',bytes},[bytes.buffer]);await ack; },
        async close() { const ack=next();channel.port1.postMessage({type:'close'});await ack;signal.removeEventListener('abort',abort);channel.port1.close();setTimeout(()=>frame.remove(),1000); },
        async abort() { abort(); }
    };
}
function host() {
    let result = document.getElementById('transfer-progress-host');
    if (!result) { result = document.createElement('aside'); result.id = 'transfer-progress-host'; result.className = 'transfer-progress-host'; result.setAttribute('aria-label', 'File transfer progress'); document.body.append(result); }
    return result;
}
export function begin(direction, filename, total = null, triggerId = null) {
    const id = crypto.randomUUID();
    const panel = document.createElement('section'); panel.className = 'panel transfer-progress page-enter';
    const heading = document.createElement('h2'); heading.textContent = `${direction}: ${filename}`;
    const bar = document.createElement('progress'); bar.max = 100; bar.setAttribute('aria-label', `${direction} progress`);
    const detail = document.createElement('p');
    const exact = document.createElement('small');
    const stage = document.createElement('p'); stage.setAttribute('role', 'status'); stage.setAttribute('aria-live', 'polite');
    const cancel = document.createElement('button'); cancel.type = 'button'; cancel.className = 'button secondary'; cancel.textContent = 'Cancel transfer';
    panel.append(heading, bar, detail, exact, stage, cancel); host().append(panel);
    const value = { panel, bar, detail, exact, stage, cancel, total, bytes: 0, started: performance.now(), lastAt: performance.now(), lastBytes: 0, rate: 0, trigger: (triggerId && document.getElementById(triggerId)) || document.activeElement, controller: new AbortController() };
    cancel.addEventListener('click', () => value.controller.abort()); cancel.hidden = true;
    transfers.set(id, value); update(id, 0, 'Starting'); return id;
}
export function update(id, bytes, stage = 'Transferring') {
    const value = transfers.get(id); if (!value) return;
    const now = performance.now(), seconds = Math.max((now - value.started) / 1000, .001);
    const interval = (now - value.lastAt) / 1000;
    if (bytes>value.lastBytes && (interval >= .25 || bytes===value.total)) { value.rate = Math.max(0, bytes - value.lastBytes) / interval; value.lastAt = now; value.lastBytes = bytes; }
    if(!value.finished && interval>=2 && bytes===value.lastBytes)value.rate=0;
    if(bytes>value.bytes)value.lastByteAt=now;
    value.bytes = bytes;
    if(now-(value.lastRender||0)<250 && value.stage.textContent===stage && bytes!==value.total)return;
    value.lastRender=now;
    const known = Number.isFinite(value.total) && value.total >= 0;
    const percentage = value.completed ? 100 : known ? (value.total === 0 ? 0 : Math.min(100, bytes / value.total * 100)) : null;
    if (percentage == null) value.bar.removeAttribute('value'); else value.bar.value = percentage;
    const eta = known && value.rate > 0 ? ` · ${Math.max(0, (value.total - bytes) / value.rate).toFixed(0)} s remaining` : '';
    value.detail.textContent = `${formatBytes(bytes)} / ${known ? formatBytes(value.total) : 'unknown total'} · ${percentage == null ? 'percentage unavailable' : percentage.toFixed(1) + '%'} · ${formatBytes(value.rate)}/s · ${seconds.toFixed(0)} s elapsed${eta}`;
    value.exact.textContent = `${bytes.toLocaleString()} bytes transferred${known ? ' of ' + value.total.toLocaleString() + ' bytes' : ''}`;
    // Announce stages, rather than speaking every byte update to assistive technology.
    if (value.stage.textContent !== stage) value.stage.textContent = stage;
}
export function bindCancellation(id, dotnet) {
    const value=transfers.get(id);if(!value)return;
    value.cancel.hidden=false;
    value.controller.signal.addEventListener('abort',()=>dotnet.invokeMethodAsync('CancelUpload').catch(()=>{}),{once:true});
}
export function finish(id, success, message) {
    const value = transfers.get(id); if (!value) return;
    value.finished=true;
    if(success){if(value.total==null)value.total=value.bytes;value.completed=true;}
    if(value.bytes>0)value.rate=value.bytes/Math.max(((value.lastByteAt||performance.now())-(value.transferStarted||value.started))/1000,.001);
    update(id, value.bytes, message || (success ? 'Complete' : 'Failed'));
    if (success && (value.total == null || value.bytes === value.total)) value.bar.value = 100;
    value.panel.dataset.state = success ? 'success' : 'error'; value.cancel.hidden = false; value.cancel.textContent = 'Dismiss';
    value.cancel.replaceWith(value.cancel.cloneNode(true));
    value.panel.lastElementChild.addEventListener('click', () => { value.panel.remove(); transfers.delete(id); if(value.trigger?.isConnected)value.trigger.focus(); });
}
export async function download(url, suggestedName = 'download') {
    if(new URL(url,location.href).origin!==location.origin)throw new Error('Transfers require a same-origin application endpoint.');
    const id = begin('Download', suggestedName), value = transfers.get(id);
    let writable, streamedToBrowser=false;
    try {
        // Request a streaming sink during the user gesture. Never silently buffer an unbounded file.
        if ('showSaveFilePicker' in window) writable = await (await window.showSaveFilePicker({ suggestedName })).createWritable();
        value.transferStarted=performance.now();value.lastAt=value.transferStarted;value.lastBytes=0;
        const response = await fetch(url, { credentials: 'same-origin', signal: value.controller.signal });
        if (!response.ok || response.redirected) throw new Error('Download denied or unavailable.');
        const contentLength = response.headers.get('Content-Length');
        value.total = contentLength == null || response.headers.has("Content-Encoding") ? null : Number(contentLength);
        if(!writable && 'serviceWorker' in navigator && window.isSecureContext){
            writable=await browserStream(suggestedName,value.total,value.controller.signal);streamedToBrowser=true;
        }
        const chunks = [], limit = 64 * 1000 * 1000;
        if (!writable && value.total > limit) throw new Error('This browser requires its native download manager for files over 64 MB. Use “Open native download” below.');
        value.cancel.hidden = false;
        const reader = response.body.getReader();
        while (true) {
            const { value: chunk, done } = await reader.read(); if (done) break;
            const chunkSize=chunk.byteLength; // Transferring an ArrayBuffer to the streaming sink detaches it.
            if (!writable && value.bytes + chunk.byteLength > limit) { await reader.cancel(); throw new Error('This browser requires its native download manager for this large file.'); }
            if (writable) await writable.write(chunk); else chunks.push(chunk);
            update(id, value.bytes + chunkSize, 'Downloading');
        }
        if (value.total != null && value.bytes !== value.total) throw new Error('Download was interrupted before the expected size was received.');
        if (writable) await writable.close();
        else {
            const objectUrl = URL.createObjectURL(new Blob(chunks, { type: response.headers.get('Content-Type') || 'application/octet-stream' }));
            const link = document.createElement('a'); link.href = objectUrl; link.download = suggestedName; link.dataset.nativeTransfer = 'true'; link.click();
            setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
        }
        finish(id, true, writable && !streamedToBrowser ? 'Download saved' : 'Received — handed to the browser download manager');
    } catch (error) {
        if (writable) await writable.abort().catch(() => {});
        value.controller.abort(); finish(id, false, error.name === 'AbortError' ? 'Download cancelled' : error.message);
        const fallback = document.createElement('a'); fallback.href = url; fallback.textContent = 'Open native download'; fallback.className = 'button secondary'; fallback.dataset.nativeTransfer = 'true'; value.panel.append(fallback);
    }
}
// Future HTTP uploads use this same API. Multipart progress measures transmitted request bytes.
export function upload(url, data, filename, headers = {}) {
    if(new URL(url,location.href).origin!==location.origin)throw new Error("Transfers require a same-origin application endpoint.");
    const id = begin('Upload', filename), value = transfers.get(id);
    value.cancel.hidden = false;
    return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest(); xhr.open('POST', url); xhr.withCredentials = true;
        for (const [name, content] of Object.entries(headers)) xhr.setRequestHeader(name, content);
        value.controller.signal.addEventListener('abort', () => xhr.abort(), { once: true });
        xhr.upload.onprogress = event => { value.total = event.lengthComputable ? event.total : null; update(id, event.loaded, 'Uploading'); };
        xhr.upload.onload = () => update(id, value.bytes, 'Upload received — awaiting server confirmation');
        xhr.onload = () => { const ok = xhr.status >= 200 && xhr.status < 300 && new URL(xhr.responseURL||url,location.href).pathname!=='/account/login'; finish(id, ok, ok ? 'Upload accepted by server' : 'Upload rejected by server'); ok ? resolve(xhr) : reject(new Error('Upload rejected')); };
        xhr.onerror = xhr.onabort = () => { finish(id, false, 'Upload interrupted or cancelled'); reject(new Error('Upload interrupted')); };
        xhr.send(data);
    });
}
window.appSoulsTransfers = { begin, bindCancellation, update, finish, download, upload, formatBytes };
if(!transferState.timer)transferState.timer=setInterval(()=>{for(const [id,value] of transfers){if(!value.panel.isConnected)host().append(value.panel);if(!value.finished)update(id,value.bytes,value.stage.textContent);}},500);
if(!transferState.bound){transferState.bound=true;document.addEventListener('click', event => {
    const link = event.target.closest?.('a[href]');
    if (!link || link.dataset.nativeTransfer || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin || !(link.hasAttribute('download') || link.hasAttribute('data-transfer-download') || /\/api\/.*\/(download|export)$/.test(url.pathname))) return;
    event.preventDefault(); download(url.href, link.getAttribute('download') || link.dataset.transferFilename || 'AppSouls-download');
}, true);}

