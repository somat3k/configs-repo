/**
 * canvas-pulse.js
 * MLS Trading Platform — Designer canvas live-signal pulse animations.
 *
 * Provides:
 *   mlsPulse.triggerPulse(connectionId, color)
 *     Animates a glowing circle along the SVG bezier path that represents
 *     the connection.  Triggered by Blazor on every BLOCK_SIGNAL envelope.
 *     No-ops when (prefers-reduced-motion: reduce) is active.
 *
 *   mlsPulse.updateBlockBadge(blockId, latencyMs, msgPerSec, errorRatePct, statusColor)
 *     Direct DOM mutation for the per-block live-stats badge.
 *     Called at 1 Hz from the Blazor PeriodicTimer — avoids triggering a
 *     full Blazor component re-render.
 *
 *   mlsPulse.updateOverlay(infPerSec, fillRatePct, plDelta)
 *     Direct DOM mutation for the LiveOverlay KPI strip.
 *     Called at 1 Hz from LiveOverlay.razor.
 */
(function () {
    'use strict';

    // ── Pulse animation ──────────────────────────────────────────────────────

    /**
     * Animate a glowing circle travelling from source to target along the
     * SVG bezier path identified by connectionId.
     *
     * Uses SVG SMIL <animateMotion> with an <mpath> reference for sub-pixel
     * accurate path-following.  The circle is appended to a dedicated
     * <g class="pulses-layer"> inside the connections SVG and removed after
     * the 300 ms animation completes.
     *
     * @param {string} connectionId   - GUID string matching the path id attribute
     *                                  ("conn-path-{connectionId}")
     * @param {string} color          - CSS colour for the pulse circle and glow
     */
    window.mlsPulse = window.mlsPulse || {};

    window.mlsPulse.triggerPulse = function (connectionId, color) {
        // Respect user motion preferences
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const pathId = 'conn-path-' + connectionId;
        const pathEl = document.getElementById(pathId);
        if (!pathEl) return;

        const svg = pathEl.closest('svg');
        if (!svg) return;

        const NS = 'http://www.w3.org/2000/svg';

        // ── Pulse circle ─────────────────────────────────────────────────────
        const circle = document.createElementNS(NS, 'circle');
        circle.setAttribute('r', '5');
        circle.setAttribute('fill', color);
        // will-change: transform + opacity satisfies the spec requirement
        circle.style.willChange = 'transform, opacity';

        // ── Motion animation ─────────────────────────────────────────────────
        const animMotion = document.createElementNS(NS, 'animateMotion');
        animMotion.setAttribute('dur', '300ms');
        animMotion.setAttribute('fill', 'remove');
        animMotion.setAttribute('calcMode', 'spline');
        animMotion.setAttribute('keyTimes', '0;1');
        animMotion.setAttribute('keySplines', '0.4 0 0.2 1');
        animMotion.setAttribute('rotate', 'auto');

        const mpath = document.createElementNS(NS, 'mpath');
        mpath.setAttributeNS('http://www.w3.org/1999/xlink', 'href', '#' + pathId);
        animMotion.appendChild(mpath);
        circle.appendChild(animMotion);

        // ── Opacity fade: 0 → 1 → 1 → 0 ────────────────────────────────────
        const animOpacity = document.createElementNS(NS, 'animate');
        animOpacity.setAttribute('attributeName', 'opacity');
        animOpacity.setAttribute('values', '0;1;1;0');
        animOpacity.setAttribute('keyTimes', '0;0.1;0.8;1');
        animOpacity.setAttribute('dur', '300ms');
        animOpacity.setAttribute('fill', 'remove');
        circle.appendChild(animOpacity);

        // ── Glow using filter (no layout reflow) ─────────────────────────────
        const filterId = 'pulse-glow-' + connectionId;
        let filter = svg.querySelector('#' + filterId);
        if (!filter) {
            const defs = svg.querySelector('defs') || (() => {
                const d = document.createElementNS(NS, 'defs');
                svg.insertBefore(d, svg.firstChild);
                return d;
            })();
            filter = document.createElementNS(NS, 'filter');
            filter.setAttribute('id', filterId);
            filter.setAttribute('x', '-50%');
            filter.setAttribute('y', '-50%');
            filter.setAttribute('width', '200%');
            filter.setAttribute('height', '200%');
            const feGlow = document.createElementNS(NS, 'feGaussianBlur');
            feGlow.setAttribute('in', 'SourceGraphic');
            feGlow.setAttribute('stdDeviation', '3');
            filter.appendChild(feGlow);
            defs.appendChild(filter);
        }
        circle.setAttribute('filter', 'url(#' + filterId + ')');

        // ── Append to pulses layer ────────────────────────────────────────────
        let pulsesLayer = svg.querySelector('.pulses-layer');
        if (!pulsesLayer) {
            pulsesLayer = document.createElementNS(NS, 'g');
            pulsesLayer.setAttribute('class', 'pulses-layer');
            svg.appendChild(pulsesLayer);
        }
        pulsesLayer.appendChild(circle);

        // ── Kick off SMIL ─────────────────────────────────────────────────────
        try {
            animMotion.beginElement();
            animOpacity.beginElement();
        } catch (_) { /* IE/old WebKit fallback: animations auto-start */ }

        // Remove element after animation; 320 ms gives a small buffer
        setTimeout(function () {
            if (pulsesLayer.contains(circle)) pulsesLayer.removeChild(circle);
        }, 320);
    };

    // ── Block badge direct-DOM update ────────────────────────────────────────

    /**
     * Update a block's live-stats badge elements directly in the DOM at 1 Hz
     * without triggering a Blazor render cycle.
     *
     * @param {string} blockId        - GUID string used to locate badge elements
     * @param {number} latencyMs      - rolling average latency in ms
     * @param {number} msgPerSec      - messages processed in the last second
     * @param {number} errorRatePct   - error percentage over last 60 s
     * @param {string} statusColor    - CSS colour for the status dot
     */
    window.mlsPulse.updateBlockBadge = function (blockId, latencyMs, msgPerSec, errorRatePct, statusColor) {
        const prefix = 'blk-' + blockId;

        _setText(prefix + '-lat', latencyMs.toFixed(1) + ' ms');
        _setText(prefix + '-mps', msgPerSec.toFixed(1) + '/s');
        _setText(prefix + '-err', errorRatePct.toFixed(1) + '%');

        const dot = document.getElementById(prefix + '-dot');
        if (dot) dot.style.background = statusColor;
    };

    // ── LiveOverlay direct-DOM update ─────────────────────────────────────────

    /**
     * Update the LiveOverlay KPI values directly in the DOM at 1 Hz.
     *
     * @param {number} infPerSec    - total BLOCK_SIGNAL events in the last second
     * @param {number} fillRatePct  - ratio of order fills to orders placed (0–100)
     * @param {number|null} plDelta - realised P&L change since last window (null → "—")
     */
    window.mlsPulse.updateOverlay = function (infPerSec, fillRatePct, plDelta) {
        _setText('lo-inf-sec', infPerSec.toFixed(1));
        _setText('lo-fill-rate', fillRatePct.toFixed(1) + '%');
        _setText('lo-pl-delta', plDelta !== null ? (plDelta >= 0 ? '+' : '') + plDelta.toFixed(2) : '—');

        const plEl = document.getElementById('lo-pl-delta');
        if (plEl) {
            plEl.className = 'lo-val ' + (plDelta === null ? '' : plDelta >= 0 ? 'lo-positive' : 'lo-negative');
        }
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    function _setText(id, text) {
        const el = document.getElementById(id);
        if (el && el.textContent !== text) el.textContent = text;
    }
}());
