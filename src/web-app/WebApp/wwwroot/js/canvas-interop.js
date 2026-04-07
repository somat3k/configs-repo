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
     * Push a new data point to an ApexCharts instance by series name.
     * When the element was initialised by initTrainCharts (i.e. it has _seriesData),
     * the named series is updated and the full chart is refreshed.
     * Otherwise falls back to appendData for single-series charts.
     * @param {string} chartId    - id attribute of chart container
     * @param {string} seriesName - name of the series to update
     * @param {number} epoch      - x-axis value (epoch index or Unix ms)
     * @param {number} value      - y-axis value
     */
    window.updateApexSeries = function (chartId, seriesName, epoch, value) {
        const el = document.getElementById(chartId);
        if (!el || !el._apexChart) return;

        if (el._seriesData && Object.prototype.hasOwnProperty.call(el._seriesData, seriesName)) {
            // Named-series chart initialised by initTrainCharts
            el._seriesData[seriesName].push([epoch, value]);
            const updatedSeries = Object.keys(el._seriesData).map(name => ({
                name,
                data: el._seriesData[name],
            }));
            el._apexChart.updateSeries(updatedSeries, false);
        } else {
            // Legacy / single-series fallback
            el._apexChart.appendData([{ data: [[epoch, value]] }]);
        }
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

    // ── ML training chart helpers ────────────────────────────────────────────

    /**
     * Initialize ApexCharts line charts for TrainProgress loss and accuracy panels.
     * Stores _seriesData on each element for named-series tracking via updateApexSeries.
     * @param {string} lossChartId     - element id for the loss chart container
     * @param {string} accuracyChartId - element id for the accuracy chart container
     */
    window.initTrainCharts = function (lossChartId, accuracyChartId) {
        function buildLineChart(containerId, seriesNames, colors) {
            const container = document.getElementById(containerId);
            if (!container || typeof ApexCharts === 'undefined') return;
            if (container._apexChart) { container._apexChart.destroy(); }

            container._seriesData = {};
            seriesNames.forEach(n => { container._seriesData[n] = []; });

            const chart = new ApexCharts(container, {
                series: seriesNames.map((name, i) => ({ name, data: [], color: colors[i] })),
                chart: {
                    id: containerId,
                    type: 'line',
                    height: '100%',
                    background: 'transparent',
                    foreColor: '#8b949e',
                    animations: { enabled: false },
                    toolbar: { show: false },
                },
                stroke: { curve: 'smooth', width: 2 },
                xaxis: {
                    title: { text: 'Epoch', style: { color: '#8b949e' } },
                    labels: { style: { colors: '#8b949e' } },
                },
                yaxis: {
                    labels: { formatter: v => v.toFixed(4), style: { colors: '#8b949e' } },
                },
                grid: { borderColor: 'rgba(255,255,255,0.05)' },
                theme: { mode: 'dark' },
                legend: { show: true, labels: { colors: '#8b949e' } },
            });
            chart.render();
            container._apexChart = chart;
        }

        buildLineChart(lossChartId, ['train_loss', 'val_loss'], ['#00d4ff', '#7c3aed']);
        buildLineChart(accuracyChartId, ['accuracy'], ['#22c55e']);
    };

    /**
     * Render an ApexCharts heatmap for a confusion matrix on TRAINING_JOB_COMPLETE.
     * @param {string}     containerId - element id
     * @param {number[][]} matrix      - square matrix of class counts
     */
    window.renderConfusionMatrix = function (containerId, matrix) {
        const container = document.getElementById(containerId);
        if (!container || typeof ApexCharts === 'undefined') return;
        if (container._apexChart) { container._apexChart.destroy(); }

        const n = matrix.length;
        const labels = Array.from({ length: n }, (_, i) => `Class ${i}`);
        const series = matrix.map((row, ri) => ({
            name: labels[ri],
            data: row.map((val, ci) => ({ x: labels[ci], y: val })),
        }));

        const chart = new ApexCharts(container, {
            series,
            chart: {
                type: 'heatmap',
                height: '100%',
                background: 'transparent',
                foreColor: '#8b949e',
                toolbar: { show: false },
            },
            colors: ['#7c3aed'],
            dataLabels: { enabled: true, style: { colors: ['#fff'], fontSize: '11px' } },
            xaxis: { labels: { style: { colors: Array(n).fill('#8b949e') } } },
            theme: { mode: 'dark' },
        });
        chart.render();
        container._apexChart = chart;
    };

    /**
     * Render a horizontal bar chart of SHAP feature importances on TRAINING_JOB_COMPLETE.
     * Shows top-15 features sorted by |SHAP value| descending.
     * @param {string}   containerId  - element id
     * @param {string[]} featureNames
     * @param {number[]} shapValues
     */
    window.renderShapChart = function (containerId, featureNames, shapValues) {
        const container = document.getElementById(containerId);
        if (!container || typeof ApexCharts === 'undefined') return;
        if (container._apexChart) { container._apexChart.destroy(); }

        const pairs = featureNames.map((name, i) => ({ name, value: shapValues[i] }));
        pairs.sort((a, b) => Math.abs(b.value) - Math.abs(a.value));
        const top = pairs.slice(0, 15);

        const chart = new ApexCharts(container, {
            series: [{ name: 'SHAP', data: top.map(d => d.value) }],
            chart: {
                type: 'bar',
                height: '100%',
                background: 'transparent',
                foreColor: '#8b949e',
                toolbar: { show: false },
            },
            plotOptions: { bar: { horizontal: true, barHeight: '70%' } },
            colors: ['#00d4ff'],
            xaxis: {
                categories: top.map(d => d.name),
                labels: { style: { colors: Array(top.length).fill('#8b949e') } },
            },
            yaxis: { labels: { style: { colors: '#8b949e' } } },
            dataLabels: { enabled: false },
            theme: { mode: 'dark' },
        });
        chart.render();
        container._apexChart = chart;
    };

    // ── Indicator chart ──────────────────────────────────────────────────────

    /**
     * Initialise a multi-series ApexCharts indicator panel from a list of
     * indicator descriptors produced by IndicatorLibrary / FeatureEngineer.ToPlotSamples.
     *
     * After initialisation use updateApexSeries(containerId, sample.SeriesName,
     * sample.TimestampEpochMs, sample.Value) to push live data into the chart.
     *
     * @param {string} containerId
     * @param {Array<{
     *   id:       string,
     *   name:     string,
     *   plotType: string,   // "Line" | "Histogram" | "Area"
     *   color:    string,
     *   min:      number|null,
     *   max:      number|null,
     *   unit:     string
     * }>} descriptors  — serialised IndicatorDescriptor objects
     */
    window.initIndicatorChart = function (containerId, descriptors) {
        const container = document.getElementById(containerId);
        if (!container || typeof ApexCharts === 'undefined') return;
        if (container._apexChart) { container._apexChart.destroy(); }

        container._seriesData = {};
        const series = [];
        const yaxes  = [];

        descriptors.forEach(function (d, idx) {
            const isHistogram = (d.plotType === 'Histogram');
            const isArea      = (d.plotType === 'Area');
            const seriesType  = isHistogram ? 'bar' : (isArea ? 'area' : 'line');

            // Track named series for updateApexSeries
            container._seriesData[d.name] = [];

            series.push({
                name:  d.name,
                type:  seriesType,
                data:  [],
                color: d.color || '#00d4ff',
            });

            // First line-type series drives the primary (left) y-axis.
            // Histograms always get a secondary (right) y-axis.
            // If no line series has been seen yet this entry is the first candidate.
            const firstLineSeen = series.filter(function (s) { return s.type === 'line' || s.type === 'area'; }).length === 1
                               && (seriesType === 'line' || seriesType === 'area');
            yaxes.push({
                seriesName: d.name,
                opposite:   isHistogram,
                show:       firstLineSeen || isHistogram,
                min:        d.min  != null ? d.min  : undefined,
                max:        d.max  != null ? d.max  : undefined,
                tickAmount: 5,
                labels: {
                    style: { colors: [d.color || '#8b949e'] },
                    formatter: function (v) {
                        if (typeof v !== 'number') return v;
                        var formatted = v.toFixed(4);
                        return d.unit ? (formatted + ' ' + d.unit) : formatted;
                    },
                },
            });
        });

        const chart = new ApexCharts(container, {
            series: series,
            chart: {
                id:         containerId,
                type:       'line',
                height:     '100%',
                background: '#0d1117',
                foreColor:  '#8b949e',
                animations: { enabled: false },
                toolbar: { show: true, tools: { zoom: true, pan: true, reset: true } },
            },
            stroke: {
                curve: 'smooth',
                width: series.map(function (s) { return s.type === 'bar' ? 0 : 2; }),
            },
            fill: {
                type: series.map(function (s) { return s.type === 'area' ? 'gradient' : 'solid'; }),
                gradient: { opacityFrom: 0.4, opacityTo: 0.05 },
            },
            xaxis: {
                type: 'datetime',
                labels: { datetimeUTC: false, style: { colors: '#8b949e' } },
            },
            yaxis: yaxes,
            grid:   { borderColor: 'rgba(255,255,255,0.05)' },
            legend: { show: true, position: 'top', labels: { colors: '#8b949e' } },
            theme:  { mode: 'dark' },
            tooltip: {
                theme:  'dark',
                shared: true,
                x:      { format: 'dd MMM HH:mm' },
            },
        });

        chart.render();
        container._apexChart = chart;
    };

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

// ── AI Chat helpers ──────────────────────────────────────────────────────────

window.scrollElementToBottom = function (el) {
    if (el) el.scrollTop = el.scrollHeight;
};

// ── Viewport helpers ─────────────────────────────────────────────────────────

window.mlsViewport = {
    /** Returns the current inner width of the browser window. */
    getInnerWidth: function () { return window.innerWidth; }
};

})();
