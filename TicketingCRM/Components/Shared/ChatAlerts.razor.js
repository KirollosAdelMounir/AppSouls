let audio, events, storageKey, seen, baseline, titleObserver;
let currentFeed = { unreadCount: 0, conversations: [] };
let sound = true;
const read = key => { try { return sessionStorage.getItem(key); } catch { return null; } };
const write = (key, value) => { try { sessionStorage.setItem(key, value); } catch { /* Session-only fallback. */ } };
async function unlock() {
    // A brief silent play during a real gesture unlocks media in restrictive browsers.
    if (!audio || audio.dataset.unlocked) return;
    audio.dataset.unlocked = 'true';
    audio.muted = true;
    try { await audio.play(); audio.pause(); audio.currentTime = 0; }
    catch { delete audio.dataset.unlocked; }
    finally { audio.muted = false; }
}
export function initialize(key, soundUrl) {
    audio = new Audio(soundUrl);
    audio.preload = "auto";
    storageKey = `appsouls:chat-alerts:${key}`;
    let saved;
    try { saved = JSON.parse(read(storageKey)); } catch { saved = null; }
    baseline = Array.isArray(saved);
    seen = new Set(baseline ? saved.slice(-200) : []);
    sound = read(`${storageKey}:sound`) !== 'off';
    events = new AbortController();
    document.addEventListener('pointerdown', unlock, { signal: events.signal });
    document.addEventListener('keydown', unlock, { signal: events.signal });
    titleObserver = new MutationObserver(() => updateTitle());
    const title = document.querySelector('title');
    if (title) titleObserver.observe(title, { childList: true, characterData: true, subtree: true });
    document.addEventListener('enhancedload', () => updateCounts(currentFeed), { signal: events.signal });
    return sound;
}
function updateTitle() {
    const base = document.title.replace(/^\(\d+\) /, '');
    const next = currentFeed.unreadCount > 0 ? `(${currentFeed.unreadCount}) ${base}` : base;
    if (document.title !== next) document.title = next;
}
export function updateCounts(feed) {
    currentFeed = feed;
    const counts = new Map(feed.conversations.map(x => [x.chatId, x.count]));
    for (const badge of document.querySelectorAll('[data-chat-unread]')) {
        const count = badge.dataset.chatUnread === 'all' ? feed.unreadCount : counts.get(badge.dataset.chatUnread) || 0;
        const text = String(count);
        if (badge.textContent !== text) badge.textContent = text;
        badge.hidden = count === 0;
        badge.setAttribute('aria-label', `${count} unread chat messages`);
    }
    updateTitle();
}
export function check(feed, enabled) {
    updateCounts(feed);
    const incoming = feed.items.filter(message => !seen.has(message.id));
    for (const message of incoming) seen.add(message.id);
    seen = new Set([...seen].slice(-200));
    write(storageKey, JSON.stringify([...seen]));
    const notify = baseline && incoming.length > 0;
    baseline = true;
    if (notify && enabled && audio) {
        audio.currentTime = 0;
        audio.play().catch(() => { /* Browser policy can block sound; the visible alert stays available. */ });
    }
    const reading = document.visibilityState === 'visible' && document.hasFocus()
        ? document.querySelector('[data-chat-reading][data-chat-last-message]') : null;
    return { chatId: notify ? incoming[0].chatId : null,
        readingChatId: reading?.dataset.chatReading || null,
        readingMessageId: reading?.dataset.chatLastMessage || null };
}
export async function setSound(enabled) {
    sound = enabled;
    write(`${storageKey}:sound`, enabled ? 'on' : 'off');
    if (enabled) await unlock();
}
export async function dispose() {
    events?.abort();
    titleObserver?.disconnect();
    document.title = document.title.replace(/^\(\d+\) /, "");
    audio?.pause();
    if (audio) { audio.removeAttribute("src"); audio.load(); }
    audio = undefined;
}
