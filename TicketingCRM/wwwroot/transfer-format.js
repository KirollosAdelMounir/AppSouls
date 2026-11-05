export function formatBytes(bytes) {
    if (!Number.isFinite(bytes) || bytes < 0) return 'Unknown';
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB'];
    let unit = 0, value = bytes;
    while (value >= 1000 && unit < units.length - 1) { value /= 1000; unit++; }
    return `${value.toLocaleString(undefined, { maximumFractionDigits: unit === 0 ? 0 : 2 })} ${units[unit]}`;
}
