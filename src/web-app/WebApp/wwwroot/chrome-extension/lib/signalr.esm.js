/**
 * SignalR ESM development stub for MLS Chrome Extension.
 *
 * PRODUCTION BUILD:
 *   This stub must be replaced with the real @microsoft/signalr ESM build.
 *   Run the following from chrome-extension/:
 *
 *     npm install
 *     npm run build
 *
 *   The build script copies node_modules/@microsoft/signalr/dist/esm/index.js
 *   to this location (lib/signalr.esm.js).
 *
 * The stub exports all types needed by background.js so the module loads
 * without errors in developer mode, but .start() will throw to alert that
 * the real library is missing.
 */

// ── Enums ─────────────────────────────────────────────────────────────────────

export const LogLevel = Object.freeze({
    Trace: 0, Debug: 1, Information: 2, Warning: 3,
    Error: 4, Critical: 5, None: 6
});

export const HubConnectionState = Object.freeze({
    Disconnected:  'Disconnected',
    Connecting:    'Connecting',
    Connected:     'Connected',
    Disconnecting: 'Disconnecting',
    Reconnecting:  'Reconnecting'
});

export const HttpTransportType = Object.freeze({
    None: 0, WebSockets: 1, ServerSentEvents: 2, LongPolling: 4
});

// ── Stub HubConnection ────────────────────────────────────────────────────────

class StubHubConnection {
    #state = HubConnectionState.Disconnected;
    get state() { return this.#state; }

    async start() {
        throw new Error(
            '[MLS Extension] signalr.esm.js is a development stub.\n' +
            'Run: cd chrome-extension && npm install && npm run build\n' +
            'to replace this file with the real @microsoft/signalr ESM build.'
        );
    }
    async stop() {}
    async invoke() {}
    async send() {}
    on() {}
    off() {}
    onclose() {}
    onreconnecting() {}
    onreconnected() {}
    stream() { return { subscribe() {} }; }
}

// ── HubConnectionBuilder ──────────────────────────────────────────────────────

export class HubConnectionBuilder {
    #url = '';
    #logLevel = LogLevel.Warning;

    withUrl(url) {
        this.#url = url;
        return this;
    }

    withAutomaticReconnect() { return this; }

    configureLogging(level) {
        this.#logLevel = level;
        return this;
    }

    build() {
        console.warn(
            `[MLS Extension] Building stub SignalR connection to ${this.#url}.\n` +
            'Replace lib/signalr.esm.js with the real @microsoft/signalr package.'
        );
        return new StubHubConnection();
    }
}
