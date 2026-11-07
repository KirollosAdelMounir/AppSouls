import {formatBytes} from '../../transfer-format.js';
export function initialize(root) {
    const controller = new AbortController();
    const query = name => root.querySelector(`[data-maintenance-${name}]`);
    const form = query('form');
    let timer, polling = false, busy = false;
    const set = (name, value) => { query(name).textContent = value; };
    async function post(action, data) {
        const response = await fetch(`/api/database-maintenance/${action}`, { method: 'POST', body: data, signal: controller.signal, headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error(response.status === 403 ? 'Platform administrator access is required.' : 'The operation was rejected. Check target compatibility, permissions, confirmation and the server diagnostics.');
        return response.json();
    }
    async function poll() {
        if (polling || controller.signal.aborted) return;
        polling = true;
        try {
            const response = await fetch('/api/database-maintenance/progress', { cache: 'no-store', signal: controller.signal });
            if (!response.ok) throw new Error();
            const value = await response.json();
            if (value.stage !== 'Idle') {
                form.hidden=true;
                query('fields').disabled = true;
                set('destination',`${value.mode}: ${value.source} → ${value.target}`);
                set('stage', value.stage); set('message', value.message || '');
                if (value.percentage == null) { query('progress').removeAttribute('value'); set('percent', 'Database engine stage — percentage unavailable'); }
                else { query('progress').value = value.percentage; set('percent', `${value.percentage.toFixed(1)}%`); }
                set('table', value.table || '—'); set('rows', `${value.rowsCopied.toLocaleString()} / ${value.totalRows.toLocaleString()}`);
                set('speed', `${value.rowsPerSecond.toFixed(1)} rows/s`); set('elapsed', `${value.elapsedSeconds.toFixed(0)} s`);
                const format=formatBytes;
                set('size',`${format(value.bytesCopied)} / ${format(value.totalBytes)}`);
                set('byte-rate',`${format(value.bytesPerSecond)}/s`);
                query('stages').replaceChildren(...(value.stages || []).map(stage => { const item = document.createElement('li'); item.textContent = `${new Date(stage.atUtc).toLocaleTimeString()} · ${stage.stage}${stage.table ? ` · ${stage.table}` : ''}`; return item; }));
                query('finish').hidden = !value.finished;
                if (value.finished) return;
            }
        } catch { if (busy) set('message', 'Reconnecting to the maintenance host…'); }
        finally { polling = false; }
        timer = setTimeout(poll, 1000);
    }
    query('inspect').addEventListener('click', async () => {
        if (busy) return; busy = true;
        try { const state = await post('inspect', new FormData(form)); set('message', `Server accessible. Catalog: ${state.state}. Applied migrations: ${state.appliedMigrations}; pending: ${state.pendingMigrations}. No changes made.`); }
        catch (error) { set('message', error.message); } finally { busy = false; }
    }, { signal: controller.signal });
    form.addEventListener('submit', async event => {
        event.preventDefault(); if (busy) return; busy = true;
        const data = new FormData(form); query('fields').disabled = true;
        try { const result = await post('start', data); set('message', result.message); const secret=form.querySelector('[name="DatabasePassword"]');if(secret)secret.value=''; clearTimeout(timer); timer = setTimeout(poll, 1000); }
        catch (error) { set('message', error.message); query('fields').disabled = false; busy = false; }
    }, { signal: controller.signal });
    query('finish').addEventListener('click', async () => {
        query('finish').disabled = true;
        try { const result = await post('finish', new FormData(form)); set('message', result.message); setTimeout(() => location.assign('/'), 2500); }
        catch (error) { set('message', error.message); query('finish').disabled = false; }
    }, { signal: controller.signal });
    poll();
    return { dispose() { clearTimeout(timer); controller.abort(); } };
}
