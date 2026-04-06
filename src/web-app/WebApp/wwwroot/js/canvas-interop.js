/**
 * canvas-interop.js
 * MLS Trading Platform — MDI canvas and DesignerCanvas JavaScript interop.
 *
 * Provides:
 *   - mlsCanvas.*      : DesignerCanvas pan/zoom helpers
 *   - mlsMDI.*         : MDI window drag/resize helpers (pointer-event bridging)
 *   - Hammer.js touch  : pinch-to-zoom and pan gestures for Android Chrome
 *   - Chart helpers    : targeted DOM updates without Blazor re-render
 *   - localStorage     : layout persistence helpers called from WindowLayoutService
 */
(function () {
    'use strict';

    // ── Chart initialization ─────────────────────────────────────────────────

    window.mlsChart = {
        /**
         * Initialize an ApexCharts candlestick chart in a container element.
         * @param {string} containerId - element id
         * @param {string} symbol      - trading symbol label
         */
        initCandleChart(containerId, symbol) {
            const container = document.getElementById(containerId);
            if (!container || typeof ApexCharts === 'undefined') return;

            const options = {
                series: [{ name: symbol, type: 'candlestick', data: [] }],
                chart: {
                    id: containerId,
                    type: 'candlestick',
                    height: '100%',
                    background: '#0d1117',
                    foreColor: '#8b949e',
                    animations: { enabled: false },
                    toolbar: { show: true, tools: { zoom: true, pan: true, reset: true } },
                },
                plotOptions: {
                    candlestick: {
                        colors: { upward: '#2ea043', downward: '#f85149' },
                        wick: { useFillColor: true },
                    },
                },
                xaxis: { type: 'datetime', labels: { datetimeUTC: false } },
                yaxis: { labels: { formatter: v => v.toFixed(4) } },
                grid: { borderColor: 'rgba(255,255,255,0.05)' },
                theme: { mode: 'dark' },
                tooltip: { theme: 'dark' },
            };

            const chart = new ApexCharts(container, options);
            chart.render();
            // Store reference for pushCandleToChart / addTradeAnnotation
            const wrapper = container.closest('.trading-chart');
            if (wrapper) wrapper._apexChart = chart;
            container._apexChart = chart;
        },
    };

    // ── DesignerCanvas helpers ───────────────────────────────────────────────

    window.mlsCanvas = {
        /**
         * Register HammerJS gesture recognizers on the canvas element.
         * @param {string} canvasId  - element id or CSS selector
         * @param {object} dotnetRef - DotNetObjectReference for Blazor callbacks
         */
        initGestures(canvasId, dotnetRef) {
            const el = document.querySelector(canvasId);
            if (!el || typeof Hammer === 'undefined') return;

            const mc = new Hammer.Manager(el, { touchAction: 'none' });

            // Pinch-to-zoom
            const pinch = new Hammer.Pinch({ threshold: 0 });
            mc.add(pinch);

            let lastScale = 1;
            mc.on('pinchstart', () => { lastScale = 1; });
            mc.on('pinch', (ev) => {
                const delta = ev.scale - lastScale;
                lastScale = ev.scale;
                dotnetRef.invokeMethodAsync('OnPinchZoom', delta);
            });

            // Two-finger pan
            const pan = new Hammer.Pan({ pointers: 2, threshold: 5, direction: Hammer.DIRECTION_ALL });
            mc.add(pan);

            let lastPanX = 0, lastPanY = 0;
            mc.on('panstart', () => { lastPanX = 0; lastPanY = 0; });
            mc.on('pan', (ev) => {
                const dx = ev.deltaX - lastPanX;
                const dy = ev.deltaY - lastPanY;
                lastPanX = ev.deltaX;
                lastPanY = ev.deltaY;
                dotnetRef.invokeMethodAsync('OnTouchPan', dx, dy);
            });

            el._mlsHammer = mc;
        },

        destroyGestures(canvasId) {
            const el = document.querySelector(canvasId);
            if (el && el._mlsHammer) {
                el._mlsHammer.destroy();
                delete el._mlsHammer;
            }
        },

        /** Fit canvas content to view by resetting transform. */
        fitToView() {
            const layer = document.querySelector('.blocks-layer');
            if (layer) {
                layer.style.transform = 'translate(20px, 20px) scale(1)';
            }
        },
    };

    // ── Chart update helpers (targeted DOM, no Blazor re-render) ────────────

    /**
     * Push a new data point to an ApexCharts instance.
     * @param {string} chartId   - id attribute of chart container
     * @param {string} seriesName
     * @param {number} epoch     - Unix ms timestamp
     * @param {number} value
     */
    window.updateApexSeries = function (chartId, seriesName, epoch, value) {
        const el = document.getElementById(chartId);
        if (!el || !el._apexChart) return;
        el._apexChart.appendData([{ data: [[epoch, value]] }]);
    };

    /**
     * Append an OHLCV candle to a candlestick ApexCharts instance.
     * @param {string} chartId
     * @param {{ t:number, o:number, h:number, l:number, c:number }} candle
     */
    window.pushCandleToChart = function (chartId, candle) {
        const el = document.getElementById(chartId);
        if (!el || !el._apexChart) return;
        el._apexChart.appendData([{
            data: [{ x: new Date(candle.t), y: [candle.o, candle.h, candle.l, candle.c] }],
        }]);
    };

    /**
     * Add a buy/sell/signal annotation to a chart.
     * @param {string} chartId
     * @param {{ time:number, label:string, color:string }} signal
     */
    window.addTradeAnnotation = function (chartId, signal) {
        const el = document.getElementById(chartId);
        if (!el || !el._apexChart) return;
        el._apexChart.addPointAnnotation({
            x: signal.time,
            label: {
                text: signal.label,
                style: { background: signal.color || '#00d4ff', color: '#fff', fontSize: '11px' },
            },
            marker: { size: 6, fillColor: signal.color || '#00d4ff' },
        });
    };

    /**
     * Update a single position row in the PositionsGrid without re-render.
     * @param {string} symbol
     * @param {{ pnl:number, price:number, size:number }} data
     */
    window.updatePositionRow = function (symbol, data) {
        const row = document.querySelector(`[data-symbol="${CSS.escape(symbol)}"]`);
        if (!row) return;

        const pnlEl = row.querySelector('[data-field="pnl"]');
        if (pnlEl) {
            pnlEl.textContent = data.pnl >= 0
                ? `+${data.pnl.toFixed(2)}`
                : data.pnl.toFixed(2);
            pnlEl.className = 'pnl-value ' + (data.pnl >= 0 ? 'positive' : 'negative');
            flashElement(pnlEl);
        }

        const priceEl = row.querySelector('[data-field="price"]');
        if (priceEl) {
            priceEl.textContent = data.price.toFixed(4);
            flashElement(priceEl);
        }
    };

    /**
     * Update the order book bids and asks.
     * @param {string} symbol
     * @param {Array<[number,number]>} bids  - [[price, qty], ...]
     * @param {Array<[number,number]>} asks
     */
    window.updateOrderBook = function (symbol, bids, asks) {
        const container = document.getElementById(`orderbook-${symbol}`);
        if (!container) return;

        const bidsEl = container.querySelector('.ob-bids');
        const asksEl = container.querySelector('.ob-asks');
        if (!bidsEl || !asksEl) return;

        bidsEl.innerHTML = bids.slice(0, 12).map(([p, q]) =>
            `<div class="ob-row bid"><span class="ob-price">${p.toFixed(4)}</span><span class="ob-qty">${q.toFixed(3)}</span></div>`
        ).join('');

        asksEl.innerHTML = asks.slice(0, 12).map(([p, q]) =>
            `<div class="ob-row ask"><span class="ob-price">${p.toFixed(4)}</span><span class="ob-qty">${q.toFixed(3)}</span></div>`
        ).join('');
    };

    /**
     * Animate the DeFi health factor gauge.
     * @param {string} gaugeId  - element id
     * @param {number} value    - health factor value
     */
    window.updateHealthFactor = function (gaugeId, value) {
        const el = document.getElementById(gaugeId);
        if (!el) return;

        const valueEl = el.querySelector('.hf-value');
        if (valueEl) {
            valueEl.textContent = value.toFixed(3);
            valueEl.className = 'hf-value ' + getHealthClass(value);
            flashElement(valueEl);
        }

        // SVG arc gauge update
        const arc = el.querySelector('.hf-arc');
        if (arc) {
            const pct = Math.min(1, value / 3);
            const circumference = 2 * Math.PI * 44;
            arc.style.strokeDashoffset = circumference * (1 - pct);
            arc.style.stroke = getHealthColor(value);
        }

        // Critical alert animation
        if (value < 1.2) {
            el.classList.add('hf-critical');
            el.classList.remove('hf-warning', 'hf-healthy');
        } else if (value < 1.5) {
            el.classList.add('hf-warning');
            el.classList.remove('hf-critical', 'hf-healthy');
        } else {
            el.classList.add('hf-healthy');
            el.classList.remove('hf-critical', 'hf-warning');
        }
    };

    /**
     * Render or re-render a Mermaid diagram inside a container element.
     * @param {string} containerId
     * @param {string} source  - Mermaid diagram source
     */
    window.renderMermaidDiagram = function (containerId, source) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (typeof mermaid === 'undefined') return;
        el.innerHTML = source;
        el.removeAttribute('data-processed');
        mermaid.run({ nodes: [el] });
    };

    /**
     * Initialize Cytoscape.js for path/topology visualization.
     * @param {string} containerId
     * @param {{ nodes: any[], edges: any[] }} graphData
     */
    window.initCytoscapeGraph = function (containerId, graphData) {
        const el = document.getElementById(containerId);
        if (!el || typeof cytoscape === 'undefined') return;

        if (el._cy) { el._cy.destroy(); }

        el._cy = cytoscape({
            container: el,
            elements: graphData,
            style: [
                {
                    selector: 'node',
                    style: {
                        'background-color': '#1c2128',
                        'border-color': '#00d4ff',
                        'border-width': 1.5,
                        'color': '#e6edf3',
                        'label': 'data(label)',
                        'font-size': '11px',
                        'text-valign': 'center',
                        'text-halign': 'center',
                    },
                },
                {
                    selector: 'edge',
                    style: {
                        'width': 2,
                        'line-color': '#22c55e',
                        'target-arrow-color': '#22c55e',
                        'target-arrow-shape': 'triangle',
                        'curve-style': 'bezier',
                        'label': 'data(weight)',
                        'font-size': '10px',
                        'color': '#8b949e',
                    },
                },
            ],
            layout: { name: 'cose', animate: true },
        });

        return el._cy;
    };

    /**
     * Update Cytoscape graph data in place.
     * @param {string} containerId
     * @param {{ nodes: any[], edges: any[] }} graphData
     */
    window.updateCytoscapeGraph = function (containerId, graphData) {
        const el = document.getElementById(containerId);
        if (!el || !el._cy) {
            window.initCytoscapeGraph(containerId, graphData);
            return;
        }
        el._cy.json({ elements: graphData });
        el._cy.layout({ name: 'cose', animate: true }).run();
    };

    // ── Utilities ────────────────────────────────────────────────────────────

    function flashElement(el) {
        el.classList.remove('value-flash');
        // Force reflow
        void el.offsetWidth;
        el.classList.add('value-flash');
    }

    function getHealthClass(hf) {
        if (hf < 1.2) return 'critical';
        if (hf < 1.5) return 'warning';
        return 'healthy';
    }

    function getHealthColor(hf) {
        if (hf < 1.2) return '#f85149';
        if (hf < 1.5) return '#f0883e';
        return '#2ea043';
    }

    // ── Global CSS for JS-driven animations ─────────────────────────────────

    const style = document.createElement('style');
    style.textContent = `
        @keyframes value-flash {
            0%   { background-color: rgba(0, 212, 255, 0.25); }
            100% { background-color: transparent; }
        }
        .value-flash {
            animation: value-flash 500ms ease-out;
        }
        .pnl-value.positive { color: #2ea043; font-family: 'JetBrains Mono', monospace; }
        .pnl-value.negative { color: #f85149; font-family: 'JetBrains Mono', monospace; }

        @keyframes hf-pulse-critical {
            0%, 100% { box-shadow: 0 0 0 0 rgba(248,81,73,0.5); }
            50%       { box-shadow: 0 0 0 8px rgba(248,81,73,0); }
        }
        .hf-critical { animation: hf-pulse-critical 1.2s ease-in-out infinite; }

        @keyframes hf-pulse-warning {
            0%, 100% { box-shadow: 0 0 0 0 rgba(240,136,62,0.4); }
            50%       { box-shadow: 0 0 0 6px rgba(240,136,62,0); }
        }
        .hf-warning { animation: hf-pulse-warning 2s ease-in-out infinite; }
    `;
    document.head.appendChild(style);

})();
