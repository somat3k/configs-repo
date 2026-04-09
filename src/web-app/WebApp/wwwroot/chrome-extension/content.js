/**
 * MLS Chrome Extension — Content Script
 * Injected into: Hyperliquid (app.hyperliquid.xyz) and Camelot (app.camelot.exchange)
 *
 * Features:
 *   - Injects a floating price overlay showing MLS live prices
 *   - Highlights spread differences between MLS and exchange displayed prices
 *   - Shows active MLS positions as a sidebar badge
 */

(function () {
    'use strict';

    if (window.__MLS_CONTENT_INJECTED__) return;
    window.__MLS_CONTENT_INJECTED__ = true;

    // ── State ─────────────────────────────────────────────────────────────────
    let port = null;
    let latestPrices = {};
    let overlayEl = null;
    let reconnectTimer = null;

    // ── Connect to background service worker ──────────────────────────────────
    function connect() {
        try {
            port = chrome.runtime.connect({ name: 'mls-sidebar' });
            port.onMessage.addListener(handleMessage);
            port.onDisconnect.addListener(() => {
                port = null;
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

    function handleMessage(msg) {
        switch (msg.type) {
            case 'PRICE_UPDATE':
                latestPrices = { ...latestPrices, ...flattenPrices(msg.data) };
                updateOverlay();
                highlightPriceDiffs();
                break;
            case 'POSITION_UPDATE':
                updatePositionBadge(msg.data);
                break;
            case 'WS_STATUS':
                updateConnectionIndicator(msg.connected);
                break;
        }
    }

    function flattenPrices(data) {
        if (!data) return {};
        if (typeof data === 'object' && !Array.isArray(data)) {
            return data;
        }
        if (Array.isArray(data)) {
            return data.reduce((acc, item) => {
                if (item.symbol && item.price != null) acc[item.symbol] = item.price;
                return acc;
            }, {});
        }
        return {};
    }

    // ── Overlay element ───────────────────────────────────────────────────────
    function createOverlay() {
        if (document.getElementById('mls-price-overlay')) return;

        overlayEl = document.createElement('div');
        overlayEl.id = 'mls-price-overlay';
        overlayEl.setAttribute('aria-live', 'polite');
        overlayEl.setAttribute('aria-label', 'MLS Live Prices');
        overlayEl.innerHTML = `
            <div class="mls-overlay-header">
                <span class="mls-overlay-logo">◈</span>
                <span class="mls-overlay-title">MLS</span>
                <span class="mls-overlay-dot" id="mls-conn-dot"></span>
                <button class="mls-overlay-collapse" id="mls-overlay-toggle" aria-label="Toggle overlay">−</button>
            </div>
            <div class="mls-overlay-body" id="mls-overlay-body">
                <div class="mls-overlay-loading">Connecting…</div>
            </div>
        `;

        const style = document.createElement('style');
        style.textContent = `
            #mls-price-overlay {
                position: fixed;
                bottom: 80px;
                right: 20px;
                z-index: 2147483647;
                background: rgba(7, 8, 12, 0.96);
                border: 1px solid rgba(0, 212, 255, 0.25);
                border-radius: 12px;
                min-width: 180px;
                max-width: 240px;
                font-family: 'Inter', system-ui, -apple-system, sans-serif;
                font-size: 12px;
                box-shadow: 0 0 24px rgba(0, 212, 255, 0.15), 0 8px 32px rgba(0,0,0,0.6);
                backdrop-filter: blur(16px);
                color: #e2e8f0;
                user-select: none;
                transition: opacity 0.2s;
            }
            #mls-price-overlay.mls-collapsed .mls-overlay-body { display: none; }
            .mls-overlay-header {
                display: flex;
                align-items: center;
                gap: 6px;
                padding: 8px 12px;
                border-bottom: 1px solid rgba(255,255,255,0.05);
                cursor: move;
            }
            .mls-overlay-logo { color: #00d4ff; font-size: 14px; }
            .mls-overlay-title {
                font-weight: 700;
                font-size: 11px;
                letter-spacing: 0.08em;
                background: linear-gradient(135deg, #00d4ff, #7c3aed);
                -webkit-background-clip: text;
                -webkit-text-fill-color: transparent;
                background-clip: text;
            }
            .mls-overlay-dot {
                width: 5px; height: 5px;
                border-radius: 50%;
                background: #64748b;
                transition: background 0.3s;
            }
            .mls-overlay-dot.online { background: #22c55e; box-shadow: 0 0 6px #22c55e; }
            .mls-overlay-collapse {
                margin-left: auto;
                background: none;
                border: none;
                color: #64748b;
                cursor: pointer;
                font-size: 14px;
                line-height: 1;
                padding: 0 2px;
                font-family: inherit;
            }
            .mls-overlay-collapse:hover { color: #e2e8f0; }
            .mls-overlay-body { padding: 8px 12px 10px; }
            .mls-overlay-loading { color: #64748b; font-size: 11px; }
            .mls-price-row {
                display: flex;
                justify-content: space-between;
                align-items: center;
                gap: 8px;
                padding: 3px 0;
                border-bottom: 1px solid rgba(255,255,255,0.03);
            }
            .mls-price-row:last-child { border-bottom: none; }
            .mls-price-symbol {
                color: #94a3b8;
                font-size: 10px;
                font-weight: 600;
                letter-spacing: 0.05em;
                font-family: 'JetBrains Mono', monospace;
            }
            .mls-price-value {
                color: #e2e8f0;
                font-size: 11px;
                font-weight: 600;
                font-family: 'JetBrains Mono', monospace;
                font-variant-numeric: tabular-nums;
            }
            .mls-price-diff {
                font-size: 9px;
                font-family: 'JetBrains Mono', monospace;
                padding: 1px 4px;
                border-radius: 3px;
                font-weight: 700;
            }
            .mls-diff-pos { color: #22c55e; background: rgba(34,197,94,0.1); }
            .mls-diff-neg { color: #f43f5e; background: rgba(244,63,94,0.1); }
            .mls-highlight-diff {
                outline: 2px solid rgba(0, 212, 255, 0.4) !important;
                outline-offset: 2px;
                border-radius: 2px;
            }
        `;

        document.head.appendChild(style);
        document.body.appendChild(overlayEl);

        // Toggle collapse
        document.getElementById('mls-overlay-toggle')?.addEventListener('click', () => {
            overlayEl.classList.toggle('mls-collapsed');
            const btn = document.getElementById('mls-overlay-toggle');
            if (btn) btn.textContent = overlayEl.classList.contains('mls-collapsed') ? '+' : '−';
        });

        // Drag to move
        makeDraggable(overlayEl, overlayEl.querySelector('.mls-overlay-header'));
    }

    function updateOverlay() {
        if (!overlayEl) return;
        const body = document.getElementById('mls-overlay-body');
        if (!body) return;

        const watchedSymbols = ['BTC-PERP', 'ETH-PERP', 'ARB-PERP', 'BTC', 'ETH', 'ARB'];
        const rows = watchedSymbols
            .filter(s => latestPrices[s] != null)
            .map(s => {
                const price = latestPrices[s];
                const formatted = price > 1000
                    ? price.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                    : price.toFixed(4);
                return `
                    <div class="mls-price-row">
                        <span class="mls-price-symbol">${s}</span>
                        <span class="mls-price-value">$${formatted}</span>
                    </div>
                `;
            })
            .join('');

        body.innerHTML = rows || '<div class="mls-overlay-loading">Waiting for data…</div>';
    }

    function updateConnectionIndicator(connected) {
        const dot = document.getElementById('mls-conn-dot');
        if (dot) dot.classList.toggle('online', connected);
    }

    function updatePositionBadge(positions) {
        if (!Array.isArray(positions) || positions.length === 0) return;
        // Future: inject a small positions badge near the exchange's own position panel
    }

    // ── Price diff highlighting on exchange pages ─────────────────────────────
    function highlightPriceDiffs() {
        // Find price elements on the exchange page and compare with MLS prices
        // Uses data attributes added by previous runs to avoid re-processing
        const priceEls = document.querySelectorAll('[data-mls-symbol]');
        priceEls.forEach(el => {
            const symbol = el.getAttribute('data-mls-symbol');
            const exchangePrice = parseFloat(el.textContent.replace(/[,$]/g, ''));
            const mlsPrice = latestPrices[symbol];
            if (mlsPrice == null || isNaN(exchangePrice)) return;

            const diffPct = ((exchangePrice - mlsPrice) / mlsPrice) * 100;
            el.classList.toggle('mls-highlight-diff', Math.abs(diffPct) > 0.1);
        });
    }

    // ── Exchange-specific DOM injection ───────────────────────────────────────
    function injectExchangeAnnotations() {
        const host = location.hostname;

        if (host.includes('hyperliquid')) {
            injectHyperliquidAnnotations();
        } else if (host.includes('camelot')) {
            injectCamelotAnnotations();
        }
    }

    function injectHyperliquidAnnotations() {
        // Tag Hyperliquid price elements so highlightPriceDiffs can find them
        // These selectors target the Hyperliquid React app's price display elements
        const attempts = [
            { selector: '[class*="markPrice"]', symbol: 'BTC-PERP' },
            { selector: '[class*="lastPrice"]', symbol: 'BTC-PERP' },
        ];

        attempts.forEach(({ selector, symbol }) => {
            document.querySelectorAll(selector).forEach(el => {
                if (!el.hasAttribute('data-mls-symbol')) {
                    el.setAttribute('data-mls-symbol', symbol);
                }
            });
        });
    }

    function injectCamelotAnnotations() {
        // Tag Camelot price elements
        const candidates = document.querySelectorAll('[class*="price"]:not([data-mls-symbol])');
        candidates.forEach(el => {
            const text = el.textContent.trim();
            if (/^\$[\d,]+(\.\d+)?$/.test(text)) {
                const val = parseFloat(text.replace(/[$,]/g, ''));
                // Heuristic: assign symbol based on approximate price range
                if (val > 50000) el.setAttribute('data-mls-symbol', 'BTC');
                else if (val > 2000) el.setAttribute('data-mls-symbol', 'ETH');
            }
        });
    }

    // ── Draggable helper ──────────────────────────────────────────────────────
    function makeDraggable(el, handle) {
        let startX, startY, origLeft, origBottom;
        handle.addEventListener('mousedown', e => {
            startX = e.clientX;
            startY = e.clientY;
            const rect = el.getBoundingClientRect();
            origLeft = rect.left;
            origBottom = window.innerHeight - rect.bottom;
            e.preventDefault();

            const onMove = e => {
                const dx = e.clientX - startX;
                const dy = e.clientY - startY;
                el.style.left = `${Math.max(0, origLeft + dx)}px`;
                el.style.bottom = `${Math.max(0, origBottom - dy)}px`;
                el.style.right = 'auto';
            };
            const onUp = () => {
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
            };
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        });
    }

    // ── MutationObserver: re-annotate after React re-renders (debounced) ──────
    let annotationFrame = null;
    const observer = new MutationObserver(() => {
        if (annotationFrame !== null) return; // already scheduled
        annotationFrame = requestAnimationFrame(() => {
            annotationFrame = null;
            injectExchangeAnnotations();
        });
    });

    // ── Initialise ────────────────────────────────────────────────────────────
    function init() {
        createOverlay();
        connect();

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        // Initial annotation attempt
        injectExchangeAnnotations();

        // Poll for price updates from storage (fallback if port messaging lags)
        setInterval(async () => {
            try {
                const stored = await chrome.storage.session.get('latestPrices');
                if (stored.latestPrices) {
                    latestPrices = { ...latestPrices, ...stored.latestPrices };
                    updateOverlay();
                }
            } catch { /* extension context may have been invalidated */ }
        }, 2000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
