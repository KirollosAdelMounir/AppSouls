(() => {
    document.querySelectorAll('[data-public-collection]').forEach(form => {
        addEventListener('pageshow', () => {
            const button = form.querySelector('button[type="submit"]');
            if (button) { button.disabled = false; button.textContent = 'Submit response'; }
        });
        form.addEventListener('submit', () => {
            const button = form.querySelector('button[type="submit"]');
            if (button) { button.disabled = true; button.textContent = 'Submitting…'; }
        });
    });
})();
