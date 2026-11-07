export function insert(input, prefix, suffix) {
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;
    const selected = input.value.slice(start, end) || 'Text';
    if (input.value.length - (end - start) + prefix.length + selected.length + suffix.length > input.maxLength) { input.focus(); return; }
    input.setRangeText(prefix + selected + suffix, start, end, 'select');
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.focus();
    input.setSelectionRange(start + prefix.length, start + prefix.length + selected.length);
}
