// Regenerates the light/dark product screenshots in docs/ui from a real Release
// build. All accounts, machines and groups are synthetic and live in a temporary
// database; nothing here enters the installers. Requires Playwright (or
// playwright-core with PLAYWRIGHT_CHROMIUM_EXECUTABLE pointing at Edge/Chrome).
let playwright;
try { playwright = require('playwright'); } catch { playwright = require('playwright-core'); }
const { spawn, spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '..');
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'BeRDP-shots-'));
const password = crypto.randomBytes(24).toString('base64');
const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production',
  Storage__DataDirectory: temp, Jwt__Key: crypto.randomBytes(48).toString('base64'),
  Cors__AllowedOrigins__0: 'http://127.0.0.1:5517' };
const backend = path.join(root, 'src/BeRdp.Backend');
const portal = path.join(root, 'src/BeRdp.AdminConsole');
const dll = path.join(backend, 'bin/Release/net10.0/BeRdp.Backend.dll');
const output = path.join(root, 'docs/ui');
const bootstrap = spawnSync('dotnet', [dll, '--bootstrap-admin', 'admin'],
  { cwd: backend, env, input: password + '\n', windowsHide: true });
if (bootstrap.status !== 0) throw new Error('Bootstrap failed: ' + bootstrap.stderr);

async function api(route, token, method = 'GET', body) {
  const response = await fetch('http://127.0.0.1:5501' + route, { method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  if (!response.ok) throw new Error(`${method} ${route} -> ${response.status} ${text}`);
  return text ? JSON.parse(text) : null;
}
async function ready(url) {
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(url)).ok) return; } catch {}
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error('Server did not become ready: ' + url);
}

async function seed(token) {
  const people = [
    ['ayse.yilmaz', 'Ayşe Yılmaz'], ['mehmet.kaya', 'Mehmet Kaya'], ['zeynep.demir', 'Zeynep Demir'],
    ['can.ozturk', 'Can Öztürk'], ['elif.sahin', 'Elif Şahin'], ['burak.celik', 'Burak Çelik']];
  const ids = {};
  for (const [sam, name] of people) {
    const user = await api('/users/ad', token, 'POST', { samAccountName: sam, displayName: name,
      domainName: 'example.test', userPrincipalName: `${sam}@example.test`, isAdmin: false });
    ids[sam] = user.id;
  }
  await api(`/users/${ids['zeynep.demir']}/role`, token, 'PUT', { isAdmin: false, canAccessAdminConsole: true });
  await api('/admin/access-groups', token, 'POST', { groupName: 'vdi-admins', domainName: 'example.test' });
  const machines = [
    ['VDI-FIN-01', '10.20.30.11', ['vdi-finance'], ['ayse.yilmaz']],
    ['VDI-FIN-02', '10.20.30.12', ['vdi-finance'], []],
    ['VDI-ENG-01', '10.20.40.21', ['vdi-engineering'], ['mehmet.kaya', 'can.ozturk']],
    ['VDI-ENG-02', '10.20.40.22', ['vdi-engineering'], []],
    ['VDI-HR-01', '10.20.50.31', ['vdi-hr'], ['elif.sahin']],
    ['LAB-WIN11-01', '10.20.60.41', [], ['burak.celik', 'zeynep.demir']]];
  for (const [name, ip, groups, users] of machines) {
    await api('/machines', token, 'POST', { name, ipAddress: ip, rdpHostName: `${name.toLowerCase()}.example.test`,
      rdpPort: 3389, entitledUserIds: users.map(sam => ids[sam]),
      entitledAdGroups: groups.map(groupName => ({ groupName, domainName: 'example.test' })) });
  }
}

const children = [];
let browser;
(async () => {
  try {
    children.push(spawn('dotnet', [dll, '--urls', 'http://127.0.0.1:5501'], { cwd: backend, env, windowsHide: true, stdio: 'ignore' }));
    children.push(spawn('dotnet', [path.join(portal, 'bin/Release/net10.0/BeRdp.AdminConsole.dll'), '--urls', 'http://127.0.0.1:5517'],
      { cwd: portal, env: { ...env, AdminConsole__BackendBaseUrl: 'http://127.0.0.1:5501' }, windowsHide: true, stdio: 'ignore' }));
    await Promise.all([ready('http://127.0.0.1:5501/health'), ready('http://127.0.0.1:5517/health')]);
    const login = await api('/login', null, 'POST', { username: 'admin', password });
    await seed(login.token);
    fs.mkdirSync(output, { recursive: true });
    browser = await playwright.chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE || undefined });
    for (const theme of ['light', 'dark']) {
      const suffix = theme === 'dark' ? '-dark' : '';
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: theme });
      await context.addInitScript(value => localStorage.setItem('berdp-admin-theme', value), theme);
      // Synthetic 10.20.x.x addresses cannot be probed from a workstation; show a
      // realistic mix of reachable desktops instead of an all-offline inventory.
      await context.route('**/admin/machines?*', async route => {
        const response = await route.fetch();
        const body = await response.json();
        body.items = body.items.map((item, index) => ({ ...item, isOnline: index !== 3 }));
        await route.fulfill({ response, json: body });
      });
      const page = await context.newPage();
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      await page.goto('http://127.0.0.1:5517');
      await page.screenshot({ path: path.join(output, `login${suffix}.png`) });
      await page.locator('#usernameInput').fill('admin');
      await page.locator('#passwordInput').fill(password);
      await page.locator('#loginForm button[type=submit]').click();
      await page.locator('#dashboardView').waitFor({ state: 'visible' });
      await page.locator('#totalMachinesStat').filter({ hasText: /^6$/ }).waitFor();
      await page.screenshot({ path: path.join(output, `overview${suffix}.png`) });
      await page.locator('[data-page=machines]').click();
      await page.locator('#machinesList').getByText('VDI-ENG-01', { exact: true }).waitFor();
      await page.screenshot({ path: path.join(output, `machines${suffix}.png`) });
      await page.locator('[data-page=authorization]').click();
      await page.locator('#authorizationUsersList').getByText('Zeynep Demir').waitFor();
      await page.screenshot({ path: path.join(output, `authorization${suffix}.png`) });
      await page.locator('[data-page=users]').click();
      await page.locator('#usersList').getByText('Ayşe Yılmaz').waitFor();
      await page.screenshot({ path: path.join(output, `users${suffix}.png`) });
      if (errors.length) throw new Error('Page errors: ' + errors.join('; '));
      if (theme === 'light') {
        await page.setViewportSize({ width: 390, height: 844 });
        await page.locator('#logoutButton').click();
        await page.screenshot({ path: path.join(output, 'login-mobile.png') });
      }
      await context.close();
    }
    console.log('SUCCESS: screenshots written to ' + output);
  } finally {
    if (browser) await browser.close();
    for (const child of children) child.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
