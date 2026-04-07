/**
 * MLS Chrome Extension — Background Service Worker (MV3)
 *
 * Responsibilities:
 *   1. Open the MLS Side Panel on extension icon click
 *   2. Maintain a persistent WebSocket connection to MLS block-controller
 *   3. Fan-out live data to the Side Panel and Popup via chrome.runtime.connect ports
 *   4. Reconnect automatically after Chrome restart or WS disconnect
 *   5. Queue outbound orders when WS is disconnected; replay on reconnect
 */

// ── Configuration (override via chrome.storage.sync) ─────────────────────────
const DEFAULT_CONFIG = {
    wsUrl: 'ws://localhost:6100/hubs/block-controller',
    clientId: null, // assigned on first run
    reconnectDelayMs: 2000,
    maxReconnectDelayMs: 30000,
    heartbeatIntervalMs: 5000,
    dataTopics: ['prices', 'positions', 'arb-opportunities', 'ai-responses']
};

// ── State ─────────────────────────────────────────────────────────────────────
let ws = null;
let wsConfig = { ...DEFAULT_CONFIG };
let reconnectDelay = DEFAULT_CONFIG.reconnectDelayMs;
let heartbeatTimer = null;
let reconnectTimer = null;
let pendingOrders = [];

/** Connected ports: side-panel and popup each connect via chrome.runtime.connect */
const connectedPorts = new Set();

// ── Extension lifecycle ───────────────────────────────────────────────────────
chrome.runtime.onInstalled.addListener(async ({ reason }) => {
    if (reason === 'install') {
        const clientId = crypto.randomUUID();
        await chrome.storage.sync.set({ clientId });
        wsConfig.clientId = clientId;
    }
    await initConfig();
    connectWebSocket();
});

chrome.runtime.onStartup.addListener(async () => {
    await initConfig();
    connectWebSocket();
});

// ── Side Panel: open on action click ─────────────────────────────────────────
chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});

// ── Port management (Side Panel + Popup) ─────────────────────────────────────
chrome.runtime.onConnect.addListener(port => {
    if (!['mls-sidebar', 'mls-popup'].includes(port.name)) return;

    connectedPorts.add(port);
    broadcastToPort(port, { type: 'CONNECTED', status: ws?.readyState === WebSocket.OPEN ? 'online' : 'offline' });

    port.onMessage.addListener(msg => handlePortMessage(port, msg));
    port.onDisconnect.addListener(() => connectedPorts.delete(port));
});

function handlePortMessage(port, msg) {
    switch (msg.type) {
        case 'SUBSCRIBE':
            // Already subscribed globally; just acknowledge
            broadcastToPort(port, { type: 'SUBSCRIBED', topics: wsConfig.dataTopics });
            break;
        case 'SEND_ENVELOPE':
            sendEnvelope(msg.payload);
            break;
        case 'PLACE_ORDER':
            queueOrSendOrder(msg.payload);
            break;
        case 'GET_STATUS':
            broadcastToPort(port, { type: 'STATUS', connected: ws?.readyState === WebSocket.OPEN });
            break;
    }
}

// ── WebSocket connection ───────────────────────────────────────────────────────
async function initConfig() {
    const stored = await chrome.storage.sync.get(null);
    wsConfig = { ...DEFAULT_CONFIG, ...stored };
    if (!wsConfig.clientId) {
        wsConfig.clientId = crypto.randomUUID();
        await chrome.storage.sync.set({ clientId: wsConfig.clientId });
    }
}

function connectWebSocket() {
    if (ws && ws.readyState === WebSocket.CONNECTING) return;

    clearTimeout(reconnectTimer);
    clearInterval(heartbeatTimer);

    const url = `${wsConfig.wsUrl}?clientId=${wsConfig.clientId}`;

    try {
        ws = new WebSocket(url);
    } catch (err) {
        console.warn('[MLS BG] WS construction error:', err);
        scheduleReconnect();
        return;
    }

    ws.onopen = () => {
        console.log('[MLS BG] WebSocket connected to MLS');
        reconnectDelay = DEFAULT_CONFIG.reconnectDelayMs;

        // Subscribe to data topics
        wsConfig.dataTopics.forEach(topic => {
            sendEnvelope(buildEnvelope('SUBSCRIBE_TOPIC', { topic }));
        });

        // Start heartbeat
        heartbeatTimer = setInterval(() => {
            if (ws?.readyState === WebSocket.OPEN) {
                sendEnvelope(buildEnvelope('HEARTBEAT', {}));
            }
        }, wsConfig.heartbeatIntervalMs);

        // Replay any queued orders
        replayOrderQueue();

        broadcastToAll({ type: 'WS_STATUS', connected: true });
    };

    ws.onmessage = event => {
        let envelope;
        try {
            envelope = JSON.parse(event.data);
        } catch {
            return;
        }
        routeEnvelope(envelope);
    };

    ws.onerror = err => {
        console.warn('[MLS BG] WS error:', err);
    };

    ws.onclose = event => {
        console.log(`[MLS BG] WS closed (code=${event.code})`);
        clearInterval(heartbeatTimer);
        broadcastToAll({ type: 'WS_STATUS', connected: false });
        scheduleReconnect();
    };
}

