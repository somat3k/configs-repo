/**
 * MLS Chrome Extension — Side Panel JavaScript
 * Connects to background.js via chrome.runtime.connect port
 * Renders: mini trading chart (ApexCharts), positions, arb opportunities, AI chat
 */

// ── Port connection to background ────────────────────────────────────────────
let port = null;
let reconnectTimer = null;
let chart = null;
let chartSeries = [];
const MAX_CHART_CANDLES = 120;

function connect() {
    try {
        port = chrome.runtime.connect({ name: 'mls-sidebar' });
        port.onMessage.addListener(handleMessage);
        port.onDisconnect.addListener(() => {
            port = null;
            setConnectionStatus(false);
            scheduleReconnect();
        });
        port.postMessage({ type: 'SUBSCRIBE' });
        port.postMessage({ type: 'GET_STATUS' });
    } catch {
        scheduleReconnect();
    }
}

function scheduleReconnect() {
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connect, 3000);
}

// ── Message routing ───────────────────────────────────────────────────────────
function handleMessage(msg) {
    switch (msg.type) {
        case 'CONNECTED':
        case 'WS_STATUS':
            setConnectionStatus(msg.connected ?? msg.status === 'online');
            break;
        case 'PRICE_UPDATE':
            handlePriceUpdate(msg.data);
            break;
        case 'CANDLE_UPDATE':
            handleCandleUpdate(msg.data);
            break;
        case 'POSITION_UPDATE':
            renderPositions(msg.data);
            break;
        case 'ARB_OPPORTUNITY':
            renderArbOpportunity(msg.data);
            break;
        case 'AI_RESPONSE_CHUNK':
            appendAIChunk(msg.data);
            break;
        case 'AI_RESPONSE_COMPLETE':
            finaliseAIResponse(msg.data);
            break;
    }
}

// ── Connection status ─────────────────────────────────────────────────────────
function setConnectionStatus(online) {
    const dot = document.getElementById('conn-dot');
    if (dot) dot.classList.toggle('online', !!online);
}

// ── Price updates ─────────────────────────────────────────────────────────────
function handlePriceUpdate(data) {
    if (!data) return;
    const symbol = document.getElementById('symbol-select')?.value;
    const price = data[symbol] ?? data?.price;
    if (price == null) return;

    const priceEl = document.getElementById('symbol-price');
    const changeEl = document.getElementById('symbol-change');

    if (priceEl) {
        const formatted = price > 1000
            ? price.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : price.toFixed(4);
        priceEl.textContent = `$${formatted}`;
    }

    if (changeEl && data.changePct != null) {
        const pct = data.changePct;
        changeEl.textContent = `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`;
        changeEl.className = `symbol-change ${pct >= 0 ? 'positive' : 'negative'}`;
    }
}

// ── Chart ─────────────────────────────────────────────────────────────────────
function initChart() {
    const container = document.getElementById('chart-container');
    if (!container || !window.ApexCharts) return;

    const options = {
        chart: {
            type: 'candlestick',
            height: '100%',
            background: 'transparent',
            animations: { enabled: false },
            toolbar: { show: false },
            zoom: { enabled: false },
        },
        series: [{ name: 'Price', data: [] }],
        xaxis: {
            type: 'datetime',
            labels: {
                style: { colors: '#64748b', fontSize: '9px', fontFamily: 'JetBrains Mono, monospace' },
                datetimeUTC: false
            },
            axisBorder: { color: 'rgba(255,255,255,0.06)' },
            axisTicks: { color: 'rgba(255,255,255,0.06)' }
        },
        yaxis: {
            labels: {
                style: { colors: '#64748b', fontSize: '9px', fontFamily: 'JetBrains Mono, monospace' },
                formatter: v => v >= 1000 ? `$${(v/1000).toFixed(1)}k` : `$${v.toFixed(2)}`
            },
            opposite: true
        },
        grid: {
            borderColor: 'rgba(255,255,255,0.04)',
            strokeDashArray: 3
        },
        plotOptions: {
            candlestick: {
                colors: { upward: '#22c55e', downward: '#f43f5e' },
                wick: { useFillColor: true }
            }
        },
        tooltip: {
            theme: 'dark',
            style: { fontSize: '10px', fontFamily: 'JetBrains Mono, monospace' },
            x: { format: 'MMM dd HH:mm' }
        },
        theme: { mode: 'dark' }
    };

    chart = new ApexCharts(container, options);
    chart.render();
    document.getElementById('chart-placeholder')?.classList.remove('visible');
}

function handleCandleUpdate(candles) {
    if (!chart || !Array.isArray(candles)) return;
    const series = candles.slice(-MAX_CHART_CANDLES).map(c => ({
        x: new Date(c.timestamp || c.time),
        y: [c.open, c.high, c.low, c.close]
    }));
    chart.updateSeries([{ name: 'Price', data: series }], false);
}

// ── Positions rendering ───────────────────────────────────────────────────────
function renderPositions(positions) {
    const container = document.getElementById('positions-container');
    if (!container) return;

    if (!Array.isArray(positions) || positions.length === 0) {
        container.innerHTML = '<div class="empty-state">No open positions</div>';
        return;
    }

    container.innerHTML = positions.map(p => {
        const pnl = p.unrealisedPnl ?? p.pnl ?? 0;
        const pnlClass = pnl >= 0 ? 'positive' : 'negative';
        const pnlSign = pnl >= 0 ? '+' : '';
        return `
            <div class="position-row">
                <span class="position-symbol">${escapeHtml(p.symbol || '—')}</span>
                <span class="position-side ${p.side?.toLowerCase() || ''}">${escapeHtml(p.side || '—')}</span>
                <span class="position-pnl ${pnlClass}">${pnlSign}$${Math.abs(pnl).toFixed(2)}</span>
            </div>
        `;
    }).join('');
}

