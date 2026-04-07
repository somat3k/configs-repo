/**
 * MLS Chrome Extension — Background Service Worker (MV3)
 *
 * Responsibilities:
 *   1. Open the MLS Side Panel on extension icon click
 *   2. Maintain a persistent SignalR connection to MLS block-controller
 *   3. Fan-out live data to the Side Panel and Popup via chrome.runtime.connect ports
 *   4. Reconnect automatically after Chrome restart or hub disconnect
 *   5. Queue outbound orders when disconnected; replay on reconnect
 *
 * Dependencies:
 *   lib/signalr.esm.js — local ESM build of @microsoft/signalr.
 *   Run: cd chrome-extension && npm install && npm run build
 */

import * as signalR from './lib/signalr.esm.js';

// ── Configuration (override via chrome.storage.sync) ─────────────────────────
const DEFAULT_CONFIG = {
    hubUrl: 'http://localhost:6100/hubs/block-controller',
    clientId: null, // assigned on first run
    reconnectDelayMs: 2000,
    maxReconnectDelayMs: 30000,
    heartbeatIntervalMs: 5000,
    dataTopics: ['prices', 'positions', 'arb-opportunities', 'ai-responses'],
    arbNotificationThresholdBps: 50  // notify on arb opportunities > 0.5% profit
};

// ── State ─────────────────────────────────────────────────────────────────────
let hubConnection = null;
let wsConfig = { ...DEFAULT_CONFIG };
let reconnectDelay = DEFAULT_CONFIG.reconnectDelayMs;
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
    connectSignalR();
});

chrome.runtime.onStartup.addListener(async () => {
    await initConfig();
    connectSignalR();
});

// ── Side Panel: open on action click ─────────────────────────────────────────
chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});

// ── Port management (Side Panel + Popup) ─────────────────────────────────────
chrome.runtime.onConnect.addListener(port => {
    if (!['mls-sidebar', 'mls-popup'].includes(port.name)) return;

    connectedPorts.add(port);
    broadcastToPort(port, {
        type: 'CONNECTED',
        status: hubConnection?.state === signalR.HubConnectionState.Connected ? 'online' : 'offline'
    });

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
            // Validate required EnvelopePayload fields before forwarding
            if (isValidEnvelope(msg.payload)) {
                sendEnvelope(msg.payload);
            } else {
                broadcastToPort(port, { type: 'ERROR', message: 'Invalid envelope: missing required fields (type, version, session_id, module_id, timestamp, payload)' });
            }
            break;
        case 'PLACE_ORDER':
            queueOrSendOrder(msg.payload);
            break;
        case 'GET_STATUS':
            broadcastToPort(port, {
                type: 'STATUS',
                connected: hubConnection?.state === signalR.HubConnectionState.Connected
            });
            break;
    }
}

/** Validates that an object has all required EnvelopePayload fields. */
function isValidEnvelope(env) {
    if (!env || typeof env !== 'object') return false;
    return (
        typeof env.type === 'string' && env.type.length > 0 &&
        typeof env.version === 'number' && env.version >= 1 &&
        typeof env.session_id === 'string' && env.session_id.length > 0 &&
        typeof env.module_id === 'string' && env.module_id.length > 0 &&
        typeof env.timestamp === 'string' && env.timestamp.length > 0 &&
        env.payload !== null && env.payload !== undefined
    );
}

// ── SignalR connection ─────────────────────────────────────────────────────────
async function initConfig() {
    const stored = await chrome.storage.sync.get(null);
    wsConfig = { ...DEFAULT_CONFIG, ...stored };
    if (!wsConfig.clientId) {
        wsConfig.clientId = crypto.randomUUID();
        await chrome.storage.sync.set({ clientId: wsConfig.clientId });
    }
}

async function connectSignalR() {
    if (hubConnection?.state === signalR.HubConnectionState.Connected ||
        hubConnection?.state === signalR.HubConnectionState.Connecting ||
        hubConnection?.state === signalR.HubConnectionState.Reconnecting) {
        return;
    }

    clearTimeout(reconnectTimer);

    const url = `${wsConfig.hubUrl}?clientId=${wsConfig.clientId}`;

    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: () => {
                reconnectDelay = Math.min(reconnectDelay * 1.5, wsConfig.maxReconnectDelayMs);
                return reconnectDelay;
            }
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // ── Receive envelopes from the hub ────────────────────────────────────────
    hubConnection.on('ReceiveEnvelope', envelope => routeEnvelope(envelope));

    hubConnection.onreconnected(() => {
        console.log('[MLS BG] SignalR reconnected');
        reconnectDelay = DEFAULT_CONFIG.reconnectDelayMs;
        subscribeToTopics();
        replayOrderQueue();
        broadcastToAll({ type: 'WS_STATUS', connected: true });
    });

    hubConnection.onreconnecting(() => {
        broadcastToAll({ type: 'WS_STATUS', connected: false });
    });

    hubConnection.onclose(() => {
        console.log('[MLS BG] SignalR connection closed');
        broadcastToAll({ type: 'WS_STATUS', connected: false });
        scheduleReconnect();
    });

    try {
        await hubConnection.start();
        console.log('[MLS BG] SignalR connected to MLS block-controller');
        reconnectDelay = DEFAULT_CONFIG.reconnectDelayMs;
        await subscribeToTopics();
        replayOrderQueue();
        broadcastToAll({ type: 'WS_STATUS', connected: true });
    } catch (err) {
        console.warn('[MLS BG] SignalR start failed:', err);
        scheduleReconnect();
    }
}

async function subscribeToTopics() {
    for (const topic of wsConfig.dataTopics) {
        try {
            await hubConnection.invoke('SubscribeToTopicAsync', topic);
        } catch (err) {
            console.warn('[MLS BG] Topic subscribe failed for', topic, err);
        }
    }
}

function scheduleReconnect() {
    reconnectTimer = setTimeout(() => {
        reconnectDelay = Math.min(reconnectDelay * 1.5, DEFAULT_CONFIG.maxReconnectDelayMs);
        connectSignalR();
    }, reconnectDelay);
}

async function sendEnvelope(envelope) {
    if (hubConnection?.state === signalR.HubConnectionState.Connected) {
        try {
            await hubConnection.invoke('SendEnvelope', envelope);
        } catch (err) {
            console.warn('[MLS BG] SendEnvelope failed:', err);
        }
    }
}

function buildEnvelope(type, payload) {
    return {
        type,
        version: 1,          // int, must be ≥ 1  (EnvelopePayload.Version)
        session_id: crypto.randomUUID(),
        module_id: `chrome-extension:${wsConfig.clientId || 'unknown'}`,
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
    if (!data?.profitBps || data.profitBps < wsConfig.arbNotificationThresholdBps) return;
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
    if (hubConnection?.state === signalR.HubConnectionState.Connected) {
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
        if (hubConnection?.state === signalR.HubConnectionState.Connected) {
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

// ── Alarm: ensure SignalR stays connected even after service worker idle ──────
chrome.alarms.create('mls-keepalive', { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener(alarm => {
    if (alarm.name === 'mls-keepalive') {
        if (!hubConnection ||
            hubConnection.state === signalR.HubConnectionState.Disconnected) {
            connectSignalR();
        }
    }
});

// ── Initial connection on first load ─────────────────────────────────────────
initConfig().then(connectSignalR);
