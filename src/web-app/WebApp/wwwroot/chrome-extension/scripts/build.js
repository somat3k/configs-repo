/**
 * MLS Chrome Extension — Build Script
 *
 * Copies vendored dependencies from node_modules into their expected locations:
 *   lib/signalr.esm.js   ← @microsoft/signalr ESM build
 *   sidebar/apexcharts.min.js ← apexcharts UMD build
 *
 * Usage: npm run build
 */

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');

function copy(src, dest) {
    const destDir = path.dirname(dest);
    if (!fs.existsSync(destDir)) fs.mkdirSync(destDir, { recursive: true });
    fs.copyFileSync(src, dest);
    console.log(`  ✓ ${path.relative(root, dest)}`);
}

console.log('[MLS Build] Copying extension dependencies…\n');

// SignalR ESM → lib/signalr.esm.js  (replaces the development stub)
copy(
    path.join(root, 'node_modules/@microsoft/signalr/dist/esm/index.js'),
    path.join(root, 'lib/signalr.esm.js')
);

// ApexCharts UMD → sidebar/apexcharts.min.js
copy(
    path.join(root, 'node_modules/apexcharts/dist/apexcharts.min.js'),
    path.join(root, 'sidebar/apexcharts.min.js')
);

console.log('\n[MLS Build] Done. Load the extension from chrome://extensions in developer mode.');
