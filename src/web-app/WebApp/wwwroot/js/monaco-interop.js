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
 * Requires Monaco Editor to be loaded from CDN before this script runs.
 * Each editor instance is keyed by containerId.
 */
(function () {
    'use strict';

    // Map from containerId → monaco editor instance
    const _editors = new Map();

    window.mlsMonaco = {

        /**
         * Initialise a Monaco editor in the given container.
         * @param {string} containerId  - id of the host <div>
         * @param {string} initialCode  - initial C# source code
         * @param {object} dotNetRef    - DotNetObjectReference for change callbacks
         */
        initEditor(containerId, initialCode, dotNetRef) {
            if (typeof monaco === 'undefined') {
                console.warn('[mlsMonaco] Monaco not loaded — editor skipped for', containerId);
                return;
            }

            const container = document.getElementById(containerId);
            if (!container) {
                console.warn('[mlsMonaco] container not found:', containerId);
                return;
            }

            // Dispose any existing editor in the same container
            if (_editors.has(containerId)) {
                _editors.get(containerId).dispose();
            }

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

            // Notify Blazor on every content change (debounced 500ms)
            let _debounceTimer = null;
            editor.onDidChangeModelContent(() => {
                if (_debounceTimer) clearTimeout(_debounceTimer);
                _debounceTimer = setTimeout(() => {
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnCodeChanged', editor.getValue())
                            .catch(err => console.warn('[mlsMonaco] OnCodeChanged error:', err));
                    }
                }, 500);
            });

            _editors.set(containerId, editor);
        },

        /**
         * Get the current code from a Monaco editor instance.
         * @param {string} containerId
         * @returns {string} Current editor content, or empty string if not found.
         */
        getCode(containerId) {
            const editor = _editors.get(containerId);
            return editor ? editor.getValue() : '';
        },

        /**
         * Set (replace) the code in a Monaco editor instance.
         * @param {string} containerId
         * @param {string} code
         */
        setCode(containerId, code) {
            const editor = _editors.get(containerId);
            if (editor) {
                editor.setValue(code || '');
            }
        },

        /**
         * Dispose a Monaco editor instance and remove it from the registry.
         * @param {string} containerId
         */
        disposeEditor(containerId) {
            const editor = _editors.get(containerId);
            if (editor) {
                editor.dispose();
                _editors.delete(containerId);
            }
        },
    };

})();
