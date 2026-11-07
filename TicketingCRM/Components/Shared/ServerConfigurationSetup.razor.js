export function initialize(form) {
    let busy = false;
    const controller = new AbortController();
    const options = { signal: controller.signal };
    const results = (kind) => form.querySelector(`[data-setup-result="${kind}"]`);
    form.addEventListener('input', () => {
        if (!busy) for (const result of form.querySelectorAll('[data-setup-result]')) result.textContent = '';
    }, options);
    async function run(kind, button) {
        if (busy) return;
        busy = true;
        const result = results(kind);
        result.textContent = kind === 'migration' ? 'Activating your system…' : 'Testing connection…';
        result.dataset.state = 'pending';
        // The scanner check needs only antiforgery proof, not database or SMTP credentials.
        const data = kind === 'scanner' ? new FormData() : new FormData(form);
        if (kind === 'scanner') data.set('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
        // Capture before disabling; never persist or log secrets.
        const fieldset = form.querySelector('fieldset');
        fieldset.disabled = true;
        form.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(kind === 'migration' ? form.action : `/setup/test-${kind}`, {
                method: 'POST', body: data, headers: { Accept: 'application/json' }, signal: controller.signal
            });
            const json = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            result.textContent = json?.message || (response.status === 400 ? 'The form has expired or contains invalid fields. Refresh setup and retry.' : response.status === 429 ? 'Too many attempts. Wait a minute and retry.' : 'The connection check could not complete. Retry or refresh setup.');
            result.dataset.state = json?.success ? 'success' : 'error';
            if (json?.redirect && json.success) {
                for (let attempt = 0; attempt < 40; attempt++) {
                    await new Promise(resolve => setTimeout(resolve, 500));
                    try {
                        const ready = await fetch(json.redirect, { signal: controller.signal });
                        if (ready.ok && new URL(ready.url).pathname === '/account/login') { location.assign(json.redirect); return; }
                    } catch { if (controller.signal.aborted) return; }
                }
                result.textContent = 'Activation succeeded. Refresh this page to continue to sign-in.';
                return;
            }
        } catch {
            if (controller.signal.aborted) return;
            result.textContent = 'The server could not be reached. Your entered settings are still here; retry the check.';
            result.dataset.state = 'error';
        } finally {
            busy = false;
            form.removeAttribute('aria-busy');
            fieldset.disabled = false;
            if (kind === 'migration' && result.dataset.state === 'error') result.focus();
            else button?.focus();
        }
    }
    form.addEventListener('click', event => {
        const button = event.target.closest('[data-setup-check]');
        if (button) { event.preventDefault(); run(button.dataset.setupCheck, button); }
    }, options);
    form.addEventListener('submit', event => { event.preventDefault(); run('migration', event.submitter); }, options);
    return { dispose() { controller.abort(); } };
}
