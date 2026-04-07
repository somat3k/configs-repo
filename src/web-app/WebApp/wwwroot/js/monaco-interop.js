/**
 * monaco-interop.js
 * MLS Trading Platform — Monaco Editor interop helpers for PropertyEditor.razor.
 *
 * Provides:
 *   - mlsMonaco.initEditor(containerId, initialCode, dotNetRef)
 *   - mlsMonaco.getCode(containerId)
 *   - mlsMonaco.setCode(containerId, code)
 *   - mlsMonaco.disposeEditor(containerId)
 *
 * Requires Monaco Editor to be loaded from CDN via require.js before this script runs.
 * Each editor instance is keyed by containerId.
 */
(function () {
    'use strict';

    // Map from containerId → { editor, debounceTimer }
    const _editors  = new Map();

    /**
     * Wait until window.monaco is defined (loaded via AMD loader), with a 10 s timeout.
     * @returns {Promise<boolean>} true when ready, false on timeout.
     */
    function waitForMonaco() {
        return new Promise(resolve => {
            if (typeof window.monaco !== 'undefined') { resolve(true); return; }
            let attempts = 0;
            const id = setInterval(() => {
                if (typeof window.monaco !== 'undefined') {
                    clearInterval(id);
                    resolve(true);
                } else if (++attempts > 200) { // 200 × 50 ms = 10 s
                    clearInterval(id);
                    console.warn('[mlsMonaco] Monaco did not load within 10 s');
                    resolve(false);
                }
            }, 50);
        });
    }

    window.mlsMonaco = {

        /**
         * Initialise a Monaco editor in the given container.
         * Waits for Monaco to finish its AMD load before creating the editor.
         * @param {string} containerId  - id of the host <div>
         * @param {string} initialCode  - initial C# source code
         * @param {object} dotNetRef    - DotNetObjectReference for change callbacks
         */
        async initEditor(containerId, initialCode, dotNetRef) {
            const ready = await waitForMonaco();
            if (!ready) return;

            const container = document.getElementById(containerId);
            if (!container) {
                console.warn('[mlsMonaco] container not found:', containerId);
                return;
            }

            // Dispose any existing editor in the same container (including its timer + model)
            mlsMonaco.disposeEditor(containerId);

            const editor = monaco.editor.create(container, {
                value:     initialCode || '',
                language:  'csharp',
                theme:     'vs-dark',
                fontSize:  12,
                lineNumbers: 'on',
                minimap:   { enabled: false },
                scrollBeyondLastLine: false,
                wordWrap:  'on',
                automaticLayout: true,
                tabSize:   4,
                insertSpaces: true,
                renderLineHighlight: 'line',
                quickSuggestions: true,
                suggestOnTriggerCharacters: true,
            });

            // Per-editor debounce timer stored on the entry object for clean disposal
            const entry = { editor, debounceTimer: null };

            editor.onDidChangeModelContent(() => {
                clearTimeout(entry.debounceTimer);
                entry.debounceTimer = setTimeout(() => {
                    entry.debounceTimer = null;
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnCodeChanged', editor.getValue())
                            .catch(err => console.warn('[mlsMonaco] OnCodeChanged error:', containerId, err));
                    }
                }, 500);
            });

            _editors.set(containerId, entry);
        },

        /**
         * Get the current code from a Monaco editor instance.
         * @param {string} containerId
         * @returns {string} Current editor content, or empty string if not found.
         */
        getCode(containerId) {
            const entry = _editors.get(containerId);
            return entry ? entry.editor.getValue() : '';
        },

        /**
         * Set (replace) the code in a Monaco editor instance.
         * @param {string} containerId
         * @param {string} code
         */
        setCode(containerId, code) {
            const entry = _editors.get(containerId);
            if (entry) {
                entry.editor.setValue(code || '');
            }
        },

        /**
         * Dispose a Monaco editor instance, its pending debounce timer, and its model.
         * @param {string} containerId
         */
        disposeEditor(containerId) {
            const entry = _editors.get(containerId);
            if (entry) {
                // Cancel any pending debounced callback so it never fires after disposal
                clearTimeout(entry.debounceTimer);
                entry.debounceTimer = null;

                // Dispose the model first, then the editor widget
                const model = entry.editor.getModel();
                if (model) model.dispose();
                entry.editor.dispose();

                _editors.delete(containerId);
            }
        },
    };

})();