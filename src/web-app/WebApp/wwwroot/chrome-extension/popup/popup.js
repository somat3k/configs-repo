/**
 * MLS Chrome Extension — Popup Script
 * Connects to background.js, displays quick stats and a mini chat input.
 */

// ── Port connection ───────────────────────────────────────────────────────────
let port = null;
let reconnectTimer = null;

function connect() {
    try {
        port = chrome.runtime.connect({ name: 'mls-popup' });
        port.onMessage.addListener(handleMessage);
        port.onDisconnect.addListener(() => {
            port = null;
            setStatus(false);
            scheduleReconnect();
        });
        port.postMessage({ type: 'SUBSCRIBE' });
        port.postMessage({ type: 'GET_STATUS' });
    } catch {
        setStatus(false);
        scheduleReconnect();
    }
}

function scheduleReconnect() {
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connect, 3000);
}

// ── Message handling ──────────────────────────────────────────────────────────
function handleMessage(msg) {
    switch (msg.type) {
        case 'CONNECTED':
        case 'STATUS':
        case 'WS_STATUS':
            setStatus(msg.connected ?? msg.status === 'online');
            break;
        case 'PRICE_UPDATE':
            handlePrices(msg.data);
            break;
        case 'POSITION_UPDATE':
            handlePositions(msg.data);
            break;
        case 'ARB_OPPORTUNITY':
            handleArb(msg.data);
            break;
        case 'AI_RESPONSE_CHUNK':
            appendAIChunk(msg.data?.chunk || '');
            break;
        case 'AI_RESPONSE_COMPLETE':
            finaliseAI();
            break;
    }
}

// ── Status ────────────────────────────────────────────────────────────────────
function setStatus(online) {
    const dot = document.getElementById('conn-dot');
    const label = document.getElementById('conn-label');
    if (dot) dot.classList.toggle('online', !!online);
    if (label) label.textContent = online ? 'Connected' : 'Offline';
}

// ── Price stats ───────────────────────────────────────────────────────────────
function handlePrices(data) {
    if (!data) return;

    const prices = flattenPrices(data);

    if (prices['BTC-PERP'] != null) {
        const el = document.getElementById('stat-btc');
        if (el) el.textContent = `$${prices['BTC-PERP'].toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }

    if (prices['ETH-PERP'] != null) {
        const el = document.getElementById('stat-eth');
        if (el) el.textContent = `$${prices['ETH-PERP'].toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }
}

function flattenPrices(data) {
    if (!data) return {};
    if (typeof data === 'object' && !Array.isArray(data)) return data;
    if (Array.isArray(data)) {
        return data.reduce((acc, item) => {
            if (item.symbol && item.price != null) acc[item.symbol] = item.price;
            return acc;
        }, {});
    }
    return {};
}

// ── Positions ─────────────────────────────────────────────────────────────────
function handlePositions(positions) {
    const el = document.getElementById('stat-pnl');
    if (!el || !Array.isArray(positions)) return;

    const totalPnl = positions.reduce((sum, p) => sum + (p.unrealisedPnl ?? p.pnl ?? 0), 0);
    const sign = totalPnl >= 0 ? '+' : '';
    el.textContent = `${sign}$${Math.abs(totalPnl).toFixed(2)}`;
    el.style.color = totalPnl >= 0 ? 'var(--positive)' : 'var(--negative)';
}

// ── Arbitrage ─────────────────────────────────────────────────────────────────
function handleArb(data) {
    const el = document.getElementById('stat-arb');
    if (!el || !data) return;

    const profitPct = ((data.profitBps ?? 0) / 100).toFixed(2);
    el.textContent = `${data.path || data.symbol || '—'} +${profitPct}%`;
    el.style.color = 'var(--positive)';
}

// ── AI chat ───────────────────────────────────────────────────────────────────
let aiResponseEl = null;

function sendQuickQuery() {
    const input = document.getElementById('quick-input');
    if (!input || !input.value.trim()) return;

    const text = input.value.trim();
    input.value = '';

    appendChatLine('user', text);

    port?.postMessage({
        type: 'SEND_ENVELOPE',
        payload: {
            type: 'AI_QUERY',
            unique_id: crypto.randomUUID(),
            module_id: 'chrome-extension-popup',
            timestamp: new Date().toISOString(),
            payload: { query: text, streaming: true }
        }
    });

    aiResponseEl = appendChatLine('ai', '');
}

function appendChatLine(role, text) {
    const container = document.getElementById('chat-mini');
    if (!container) return null;

    const el = document.createElement('div');
    el.className = `chat-msg-${role}`;
    el.textContent = role === 'user' ? `You: ${text}` : `AI: ${text}`;
    container.appendChild(el);
    container.scrollTop = container.scrollHeight;
    return el;
}

function appendAIChunk(chunk) {
    if (!aiResponseEl) aiResponseEl = appendChatLine('ai', '');
    if (aiResponseEl) aiResponseEl.textContent += chunk;
    const container = document.getElementById('chat-mini');
    if (container) container.scrollTop = container.scrollHeight;
}

function finaliseAI() {
    aiResponseEl = null;
}

// ── Action buttons ────────────────────────────────────────────────────────────
function initActions() {
    document.getElementById('btn-open-sidebar')?.addEventListener('click', async () => {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tab?.id != null) {
            await chrome.sidePanel.open({ tabId: tab.id });
        }
    });

    document.getElementById('btn-open-app')?.addEventListener('click', () => {
        chrome.storage.sync.get('mlsAppUrl', ({ mlsAppUrl }) => {
            const url = mlsAppUrl || 'http://localhost:5200';
            chrome.tabs.create({ url });
        });
    });

    document.getElementById('btn-settings')?.addEventListener('click', () => {
        chrome.runtime.openOptionsPage?.();
    });
}

// ── Chat input ────────────────────────────────────────────────────────────────
function initChatInput() {
    const input = document.getElementById('quick-input');
    const sendBtn = document.getElementById('quick-send');

    input?.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
            sendQuickQuery();
        }
    });

    sendBtn?.addEventListener('click', sendQuickQuery);
}

// ── Load cached prices from storage (fast first paint) ───────────────────────
async function loadCachedPrices() {
    try {
        const stored = await chrome.storage.session.get('latestPrices');
        if (stored.latestPrices) handlePrices(stored.latestPrices);
    } catch { /* storage may not be available */ }
}

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    initActions();
    initChatInput();
    loadCachedPrices();
    connect();
});
