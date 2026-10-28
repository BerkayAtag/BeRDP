// Run after Release builds. Requires Playwright; all data lives in a temporary directory.
const { chromium } = require('playwright');
const { spawn, spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'BeRDP-web-'));
const password = crypto.randomBytes(24).toString('base64');
const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production',
  Storage__DataDirectory: temp, Jwt__Key: crypto.randomBytes(48).toString('base64'),
  Cors__AllowedOrigins__0: 'http://127.0.0.1:5517' };
const backend = path.join(root, 'src/BeRdp.Backend');
const portal = path.join(root, 'src/BeRdp.AdminConsole');
const dll = path.join(backend, 'bin/Release/net10.0/BeRdp.Backend.dll');
const bootstrap = spawnSync('dotnet', [dll, '--bootstrap-admin', 'qa-admin'],
  { cwd: backend, env, input: password + '\n', windowsHide: true });
if (bootstrap.status !== 0) throw new Error('Bootstrap failed: ' + bootstrap.stderr);
const children = [];
let browser;
let passed = 0;
function check(condition, message) { assert.ok(condition, message); passed++; console.log('PASS ' + message); }
async function api(route, token, method = 'GET', body) {
  const response = await fetch('http://127.0.0.1:5501' + route, { method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null, headers: response.headers };
}
async function ready(url) {
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(url)).ok) return; } catch {}
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error('Server did not become ready: ' + url);
}
(async () => {
  try {
    children.push(spawn('dotnet', [dll, '--urls', 'http://127.0.0.1:5501'], { cwd: backend, env, windowsHide: true, stdio: 'ignore' }));
    children.push(spawn('dotnet', [path.join(portal, 'bin/Release/net10.0/BeRdp.AdminConsole.dll'), '--urls', 'http://127.0.0.1:5517'],
      { cwd: portal, env: { ...env, AdminConsole__BackendBaseUrl: 'http://127.0.0.1:5501' }, windowsHide: true, stdio: 'ignore' }));
    await Promise.all([ready('http://127.0.0.1:5501/health'), ready('http://127.0.0.1:5517/health')]);
    check((await api('/admin/users')).status === 401, 'Anonymous administration is rejected');
    check((await api('/login', null, 'POST', { username: 'admin', password: 'Admin123!' })).status === 401, 'No default admin login exists');
    const login = await api('/login', null, 'POST', { username: 'qa-admin', password });
    check(login.status === 200 && login.body.isAdmin, 'Explicit bootstrap login works');
    const token = login.body.token;
    check((await api('/machines', token)).body.length === 0, 'Fresh installation contains no demo machines');
    const user = await api('/users', token, 'POST', { username: 'qa-member', displayName: 'Example Operator', password });
    check(user.status === 200 || user.status === 201, 'Administrator creates a local user');
    const memberLogin = await api('/login', null, 'POST', { username: 'qa-member', password });
    const member = memberLogin.body.token;
    check((await api('/admin/users', member)).status === 403, 'Member cannot enumerate administrator data');
    const data = { name: 'Engineering Desktop', ipAddress: '192.0.2.10', rdpHostName: 'desktop01.example.test', rdpPort: 3389, entitledUserIds: [user.body.id] };
    const machine = await api('/machines', token, 'POST', data);
    check(machine.status === 200 || machine.status === 201, 'Administrator creates an entitled machine');
    check((await api('/machines/' + machine.body.id, member)).status === 200, 'Assigned member can refresh launch authorization');
    await api('/machines/' + machine.body.id, token, 'PUT', { ...data, entitledUserIds: [] });
    check((await api('/machines/' + machine.body.id, member)).status === 404, 'Revoked assignment blocks next native launch lookup');
    check((await api('/users/' + login.body.userId + '/role', token, 'PUT', { isAdmin: false, canAccessAdminConsole: false })).status === 400, 'Last local administrator is protected');
    const screenshotDir = path.join(root, 'docs/ui'); fs.mkdirSync(screenshotDir, { recursive: true });
    browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE || undefined });
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto('http://127.0.0.1:5517');
    await page.screenshot({ path: path.join(screenshotDir, 'login.png'), fullPage: true });
    await page.locator('#usernameInput').fill('qa-admin');
    await page.locator('#passwordInput').fill(password);
    await page.locator('#loginForm button[type=submit]').click();
    await page.locator('#dashboardView').waitFor({ state: 'visible' });
    await page.locator('[data-page=machines]').click();
    await page.getByText('Engineering Desktop', { exact: true }).first().waitFor();
    await page.screenshot({ path: path.join(screenshotDir, 'machines.png'), fullPage: true });
    check(errors.length === 0, 'Real browser login and machine inventory have no JavaScript errors');
    await page.locator('#logoutButton').click();
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(screenshotDir, 'login-mobile.png'), fullPage: true });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Mobile login has no horizontal overflow');
    const mock = await browser.newPage({ bypassCSP: true, viewport: { width: 1440, height: 1000 } });
    await mock.goto('http://127.0.0.1:5517');
    await mock.addScriptTag({ path: path.join(__dirname, 'AdminConsole.Regression.js') });
    const regression = await mock.evaluate(() => window.runBeRdpAdminRegression());
    console.log(JSON.stringify(regression, null, 2));
    console.log(`SUCCESS: ${passed} live checks, ${regression.passed} browser regression checks.`);
  } finally {
    if (browser) await browser.close();
    for (const child of children) child.kill();
    // Temporary DB stays outside the release tree; no fixture enters published installers.
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
