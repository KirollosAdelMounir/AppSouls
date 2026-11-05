export function initialize(form) {
    const token = new URLSearchParams(location.hash.slice(1)).get('token') || '';
    if (!/^[0-9A-Fa-f]{64}$/.test(token)) return false;
    form.elements.Token.value = token;
    history.replaceState(null, '', location.pathname + location.search);
    return true;
}
