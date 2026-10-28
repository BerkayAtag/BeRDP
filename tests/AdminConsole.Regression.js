window.runBeRdpAdminRegression = async function () {
  const originalFetch = window.fetch;
  const originalOpen = window.open;
  const calls = [];
  const results = [];
  let mode = "success";
  let resolvePending;
  const check = (condition, name) => {
    if (!condition) throw new Error(`FAIL: ${name}`);
    results.push(`PASS: ${name}`);
  };
  const reply = (status, body) => new Response(JSON.stringify(body), {
    status, headers: { "Content-Type": "application/json" }
  });
  window.fetch = async (url, options) => {
    const address = new URL(url);
    const path = address.pathname;
    calls.push({ path, query: address.searchParams, method: options.method,
      authorized: !!options.headers.Authorization, credentials: options.credentials, body: options.body });
    if (mode === "pending") return new Promise(resolve => { resolvePending = resolve; });
    if (mode === "timeout") return new Promise((resolve, reject) => {
      options.signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError")), { once: true });
    });
    if (path === "/login/windows" && mode === "sso-disabled") {
      return reply(503, { message: "Sunucuda Windows SSO etkin değil." });
    }
    if (path === "/login/windows" || (path === "/login" && mode === "domain-user")) return reply(200, {
      userId: "regression-domain", username: "aknet.akb\\sam",
      displayName: "Domain User", isAdmin: false, canAccessAdminConsole: true, consoleRole: "Standard", isActiveDirectoryUser: true, token: "domain-only-token"
    });
    if (path === "/login") return reply(200, {
      userId: "regression-admin", username: "admin", displayName: "Regression Admin",
      isAdmin: mode !== "local-user", canAccessAdminConsole: mode !== "local-user", consoleRole: "Admin", isActiveDirectoryUser: false, token: "regression-only-token"
    });
    if (path === "/machines") return reply(200, [{
      id: "regression-machine", name: "<VM & Domain>", ipAddress: "192.0.2.10",
      rdpHostName: "vm01.aknet.akb", rdpPort: 3389, isOnline: true
    }]);
    if (path === "/admin/machines/regression-machine") return reply(200, {
      name: "Test VM", ipAddress: "192.0.2.10", rdpHostName: "vm01.aknet.akb", rdpPort: 3389,
      entitledUsers: [{ userId: "regression-domain", username: "sam@aknet.akb", displayName: "Domain User" }],
      entitledAdGroups: [{ groupName: "vdi", domainName: "aknet.akb" }]
    });
    if (path === "/machines/regression-machine") return reply(200, { id: "regression-machine" });
    if (path === "/machines/regression-machine/browser-session") {
      if (mode === "gateway-disabled") return reply(503, { message: "Guacamole gateway yapılandırılmalıdır." });
      if (mode === "gateway-revoked") return reply(404, { message: "Erişim atamanız kaldırılmış." });
      return reply(200, { launchUrl: mode === "gateway-invalid"
        ? "http://insecure.invalid/?data=test" : "https://gateway.invalid/guacamole/?data=test-only-ticket" });
    }
    if (path === "/ad/test" && mode === "ldap-error") {
      return reply(400, { message: "LDAP Container bulunamadı." });
    }
    if (path === "/ad/test" && mode === "forbidden") {
      return reply(403, { code: "admin_required", message: "Yönetici yetkisi kaldırıldı." });
    }
    if (path === "/ad/test" && mode === "expired") {
      return reply(401, { code: "session_expired", message: "Oturum iptal edildi." });
    }
    if (path === "/ad/test") return reply(200, { message: "Regresyon bağlantısı başarılı." });
    if (path === "/account/password") return reply(200, { message: "Parola değiştirildi. Yeniden giriş yapın." });
    if (path === "/ad/config") return reply(200, {
      enabled: true, domainName: "aknet.akb", ldapContainer: "OU=BTPersonel,OU=Aknet,DC=aknet,DC=akb",
      useMachineCredentials: true, hasBindPassword: false
    });
    if (path === "/admin/access-groups") return reply(200, []);
    if (path === "/admin/users") return reply(200, {
      items: [{ id: "regression-domain", username: "sam@aknet.akb", displayName: "Domain User",
        isActiveDirectoryUser: true, isAdmin: false, domainName: "aknet.akb" }],
      totalCount: 1, page: 1, pageSize: 100
    });
    if (path === "/admin/machines") return reply(200, {
      items: [{ id: "regression-machine", name: "Test VM", ipAddress: "192.0.2.10",
        rdpHostName: "vm01.aknet.akb", rdpPort: 3389, isOnline: true }],
      totalCount: 1, page: 1, pageSize: 150
    });
    return reply(200, { items: [], totalCount: 0, page: 1, pageSize: path === "/admin/machines" ? 150 : 100 });
  };
  const loginAdmin = async () => {
    mode = "success";
    ui.usernameInput.value = "admin";
    ui.passwordInput.value = "regression-only-password";
    await onLoginSubmit({ preventDefault() {} });
  };
  try {
    await loginAdmin();
    check(!!state.token && !ui.dashboardView.classList.contains("hidden"), "Admin login opens the dashboard");
    check(ui.passwordInput.value === "", "Login password is cleared");
    check(calls.filter(call => call.path !== "/login").every(call => call.authorized), "Protected requests carry Authorization");
    check(ui.adDomainInput.value === "aknet.akb" && ui.adContainerInput.value.includes("BTPersonel"), "AD settings populate correctly");
    check(ui.adBindPasswordInput.disabled, "Machine credentials disable bind inputs");
    await onTestAdConfig();
    check(ui.adConfigInfo.textContent === "Regresyon bağlantısı başarılı.", "AD test success is surfaced");
    mode = "ldap-error";
    await onTestAdConfig();
    check(ui.globalError.textContent === "LDAP Container bulunamadı." &&
      ui.adConfigInfo.textContent === "LDAP Container bulunamadı." && !ui.testAdConfigButton.disabled,
    "LDAP error is explicit and test button recovers");
    mode = "forbidden";
    await onTestAdConfig();
    check(state.token === null && !ui.loginView.classList.contains("hidden") &&
      ui.loginError.textContent === "Yönetici yetkisi kaldırıldı.", "Revoked admin privileges return to login");
    await loginAdmin();
    mode = "expired";
    await onTestAdConfig();
    check(state.token === null && ui.loginError.textContent === "Oturum iptal edildi.", "Expired token returns to login");
    await loginAdmin();
    ui.currentPasswordInput.value = "regression-only-password";
    ui.newAccountPasswordInput.value = "new-regression-only-password";
    await onChangePassword({ preventDefault() {} });
    check(state.token === null && ui.newAccountPasswordInput.value === "" &&
      ui.loginError.textContent.includes("Parola değiştirildi"), "Password change clears secrets and ends session");
    await loginAdmin();
    mode = "pending";
    const pending = apiRequest("/ad/test", { method: "POST", body: {} });
    onLogout();
    resolvePending(reply(200, { message: "stale" }));
    let staleRejected = false;
    try { await pending; } catch (error) { staleRejected = error instanceof SessionChangedError; }
    check(staleRejected && state.token === null, "Response from a previous session is ignored");
    await loginAdmin();
    mode = "timeout";
    const originalSetTimeout = window.setTimeout;
    let observedTimeout;
    window.setTimeout = (callback, delay) => {
      observedTimeout = delay;
      return originalSetTimeout(callback, 0);
    };
    try {
      await onTestAdConfig();
      check(observedTimeout === 30000 && ui.globalError.textContent.includes("30 saniyede") &&
        !ui.testAdConfigButton.disabled, "Request uses a 30000ms budget and recovers after timeout");
    } finally {
      window.setTimeout = originalSetTimeout;
    }
    onLogout();
    mode = "domain-user";
    ui.usernameInput.value = "sam@aknet.akb";
    ui.passwordInput.value = "regression-only-password";
    const beforeDomain = calls.length;
    await onLoginSubmit({ preventDefault() {} });
    check(state.activePage === "desktops" && !!state.token, "Standard domain user opens assigned desktops");
    check(calls.slice(beforeDomain).every(call => !call.path.startsWith("/admin/") && !call.path.startsWith("/ad/")),
      "Domain portal does not request admin APIs");
    check([...document.querySelectorAll("[data-admin-only]")].every(element => element.classList.contains("hidden")),
      "Domain portal hides all admin navigation");
    check(ui.desktopList.textContent.includes("<VM & Domain>") && !ui.desktopList.querySelector("vm"),
      "Machine names are rendered as text, not HTML");
    check(calls.at(-1).query.get("assignedOnly") === "false", "Read-only console requests full inventory");
    setActivePage("local");
    check(state.activePage === "desktops", "Non-admin cannot navigate to local maintenance");
    onLogout();
    check(ui.desktopList.children.length === 0, "Logout removes assigned machine information");
    mode = "success";
    await login(true);
    const windowsCall = calls.findLast(call => call.path === "/login/windows");
    check(state.profile.isActiveDirectoryUser && windowsCall.credentials === "include" && windowsCall.body === undefined,
      "Windows browser login sends integrated credentials without a password payload");
    onLogout();
    mode = "sso-disabled";
    await login(true);
    check(ui.loginError.textContent.includes("SSO etkin değil") && !ui.windowsLoginButton.disabled,
      "Disabled SSO reports the server error and recovers");
    mode = "local-user";
    await onLoginSubmit({ preventDefault() {} });
    check(state.token === null && ui.loginError.textContent.includes("yönetim konsoluna erişemez"),
      "Standard local user is not offered a domain workspace");
    await loginAdmin();
    check(ui.machinesList.textContent.includes("Makine ve Erişimleri Düzenle") &&
      !ui.usersList.textContent.includes("Makine ve Erişimleri Düzenle"),
      "Machine editor is attached to machine cards, not user cards");
    await editMachine("regression-machine");
    check(state.selectedMachineUsers.size === 1 && state.selectedMachineGroups.size === 1 &&
      ui.newMachineRdpHostName.value === "vm01.aknet.akb", "Editor loads DNS and existing entitlements");
    await onCreateMachine({ preventDefault() {} });
    const update = calls.findLast(call => call.path === "/machines/regression-machine");
    check(update.method === "PUT" && JSON.parse(update.body).entitledUserIds[0] === "regression-domain",
      "Saving an existing machine updates it instead of creating a duplicate");
    check(state.editingMachineId === null && state.selectedMachineUsers.size === 0 &&
      ui.cancelMachineEditButton.classList.contains("hidden"), "Machine editor clears after save");
    const ids = [...document.querySelectorAll("[id]")].map(element => element.id);
    check(new Set(ids).size === ids.length &&
      ui.createUserForm.closest(".page").id === "page-local", "Local maintenance is separate and element IDs are unique");
    mode = "pending";
    const pendingDesktop = loadDesktops();
    const resolveOldDesktop = resolvePending;
    onLogout();
    mode = "domain-user";
    ui.usernameInput.value = "sam@aknet.akb";
    ui.passwordInput.value = "test-only";
    await onLoginSubmit({ preventDefault() {} });
    check(ui.desktopList.textContent.includes("<VM & Domain>"),
      "Previous session's pending request does not block the new user's desktop list");
    resolveOldDesktop(reply(200, [{ name: "Previous user's desktop" }]));
    let oldDesktopRejected = false;
    try { await pendingDesktop; } catch (error) { oldDesktopRejected = error instanceof SessionChangedError; }
    check(oldDesktopRejected && !ui.desktopList.textContent.includes("Previous user's desktop"),
      "Previous user's desktop response cannot overwrite the current workspace");
    onLogout();
    mode = "pending";
    const beforeDoubleLogin = calls.length;
    const firstLogin = onLoginSubmit({ preventDefault() {} });
    await login(true);
    check(calls.length === beforeDoubleLogin + 1,
      "Password login and Windows SSO cannot submit concurrently");
    resolvePending(reply(200, {
      userId: "regression-domain", username: "sam@aknet.akb", displayName: "Domain",
      isAdmin: false, canAccessAdminConsole: true, consoleRole: "Standard", isActiveDirectoryUser: true, token: "test-only-token"
    }));
    mode = "domain-user";
    await firstLogin;
    await loginAdmin();
    await editMachine("regression-machine");
    mode = "pending";
    const beforeDoubleSave = calls.length;
    const firstSave = onCreateMachine({ preventDefault() {} });
    await onCreateMachine({ preventDefault() {} });
    check(calls.length === beforeDoubleSave + 1,
      "Double click on save sends only one machine update");
    resolvePending(reply(200, { id: "regression-machine" }));
    mode = "success";
    await firstSave;
    const popup = { closed: false, opener: window, location: { replace(url) { this.destination = url; } },
      close() { this.closed = true; } };
    window.open = () => popup;
    const connectButton = document.createElement("button");
    mode = "success";
    await launchBrowserSession("regression-machine", connectButton);
    check(popup.location.destination === "https://gateway.invalid/guacamole/?data=test-only-ticket" &&
      popup.opener === null && !connectButton.disabled,
      "Browser launch requests authorization then opens the HTTPS gateway without an opener");
    const launchCall = calls.findLast(call => call.path.endsWith("/browser-session"));
    check(launchCall.method === "POST" && launchCall.authorized && launchCall.body === undefined,
      "Browser launch sends no Windows password or arbitrary target address");
    for (const testMode of ["gateway-disabled", "gateway-revoked", "gateway-invalid"]) {
      mode = testMode;
      popup.closed = false;
      await launchBrowserSession("regression-machine", connectButton);
      check(popup.closed && ui.globalError.textContent.length > 0 && !connectButton.disabled,
        `${testMode} closes the blank popup and displays an error`);
    }
    window.open = () => null;
    const beforePopup = calls.length;
    await launchBrowserSession("regression-machine", connectButton);
    check(calls.length === beforePopup && ui.globalError.textContent.includes("engellendi"),
      "Blocked popup does not create an unused gateway ticket");
    popup.closed = false;
    popup.location.destination = undefined;
    window.open = () => popup;
    mode = "pending";
    const pendingLaunch = launchBrowserSession("regression-machine", connectButton);
    onLogout();
    resolvePending(reply(200, { launchUrl: "https://gateway.invalid/guacamole/?data=stale-ticket" }));
    await pendingLaunch;
    check(popup.closed && popup.location.destination === undefined,
      "Logging out while authorizing a gateway launch prevents stale ticket navigation");
    return { passed: results.length, results };
  } finally {
    onLogout();
    window.fetch = originalFetch;
    window.open = originalOpen;
  }
};
