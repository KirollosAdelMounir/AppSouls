(() => {
    // Enhanced navigation can evaluate scripts again. Register one controller per document.
    const installed = Symbol.for('appsouls.appearance.controller');
    if (document[installed]) return;
    document[installed] = true;
    const key = 'appsouls.theme';
    const media = matchMedia('(prefers-color-scheme: dark)');
    let fallback = 'system';
    const read = () => {
        let value = fallback;
        try {
            value = localStorage.getItem(key);
            if (!value) {
                value = localStorage.getItem('tetrarchia.theme') || fallback;
                localStorage.setItem(key, value);
            }
        } catch { }
        return ['light', 'dark', 'system'].includes(value) ? value : 'system';
    };
    const apply = () => {
        const preference = read();
        const resolved = preference === 'system' ? (media.matches ? 'dark' : 'light') : preference;
        if (document.documentElement.dataset.theme !== resolved) document.documentElement.dataset.theme = resolved;
        document.querySelectorAll('[data-appearance-label]').forEach(label => {
            const text = preference.charAt(0).toUpperCase() + preference.slice(1);
            if (label.textContent !== text) label.textContent = text;
        });
        document.querySelectorAll('[data-appearance-value]').forEach(option => {
            const selected = String(option.dataset.appearanceValue === preference);
            if (option.getAttribute('aria-pressed') !== selected) option.setAttribute('aria-pressed', selected);
        });
    };
    let previousView = '';
    const syncView = () => {
        apply();
        const content = document.querySelector('.view-content');
        const heading = content?.querySelector('h1');
        if (!heading) return;
        const view = location.pathname + ':' + heading.textContent;
        if (previousView && view !== previousView && !matchMedia('(prefers-reduced-motion: reduce)').matches) {
            content.animate([{ opacity: 0, transform: 'translateY(6px)' }, { opacity: 1, transform: 'translateY(0)' }], { duration: 220, easing: 'cubic-bezier(.2,.8,.2,1)' });
        }
        previousView = view;
    };
    apply();
    media.addEventListener('change', () => { if (read() === 'system') apply(); });
    addEventListener('storage', event => { if (event.key === key || event.key === null) apply(); });
    document.addEventListener('click', event => {
        const option = event.target.closest('[data-appearance-value]');
        if (option) {
            fallback = option.dataset.appearanceValue;
            try { localStorage.setItem(key, fallback); } catch { }
            apply();
            const picker = option.closest('details');
            picker.open = false;
            picker.querySelector('summary').focus();
        }
        document.querySelectorAll('[data-appearance-picker][open], [data-form-dropdown][open]').forEach(picker => {
            if (!picker.contains(event.target)) { picker.open = false; picker.querySelector('summary').focus(); }
        });
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const picker = event.target.closest('[data-appearance-picker], [data-form-dropdown]');
        if (picker?.open) { picker.open = false; picker.querySelector('summary').focus(); }
    });
    document.addEventListener('DOMContentLoaded', apply);
    document.addEventListener('enhancedload', apply);
    // Restore the explicit preference when enhanced navigation patches the root element.
    new MutationObserver(syncView).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-theme'] });
})();
