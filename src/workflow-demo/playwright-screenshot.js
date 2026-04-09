const { chromium } = require('playwright');
const path = require('path');
const fs   = require('fs');

const BASE_URL = 'http://localhost:5099';
const OUT_DIR  = path.join(__dirname, '../../docs/screenshots');
const VIEWPORT = { width: 1440, height: 900 };

const pages = [
  { slug: 'index',            url: '/workflow',                  name: 'Workflow Index' },
  { slug: 'block-controller', url: '/workflow/block-controller', name: 'Block Controller' },
  { slug: 'data-layer',       url: '/workflow/data-layer',       name: 'Data Layer' },
  { slug: 'trader',           url: '/workflow/trader',           name: 'Trader' },
  { slug: 'arbitrager',       url: '/workflow/arbitrager',       name: 'Arbitrager' },
  { slug: 'defi',             url: '/workflow/defi',             name: 'DeFi' },
  { slug: 'ml-runtime',       url: '/workflow/ml-runtime',       name: 'ML Runtime' },
  { slug: 'designer',         url: '/workflow/designer',         name: 'Designer' },
  { slug: 'ai-hub',           url: '/workflow/ai-hub',           name: 'AI Hub' },
  { slug: 'broker',           url: '/workflow/broker',           name: 'Broker' },
  { slug: 'transactions',     url: '/workflow/transactions',     name: 'Transactions' },
  { slug: 'shell-vm',         url: '/workflow/shell-vm',         name: 'Shell VM' },
];

(async () => {
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const results = [];

  for (const p of pages) {
    const outPath = path.join(OUT_DIR, `${p.slug}.png`);
    console.log(`📸  ${p.name}  →  ${p.url}`);
    const pw = await browser.newPage();
    await pw.setViewportSize(VIEWPORT);
    try {
      // Load page and wait for Blazor circuit to complete the initial render
      await pw.goto(BASE_URL + p.url, { waitUntil: 'networkidle', timeout: 25000 });
      // Blazor Server needs time to establish WS circuit and run OnInitializedAsync
      // Wait for either #data-loaded sentinel OR the .wf-loading div to disappear
      await pw.waitForFunction(
        () => document.querySelector('#data-loaded') !== null
           || (document.querySelector('.wf-loading') === null && document.querySelector('.wf-table') !== null)
           || document.querySelector('.wfi-grid') !== null,
        { timeout: 20000 }
      ).catch(() => null); // don't fail on timeout — partial data still useful
      await pw.waitForTimeout(600); // CSS animations settle
      await pw.screenshot({ path: outPath, fullPage: true });
      results.push({ slug: p.slug, ok: true });
      console.log(`   ✔  ${outPath}`);
    } catch (err) {
      try { await pw.screenshot({ path: outPath, fullPage: true }); } catch {}
      results.push({ slug: p.slug, ok: false, err: err.message.slice(0, 80) });
      console.warn(`   ⚠  ${err.message.slice(0, 80)}`);
    } finally {
      await pw.close();
    }
  }

  await browser.close();
  console.log('\n── Results ──────────────────────────────────────────────────');
  for (const r of results)
    console.log(`${r.ok ? '✔' : '⚠'}  ${r.slug.padEnd(22)} ${r.ok ? 'OK' : r.err ?? ''}`);
  process.exit(results.every(r => r.ok) ? 0 : 1);
})();