function scheduleReconnect() {
    reconnectTimer = setTimeout(() => {
        reconnectDelay = Math.min(reconnectDelay * 1.5, DEFAULT_CONFIG.maxReconnectDelayMs);
        connectWebSocket();
    }, reconnectDelay);
}

function sendEnvelope(envelope) {
    if (ws?.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(envelope));
    }
}

function buildEnvelope(type, payload) {
    return {
        type,
        version: '1.0',
        unique_id: crypto.randomUUID(),
        module_id: 'chrome-extension',
        module_network_address: 'chrome-extension',
        module_network_port: 0,
        timestamp: new Date().toISOString(),
        payload
    };
}

// ── Envelope routing ──────────────────────────────────────────────────────────
function routeEnvelope(envelope) {
    const { type, payload } = envelope;

    switch (type) {
        case 'PRICE_UPDATE':
            broadcastToAll({ type: 'PRICE_UPDATE', data: payload });
            cacheLatestPrice(payload);
            break;
        case 'POSITION_UPDATE':
            broadcastToAll({ type: 'POSITION_UPDATE', data: payload });
            break;
        case 'ARB_OPPORTUNITY':
            broadcastToAll({ type: 'ARB_OPPORTUNITY', data: payload });
            notifyArbitrageOpportunity(payload);
            break;
        case 'AI_RESPONSE_CHUNK':
            broadcastToAll({ type: 'AI_RESPONSE_CHUNK', data: payload });
            break;
        case 'AI_RESPONSE_COMPLETE':
            broadcastToAll({ type: 'AI_RESPONSE_COMPLETE', data: payload });
            break;
        case 'ORDER_CONFIRMED':
            broadcastToAll({ type: 'ORDER_CONFIRMED', data: payload });
            removeFromOrderQueue(payload.orderId);
            break;
        default:
            broadcastToAll({ type: 'ENVELOPE', envelope });
    }
}

// ── Price cache (used by content script via storage) ─────────────────────────
function cacheLatestPrice(priceData) {
    chrome.storage.session.set({ latestPrices: priceData }).catch(() => {});
}

// ── Arbitrage notifications ───────────────────────────────────────────────────
function notifyArbitrageOpportunity(data) {
    if (!data?.profitBps || data.profitBps < 50) return; // > 0.5% only
    chrome.notifications.create({
        type: 'basic',
        iconUrl: 'icons/icon-48.svg',
        title: `MLS Arb: ${data.path || 'Opportunity'}`,
        message: `Profit: ${(data.profitBps / 100).toFixed(2)}% | ${data.exchange || ''}`,
        priority: 1
    });
}

// ── Order queue (offline resilience) ─────────────────────────────────────────
function queueOrSendOrder(order) {
    if (ws?.readyState === WebSocket.OPEN) {
        sendEnvelope(buildEnvelope('PLACE_ORDER', order));
    } else {
        order._queuedAt = Date.now();
        order._id = crypto.randomUUID();
        pendingOrders.push(order);
        chrome.storage.local.set({ pendingOrders }).catch(() => {});
        broadcastToAll({ type: 'ORDER_QUEUED', orderId: order._id });
    }
}

async function replayOrderQueue() {
    const stored = await chrome.storage.local.get('pendingOrders');
    const queue = stored.pendingOrders || [];
    pendingOrders = [...queue];

    for (const order of [...pendingOrders]) {
        if (ws?.readyState === WebSocket.OPEN) {
            sendEnvelope(buildEnvelope('PLACE_ORDER', order));
        }
    }
}

function removeFromOrderQueue(orderId) {
    pendingOrders = pendingOrders.filter(o => o._id !== orderId);
    chrome.storage.local.set({ pendingOrders }).catch(() => {});
}

// ── Port broadcast helpers ────────────────────────────────────────────────────
function broadcastToAll(message) {
    for (const port of connectedPorts) {
        broadcastToPort(port, message);
    }
}

function broadcastToPort(port, message) {
    try {
        port.postMessage(message);
    } catch {
        connectedPorts.delete(port);
    }
}

// ── Alarm: ensure WS stays connected even after service worker idle ───────────
chrome.alarms.create('mls-keepalive', { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener(alarm => {
    if (alarm.name === 'mls-keepalive') {
        if (!ws || ws.readyState === WebSocket.CLOSED || ws.readyState === WebSocket.CLOSING) {
            connectWebSocket();
        }
    }
});

// ── Initial connection on first load ─────────────────────────────────────────
initConfig().then(connectWebSocket);
