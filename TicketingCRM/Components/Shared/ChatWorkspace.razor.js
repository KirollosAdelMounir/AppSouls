const composers = new WeakMap();
export function initialize(root) {
    if (composers.has(root)) return;
    const events = new AbortController();
    composers.set(root, events);
    root.addEventListener('keydown', event => {
        if (event.key !== 'Enter' || event.shiftKey || event.isComposing || event.keyCode === 229 ||
            !event.target.matches('.conversation-composer textarea')) return;
        event.preventDefault();
        if (!event.repeat && !event.target.closest('fieldset')?.disabled) event.target.form.requestSubmit();
    }, { signal: events.signal });
    root.addEventListener('paste', event => {
        if (!event.target.matches('.conversation-composer textarea')) return;
        const files = event.clipboardData?.files;
        if (!files?.length) return; // Ordinary text paste keeps its native behavior.
        event.preventDefault();
        const input = root.querySelector('.conversation-composer input[type=file]');
        const preview = root.querySelector('.conversation-composer textarea');
        if (!input || input.disabled || preview.closest('fieldset')?.disabled) return;
        if (files.length !== 1) {
            preview.setCustomValidity('Paste one attachment at a time.');
            preview.reportValidity();
            preview.setCustomValidity('');
            return;
        }
        // Route clipboard files through the same InputFile/TransferUpload path as chosen files.
        const transfer = new DataTransfer();
        transfer.items.add(files[0]);
        input.files = transfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }, { signal: events.signal });
}
export function dispose(root) {
    composers.get(root)?.abort();
    composers.delete(root);
}