// ── Arb opportunities rendering ───────────────────────────────────────────────
const MAX_ARB_ROWS = 3;
let arbOpportunities = [];

function renderArbOpportunity(data) {
    if (!data) return;

    // Keep top-3 by profit
    arbOpportunities = [...arbOpportunities, data]
        .sort((a, b) => (b.profitBps ?? 0) - (a.profitBps ?? 0))
        .slice(0, MAX_ARB_ROWS);

    const container = document.getElementById('arb-container');
    if (!container) return;

    if (arbOpportunities.length === 0) {
        container.innerHTML = '<div class="empty-state">Scanning for opportunities…</div>';
        return;
    }

    container.innerHTML = arbOpportunities.map(op => {
        const profitPct = ((op.profitBps ?? 0) / 100).toFixed(2);
        return `
            <div class="arb-row">
                <span class="arb-path">${escapeHtml(op.path || op.symbol || '—')}</span>
                <div class="arb-meta">
                    <span class="arb-exchange">${escapeHtml(op.exchange || '—')}</span>
                    <span class="arb-profit">+${profitPct}%</span>
                </div>
            </div>
        `;
    }).join('');
}

// ── AI Chat ───────────────────────────────────────────────────────────────────
let currentAssistantEl = null;

function sendChatMessage() {
    const input = document.getElementById('chat-input');
    if (!input || !input.value.trim()) return;

    const text = input.value.trim();
    input.value = '';
    input.style.height = 'auto';

    // Append user message
    appendChatMessage('user', text);

    // Send to background → MLS AI Hub
    port?.postMessage({
        type: 'SEND_ENVELOPE',
        payload: {
            type: 'AI_QUERY',
            unique_id: crypto.randomUUID(),
            module_id: 'chrome-extension',
            timestamp: new Date().toISOString(),
            payload: { query: text, sessionId: getSessionId(), streaming: true }
        }
    });

    // Show typing indicator
    currentAssistantEl = appendChatMessage('assistant', '', true);
}

function appendChatMessage(role, text, typing = false) {
    const container = document.getElementById('chat-messages');
    if (!container) return null;

    const el = document.createElement('div');
    el.className = `chat-message chat-message--${role}${typing ? ' message-typing' : ''}`;
    el.innerHTML = `<div class="message-bubble">${escapeHtml(text)}</div>`;
    container.appendChild(el);
    container.scrollTop = container.scrollHeight;
    return el;
}

function appendAIChunk(data) {
    if (!currentAssistantEl) {
        currentAssistantEl = appendChatMessage('assistant', '', true);
    }
    const bubble = currentAssistantEl.querySelector('.message-bubble');
    if (bubble) bubble.textContent += data.chunk || '';

    const container = document.getElementById('chat-messages');
    if (container) container.scrollTop = container.scrollHeight;
}

function finaliseAIResponse(data) {
    if (currentAssistantEl) {
        currentAssistantEl.classList.remove('message-typing');
        if (data?.text) {
            const bubble = currentAssistantEl.querySelector('.message-bubble');
            if (bubble) bubble.textContent = data.text;
        }
        currentAssistantEl = null;
    }
}

function getSessionId() {
    let id = sessionStorage.getItem('mls-session-id');
    if (!id) {
        id = crypto.randomUUID();
        sessionStorage.setItem('mls-session-id', id);
    }
    return id;
}

// ── Tab switching ─────────────────────────────────────────────────────────────
function initTabs() {
    const buttons = document.querySelectorAll('.tab-btn');
    const panels = document.querySelectorAll('.tab-panel');

    buttons.forEach(btn => {
        btn.addEventListener('click', () => {
            const target = btn.getAttribute('data-tab');

            buttons.forEach(b => {
                b.classList.toggle('tab-btn--active', b === btn);
                b.setAttribute('aria-selected', b === btn ? 'true' : 'false');
            });

            panels.forEach(p => {
                const active = p.id === `panel-${target}`;
                p.classList.toggle('tab-panel--active', active);
                p.setAttribute('aria-hidden', active ? 'false' : 'true');
            });

            // Show/hide symbol bar
            const symbolBar = document.getElementById('symbol-bar');
            if (symbolBar) symbolBar.style.display = target === 'chart' ? '' : 'none';
        });
    });
}

// ── Symbol selector ───────────────────────────────────────────────────────────
function initSymbolSelector() {
    const select = document.getElementById('symbol-select');
    select?.addEventListener('change', () => {
        const symbol = select.value;
        // Request candle data for new symbol
        port?.postMessage({
            type: 'SEND_ENVELOPE',
            payload: {
                type: 'REQUEST_CANDLES',
                unique_id: crypto.randomUUID(),
                module_id: 'chrome-extension',
                timestamp: new Date().toISOString(),
                payload: { symbol, interval: '5m', limit: MAX_CHART_CANDLES }
            }
        });
        // Reset price display
        const priceEl = document.getElementById('symbol-price');
        if (priceEl) priceEl.textContent = '—';
    });
}

// ── Chat input auto-resize + send ─────────────────────────────────────────────
function initChatInput() {
    const input = document.getElementById('chat-input');
    const sendBtn = document.getElementById('chat-send');

    input?.addEventListener('input', () => {
        input.style.height = 'auto';
        input.style.height = `${Math.min(input.scrollHeight, 80)}px`;
    });

    input?.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendChatMessage();
        }
    });

    sendBtn?.addEventListener('click', sendChatMessage);
}

// ── Security: HTML escaping ───────────────────────────────────────────────────
function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    initTabs();
    initSymbolSelector();
    initChatInput();

    // Init chart after a short delay to let ApexCharts load
    setTimeout(initChart, 300);

    connect();
});
