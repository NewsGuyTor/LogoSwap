import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// Reuse an installed Playwright; no browser or npm dependencies are shipped with the plugin.
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright');
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH });
const css = await readFile(new URL('../Views/branding.css', import.meta.url), 'utf8');
const script = await readFile(new URL('../Views/branding.js', import.meta.url), 'utf8');
const configPage = await readFile(new URL('../Views/configPage.html', import.meta.url), 'utf8');
const logoUrl = '/jellyfin/logoswap/image?v=1';
const stylesheet = css.replaceAll('__LOGO_URL__', JSON.stringify(logoUrl));

try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await page.route('**/*', route => route.fulfill(route.request().isNavigationRequest() ? {
        contentType: 'text/html', body: '<!doctype html><html><body></body></html>'
    } : {
        contentType: 'image/svg+xml',
        body: '<svg xmlns="http://www.w3.org/2000/svg" width="400" height="125"><rect width="400" height="125" fill="red"/></svg>'
    }));
    await page.goto('http://logoswap.test/');
    await page.setContent(`
        <style>
        /* The ElegantFin companion selector that caps a wide logo to a small square. */
        .MuiAppBar-root .MuiButton-text:not(.MuiButtonGroup-grouped):has(.MuiButton-startIcon > img) .MuiButton-startIcon > img {
            max-height: 24px !important; max-width: 24px !important;
        }
        </style>
        <div id="reactRoot">
            <header class="MuiAppBar-root">
                <a class="MuiButton-text"><span class="MuiButton-startIcon"><img id="header" src="/icon-transparent.png" style="max-width:1.25em;max-height:1.25em" /></span>Server</a>
                <img id="unrelated" src="/poster.png" width="24" height="24" />
            </header>
            <h3 class="pageTitleWithLogo" style="width:128px;height:40px"></h3>
        </div>
        <nav class="MuiDrawer-root"><img id="drawer" src="/icon-transparent.png" style="max-width:1.25em;max-height:1.25em" /></nav>`);
    const size = selector => page.locator(selector).evaluate(el => ({ width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height }));
    assert.ok((await size('#header')).width <= 24, 'reproduce the small-logo regression');
    await page.addStyleTag({ content: stylesheet });
    assert.deepEqual(await size('#header'), { width: 128, height: 40 });
    assert.deepEqual(await size('#drawer'), { width: 128, height: 40 });
    await page.addScriptTag({ content: script.replaceAll('__LOGO_URL__', JSON.stringify(logoUrl)) });
    assert.equal(await page.locator('#header').getAttribute('src'), logoUrl);
    assert.deepEqual(await size('#header'), { width: 128, height: 40 }, 'sizing survives JS changing the image URL');
    assert.deepEqual(await size('#unrelated'), { width: 24, height: 24 });
    await page.setViewportSize({ width: 390, height: 844 });
    assert.deepEqual(await size('#header'), { width: 104, height: 32.5 });
    assert.deepEqual(await size('#drawer'), { width: 128, height: 40 });
    await page.evaluate(() => { document.querySelector('#header').src = '/icon-transparent-new.png'; });
    await page.waitForFunction(url => document.querySelector('#header').getAttribute('src') === url, logoUrl);
    assert.deepEqual(await size('#header'), { width: 104, height: 32.5 });
    await page.close();
    console.log('PASS: desktop/mobile sizing, theme priority, navigation, and unrelated images');

    for (const base of ['', '/jellyfin', '/media/jellyfin']) {
        const config = await browser.newPage();
        let hasLogo = true;
        let hasFavicon = true;
        let failStyles = false;
        const writes = [];
        await config.addInitScript(({ base }) => {
            window.savedBranding = {
                CustomCss: '/* user CSS */\nbody {color: red;}\n/* LogoSwap CSS */old/* END LogoSwap CSS */',
                CustomJs: 'userScript();\n/* LogoSwap */old/* END LogoSwap */'
            };
            window.ApiClient = {
                accessToken: () => 'test-admin-token',
                getUrl: path => location.origin + base + '/' + path,
                getNamedConfiguration: async () => ({ ...window.savedBranding }),
                updateNamedConfiguration: async (_, branding) => { window.savedBranding = branding; }
            };
        }, { base });
        await config.route('**/*', async route => {
            const request = route.request();
            const path = new URL(request.url()).pathname;
            if (path === base + '/web/config') return route.fulfill({ contentType: 'text/html', body: configPage });
            assert.ok(path.startsWith(base + '/logoswap/'), 'all requests must retain the configured base path: ' + path);
            if (path.endsWith('/status')) return route.fulfill({ json: { hasLogo, hasFavicon, logoUrl: base + '/logoswap/image', faviconUrl: base + '/logoswap/favicon' } });
            if (path.endsWith('/styles')) return route.fulfill(failStyles
                ? { status: 500, body: '<b>Save failed</b>' }
                : { contentType: 'text/css', body: css.replaceAll('__LOGO_URL__', JSON.stringify(base + '/logoswap/image?v=1')) });
            if (request.method() !== 'GET') {
                assert.equal(request.headers()['x-emby-token'], 'test-admin-token');
                writes.push(request.method() + ' ' + path);
                if (path.endsWith('/delete')) hasLogo = false;
                if (path.endsWith('/favicon')) hasFavicon = request.method() === 'POST';
                return route.fulfill({ body: 'Saved' });
            }
            return route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg"/>' });
        });
        await config.goto('http://logoswap.test' + base + '/web/config');
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent.includes('Custom logo is active'));
        await config.locator('#applyBrandingBtn').click();
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent.includes('Custom logo applied'));
        let branding = await config.evaluate(() => window.savedBranding);
        assert.ok(branding.CustomCss.includes('/* user CSS */'));
        assert.equal(branding.CustomCss.split('/* LogoSwap CSS */').length, 2);
        assert.ok(branding.CustomCss.includes('"' + base + '/logoswap/image?v=1"'));
        assert.equal(branding.CustomJs, 'userScript();');

        await config.evaluate(() => document.querySelector('#LogoSwapConfigPage').dispatchEvent(new Event('viewshow')));
        await config.waitForFunction(() => !document.querySelector('#selectFaviconBtn').disabled);
        await config.locator('#faviconFile').setInputFiles({ name: 'test.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg"/>') });
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent === 'Saved');
        assert.equal(writes.length, 1, 're-entering the page does not duplicate upload handlers');

        failStyles = true;
        await config.locator('#applyBrandingBtn').click();
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent === 'Error: <b>Save failed</b>');
        assert.equal(await config.locator('#statusMessage b').count(), 0, 'server errors are text, not HTML');
        assert.deepEqual(await config.evaluate(() => window.savedBranding), branding, 'failed stylesheet requests do not overwrite branding');

        config.on('dialog', dialog => dialog.accept());
        await config.locator('#deleteFaviconBtn').click();
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent.startsWith('Default browser icon restored'));
        assert.ok(hasLogo, 'deleting a favicon preserves the logo');
        await config.locator('#deleteBtn').click();
        await config.waitForFunction(() => document.querySelector('#statusMessage').textContent.startsWith('Default logo restored'));
        branding = await config.evaluate(() => window.savedBranding);
        assert.equal(branding.CustomCss, '/* user CSS */\nbody {color: red;}');
        assert.ok(await config.locator('#applyBrandingBtn').isDisabled());
        await config.close();
    }
    console.log('PASS: root/nested base paths, authenticated writes, apply/remove, independent favicon, errors, and repeated navigation');
} finally {
    await browser.close();
}
