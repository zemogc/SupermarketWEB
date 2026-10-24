// Uso: node capture.js <carpeta-de-salida>. Ver README.md de esta carpeta.
// Recorre la aplicación en Edge real: verifica el JavaScript del formulario de ventas,
// detecta errores de consola / violaciones de CSP y guarda capturas para el README.
const { chromium } = require('playwright-core');
const path = require('path');

const BASE = process.env.BASE_URL || 'http://localhost:5330';
const OUT = process.argv[2];
const problems = [];

async function main() {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const context = await browser.newContext({ viewport: { width: 1366, height: 820 }, locale: 'es-CO' });
    const page = await context.newPage();

    page.on('console', (msg) => {
        if (msg.type() === 'error' || msg.type() === 'warning') problems.push(`[console ${msg.type()}] ${page.url()} :: ${msg.text()}`);
    });
    page.on('pageerror', (err) => problems.push(`[pageerror] ${page.url()} :: ${err.message}`));
    page.on('response', (res) => { if (res.status() >= 400) problems.push(`[http ${res.status()}] ${res.url()}`); });

    const shot = async (name, fullPage = true) => {
        await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage });
        console.log(`captura: ${name}.png`);
    };

    // Login
    await page.goto(`${BASE}/Account/Login`);
    await shot('login', false);
    await page.fill('#Input_Email', 'admin@supermarket.local');
    await page.fill('#Input_Password', 'Admin123!');
    await Promise.all([page.waitForURL(`${BASE}/`), page.click('button[type=submit]')]);

    // La barra de navegación debe caber en una línea en un portátil común.
    const navHeight = await page.$eval('nav.navbar', (n) => n.offsetHeight);
    if (navHeight > 72) problems.push(`[layout] la barra de navegación mide ${navHeight}px (se parte en varias líneas)`);
    console.log(`altura de la barra: ${navHeight}px`);

    await shot('dashboard');

    // Formulario de venta: agrega filas, elige productos y verifica el total calculado en el navegador.
    await page.goto(`${BASE}/Sales/Create`);
    const options = await page.$$eval('[data-lines] [data-product] option[value]:not([value=""])',
        (els) => els.map((o) => ({ value: o.value, price: parseFloat(o.dataset.price) })));
    const picks = options.slice(0, 3);

    await page.selectOption('select[name="Input.CustomerId"]', { index: 1 });
    await page.selectOption('select[name="Input.PayModeId"]', { index: 1 });
    for (let i = 0; i < picks.length; i++) {
        if (i > 0) await page.click('[data-add-line]');
        const row = page.locator('[data-lines] [data-line]').nth(i);
        await row.locator('[data-product]').selectOption(picks[i].value);
        await row.locator('[data-quantity]').fill(String(i + 1));
    }

    const names = await page.$$eval('[data-lines] [data-line] [data-product]', (els) => els.map((e) => e.name));
    const expectedNames = picks.map((_, i) => `Input.Lines[${i}].ProductId`);
    if (JSON.stringify(names) !== JSON.stringify(expectedNames)) problems.push(`[sales.js] índices incorrectos: ${names}`);

    const expectedTotal = picks.reduce((sum, p, i) => sum + p.price * (i + 1), 0);
    const totalText = await page.textContent('[data-total]');
    const totalDigits = Number(totalText.replace(/[^\d,]/g, '').replace(',', '.'));
    if (Math.abs(totalDigits - expectedTotal) > 0.01) problems.push(`[sales.js] total ${totalText} != ${expectedTotal}`);
    console.log(`total en pantalla: ${totalText.trim()} (esperado ${expectedTotal})`);

    // Quitar una fila y volver a agregarla debe mantener los índices consecutivos.
    await page.locator('[data-lines] [data-line]').nth(1).locator('[data-remove-line]').click();
    const afterRemove = await page.$$eval('[data-lines] [data-product]', (els) => els.map((e) => e.name));
    if (JSON.stringify(afterRemove) !== JSON.stringify(['Input.Lines[0].ProductId', 'Input.Lines[1].ProductId'])) {
        problems.push(`[sales.js] reindexado tras quitar: ${afterRemove}`);
    }
    await page.click('[data-add-line]');
    const row = page.locator('[data-lines] [data-line]').nth(2);
    await row.locator('[data-product]').selectOption(picks[1].value);
    await row.locator('[data-quantity]').fill('2');
    await shot('nueva-venta');

    await Promise.all([page.waitForURL(/\/Sales\/Details\/\d+/), page.click('button[type=submit].btn-success')]);
    await shot('comprobante');

    await page.goto(`${BASE}/Sales`);
    await shot('ventas', false);

    await page.goto(`${BASE}/Reports`);
    await shot('reportes', false);

    await page.goto(`${BASE}/Inventory`);
    await shot('inventario', false);

    await page.goto(`${BASE}/Products`);
    await shot('productos');

    await page.goto(`${BASE}/Users`);
    await shot('usuarios');

    // Menú de administración desplegado (requiere el JavaScript de Bootstrap).
    await page.click('nav .dropdown-toggle:has-text("Administración")');
    await page.waitForSelector('nav .dropdown-menu.show');
    await shot('menu-administracion', false);
    await page.keyboard.press('Escape');

    // Vista del cajero en móvil.
    const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, locale: 'es-CO' });
    const phone = await mobile.newPage();
    phone.on('pageerror', (err) => problems.push(`[pageerror móvil] ${err.message}`));
    await phone.goto(`${BASE}/Account/Login`);
    await phone.fill('#Input_Email', 'cajero@supermarket.local');
    await phone.fill('#Input_Password', 'Cajero123!');
    await Promise.all([phone.waitForURL(`${BASE}/`), phone.click('button[type=submit]')]);
    await phone.screenshot({ path: path.join(OUT, 'movil-cajero.png'), fullPage: false });
    console.log('captura: movil-cajero.png');

    // Prueba de las confirmaciones data-confirm (anular venta) en el comprobante, cancelando el diálogo.
    await page.goto(`${BASE}/Sales`);
    await page.click('table a.btn-outline-primary');
    let dialogShown = false;
    page.once('dialog', async (d) => { dialogShown = true; await d.dismiss(); });
    const cancelButton = page.locator('form[data-confirm] button');
    if (await cancelButton.count()) {
        await cancelButton.click();
        await page.waitForTimeout(300);
        if (!dialogShown) problems.push('[site.js] no se mostró la confirmación al anular');
        if (await page.locator('.badge.bg-danger.fs-6').count()) problems.push('[site.js] la venta se anuló pese a cancelar el diálogo');
    }

    await browser.close();

    console.log(problems.length ? `PROBLEMAS:\n${problems.join('\n')}` : 'Sin errores de consola, CSP ni HTTP.');
    process.exit(problems.length ? 1 : 0);
}

main().catch((err) => { console.error(err); process.exit(2); });
