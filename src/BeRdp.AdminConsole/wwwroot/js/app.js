const config = window.__APP_CONFIG__ ?? {};
const backendBaseUrl = (config.backendBaseUrl ?? "").replace(/\/$/, "");
const THEME_STORAGE_KEY = "berdp-admin-theme";

const PAGE_TITLES = {
  desktops: "Masaüstlerim",
  local: "Yerel Hesap Bakımı",
  overview: "Genel Bakış",
  users: "Kullanıcılar",
  authorization: "Yetkilendirme",
  machines: "Makine & Entitlement",
  ad: "AD Entegrasyon",
  firewall: "Ağ Erişim Kuralları",
  installation: "Sessiz Kurulum & Kaldırma"
};

const ui = {
  loginView: document.getElementById("loginView"),
  dashboardView: document.getElementById("dashboardView"),
  loginForm: document.getElementById("loginForm"),
  windowsLoginButton: document.getElementById("windowsLoginButton"),
  desktopList: document.getElementById("desktopList"),
  desktopsPrevButton: document.getElementById("desktopsPrevButton"),
  desktopsNextButton: document.getElementById("desktopsNextButton"),
  desktopsPageInfo: document.getElementById("desktopsPageInfo"),
  desktopsFilterInput: document.getElementById("desktopsFilterInput"),
  applyDesktopsFilterButton: document.getElementById("applyDesktopsFilterButton"),
  usernameInput: document.getElementById("usernameInput"),
  passwordInput: document.getElementById("passwordInput"),
  loginError: document.getElementById("loginError"),
  globalError: document.getElementById("globalError"),
  adminInfo: document.getElementById("adminInfo"),
  pageTitle: document.getElementById("pageTitle"),
  downloadClientButton: document.getElementById("downloadClientButton"),
  navLinks: Array.from(document.querySelectorAll(".nav-link")),
  pages: {
    desktops: document.getElementById("page-desktops"),
    local: document.getElementById("page-local"),
    overview: document.getElementById("page-overview"),
    users: document.getElementById("page-users"),
    authorization: document.getElementById("page-authorization"),
    machines: document.getElementById("page-machines"),
    ad: document.getElementById("page-ad"),
    firewall: document.getElementById("page-firewall"),
    installation: document.getElementById("page-installation")
  },

  refreshButton: document.getElementById("refreshButton"),
  logoutButton: document.getElementById("logoutButton"),
  themeToggleButtons: Array.from(document.querySelectorAll("[data-theme-toggle]")),

  totalUsersStat: document.getElementById("totalUsersStat"),
  // Overview counts every user, independent of the Users page source filter.
  totalMachinesStat: document.getElementById("totalMachinesStat"),
  adminGroupsStat: document.getElementById("adminGroupsStat"),
  adStatusStat: document.getElementById("adStatusStat"),

  createUserForm: document.getElementById("createUserForm"),
  newUsername: document.getElementById("newUsername"),
  newDisplayName: document.getElementById("newDisplayName"),
  newPassword: document.getElementById("newPassword"),
  usersFilterInput: document.getElementById("usersFilterInput"),
  usersSourceFilter: document.getElementById("usersSourceFilter"),
  applyUsersFilterButton: document.getElementById("applyUsersFilterButton"),
  usersPrevPageButton: document.getElementById("usersPrevPageButton"),
  usersNextPageButton: document.getElementById("usersNextPageButton"),
  usersPageInfo: document.getElementById("usersPageInfo"),
  usersList: document.getElementById("usersList"),

  authUsersFilterInput: document.getElementById("authUsersFilterInput"),
  applyAuthUsersFilterButton: document.getElementById("applyAuthUsersFilterButton"),
  authUsersPrevPageButton: document.getElementById("authUsersPrevPageButton"),
  authUsersNextPageButton: document.getElementById("authUsersNextPageButton"),
  authUsersPageInfo: document.getElementById("authUsersPageInfo"),
  authorizationUsersList: document.getElementById("authorizationUsersList"),
  authGroupSearchForm: document.getElementById("authGroupSearchForm"),
  authGroupSearchInput: document.getElementById("authGroupSearchInput"),
  authGroupSearchResults: document.getElementById("authGroupSearchResults"),
  adminAccessGroupsList: document.getElementById("adminAccessGroupsList"),

  createMachineForm: document.getElementById("createMachineForm"),
  cancelMachineEditButton: document.getElementById("cancelMachineEditButton"),
  newMachineName: document.getElementById("newMachineName"),
  newMachineIp: document.getElementById("newMachineIp"),
  newMachineRdpHostName: document.getElementById("newMachineRdpHostName"),
  newMachinePort: document.getElementById("newMachinePort"),
  machineCreateInfo: document.getElementById("machineCreateInfo"),
  machineUserSearchForm: document.getElementById("machineUserSearchForm"),
  machineUserSearchInput: document.getElementById("machineUserSearchInput"),
  machineUserSearchResults: document.getElementById("machineUserSearchResults"),
  selectedMachineUsers: document.getElementById("selectedMachineUsers"),
  machineGroupSearchForm: document.getElementById("machineGroupSearchForm"),
  machineGroupSearchInput: document.getElementById("machineGroupSearchInput"),
  machineGroupSearchResults: document.getElementById("machineGroupSearchResults"),
  selectedMachineGroups: document.getElementById("selectedMachineGroups"),
  machinesFilterInput: document.getElementById("machinesFilterInput"),
  applyMachinesFilterButton: document.getElementById("applyMachinesFilterButton"),
  machinesPrevPageButton: document.getElementById("machinesPrevPageButton"),
  machinesNextPageButton: document.getElementById("machinesNextPageButton"),
  machinesPageInfo: document.getElementById("machinesPageInfo"),
  machinesList: document.getElementById("machinesList"),

  adConfigForm: document.getElementById("adConfigForm"),
  adEnabledInput: document.getElementById("adEnabledInput"),
  adDomainInput: document.getElementById("adDomainInput"),
  adContainerInput: document.getElementById("adContainerInput"),
  adUseMachineCredentialsInput: document.getElementById("adUseMachineCredentialsInput"),
  adBindUsernameInput: document.getElementById("adBindUsernameInput"),
  adBindPasswordInput: document.getElementById("adBindPasswordInput"),
  testAdConfigButton: document.getElementById("testAdConfigButton"),
  adConfigInfo: document.getElementById("adConfigInfo"),
  adUserSearchForm: document.getElementById("adUserSearchForm"),
  adUserSearchInput: document.getElementById("adUserSearchInput"),
  adSearchResults: document.getElementById("adSearchResults")
};
ui.changePasswordForm = document.getElementById("changePasswordForm");
ui.currentPasswordInput = document.getElementById("currentPasswordInput");
ui.newAccountPasswordInput = document.getElementById("newAccountPasswordInput");
ui.passwordSettingsCard = document.getElementById("passwordSettingsCard");

const state = {
  editingMachineId: null,
  savingMachine: false,
  refreshTimer: null,
  loggingIn: false,
  desktops: { page: 1, pageSize: 50, loadingGeneration: null, search: "" },
  token: null,
  sessionGeneration: 0,
  profile: null,
  activePage: "overview",
  totalUserCount: 0,
  isConsoleAdmin: false,
  adConfig: null,
  adUserSearchResults: [],
  authGroupSearchResults: [],
  machineUserSearchResults: [],
  machineGroupSearchResults: [],
  adminAccessGroups: [],
  selectedMachineUsers: new Map(),
  selectedMachineGroups: new Map(),
  users: {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 100,
    search: "",
    source: "ad"
  },
  authUsers: {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 100,
    search: ""
  },
  machines: {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 150,
    search: ""
  }
};

initialize();

function initialize() {
  applyTheme(loadSavedTheme());

  ui.navLinks.forEach(link => {
    link.addEventListener("click", () => {
      const pageName = link.dataset.page;
      if (!pageName || !ui.pages[pageName]) {
        return;
      }

      setActivePage(pageName);
      if (pageName === "desktops") {
        runSafely(() => loadDesktops());
      }
    });
  });

  ui.loginForm.addEventListener("submit", onLoginSubmit);
  ui.windowsLoginButton.addEventListener("click", () => login(true));
  ui.desktopsPrevButton.addEventListener("click", () => runSafely(() => loadDesktops(state.desktops.page - 1)));
  ui.desktopsNextButton.addEventListener("click", () => runSafely(() => loadDesktops(state.desktops.page + 1)));
  ui.changePasswordForm.addEventListener("submit", onChangePassword);
  ui.logoutButton.addEventListener("click", onLogout);
  ui.refreshButton.addEventListener("click", () => runSafely(refreshAll));
  ui.themeToggleButtons.forEach(button => button.addEventListener("click", onToggleTheme));

  ui.downloadClientButton.addEventListener("click", onDownloadClientSetup);

  ui.createUserForm.addEventListener("submit", onCreateLocalUser);
  ui.applyUsersFilterButton.addEventListener("click", () => runSafely(applyUsersFilter));
  ui.usersPrevPageButton.addEventListener("click", () => runSafely(() => changePage(state.users, -1, loadUsers)));
  ui.usersNextPageButton.addEventListener("click", () => runSafely(() => changePage(state.users, 1, loadUsers)));

  ui.applyAuthUsersFilterButton.addEventListener("click", () => runSafely(applyAuthUsersFilter));
  ui.authUsersPrevPageButton.addEventListener("click", () => runSafely(() => changePage(state.authUsers, -1, loadAuthUsers)));
  ui.authUsersNextPageButton.addEventListener("click", () => runSafely(() => changePage(state.authUsers, 1, loadAuthUsers)));
  ui.authGroupSearchForm.addEventListener("submit", onSearchAuthorizationAdGroups);

  ui.createMachineForm.addEventListener("submit", onCreateMachine);
  ui.cancelMachineEditButton.addEventListener("click", () => {
    if (!state.savingMachine) resetMachineEditor();
  });
  ui.machineUserSearchForm.addEventListener("submit", onSearchMachineUsers);
  ui.machineGroupSearchForm.addEventListener("submit", onSearchMachineGroups);
  ui.applyMachinesFilterButton.addEventListener("click", () => runSafely(applyMachinesFilter));
  ui.machinesPrevPageButton.addEventListener("click", () => runSafely(() => changePage(state.machines, -1, loadMachines)));
  ui.machinesNextPageButton.addEventListener("click", () => runSafely(() => changePage(state.machines, 1, loadMachines)));

  ui.adConfigForm.addEventListener("submit", onSaveAdConfig);
  ui.testAdConfigButton.addEventListener("click", onTestAdConfig);
  ui.adUseMachineCredentialsInput.addEventListener("change", syncAdCredentialInputsState);
  ui.adUserSearchForm.addEventListener("submit", onSearchAdUsers);

  // Desktop filter
  ui.applyDesktopsFilterButton.addEventListener("click", () => runSafely(applyDesktopsFilter));

  // Enter key support in all filter rows / search textboxes (Requirement: Enter key triggers button click)
  document.querySelectorAll(".filter-row input").forEach(input => {
    input.addEventListener("keydown", onFilterRowEnterKeyDown);
  });

  syncAdCredentialInputsState();
  renderSelectedMachineUsers();
  renderSelectedMachineGroups();
}

function onFilterRowEnterKeyDown(event) {
  if (event.key !== "Enter") return;
  const input = event.target;
  const filterRow = input.closest(".filter-row");
  if (!filterRow) return;
  event.preventDefault();
  // Find the button in the filter row
  const button = filterRow.querySelector("button");
  if (button) {
    button.click();
  }
}

function onDownloadClientSetup() {
  window.location.href = `${backendBaseUrl}/download/client-setup`;
}

async function onLoginSubmit(event) {
  event.preventDefault();
  await login(false);
}

async function login(windowsSso) {
  if (state.loggingIn) return;
  state.loggingIn = true;
  ui.loginError.textContent = "";
  const loginButton = ui.loginForm.querySelector("button");
  loginButton.disabled = true;
  ui.windowsLoginButton.disabled = true;

  const username = ui.usernameInput.value.trim();
  const password = ui.passwordInput.value;

  try {
    const loginResult = await apiRequest(windowsSso ? "/login/windows" : "/login", {
      method: "POST",
      authRequired: false,
      credentials: windowsSso ? "include" : "omit",
      body: windowsSso ? undefined : { username, password }
    });

    if (!loginResult.canAccessAdminConsole) {
      throw new Error("Bu hesap yönetim konsoluna erişemez. Masaüstü istemcisinden giriş yapın.");
    }
    if (typeof loginResult.token !== "string" || loginResult.token.length === 0) {
      throw new Error("Sunucu geçerli bir oturum tokenı döndürmedi.");
    }

    state.sessionGeneration++;
    state.token = loginResult.token;
    state.profile = loginResult;
    // The API serializes the enum as "Admin"/"Standard"; accept the legacy numeric
    // value too so an upgraded console works against a not-yet-upgraded backend.
    state.isConsoleAdmin = loginResult.consoleRole === "Admin" || loginResult.consoleRole === 1;
    ui.adminInfo.textContent = `${loginResult.displayName} (${loginResult.username})`;
    ui.loginView.classList.add("hidden");
    ui.dashboardView.classList.remove("hidden");
    ui.passwordSettingsCard.classList.toggle("hidden", !!loginResult.isActiveDirectoryUser);
    ui.passwordInput.value = "";

    // Handle role-based UI visibility
    const isAdmin = state.isConsoleAdmin;
    document.querySelectorAll("[data-admin-only]").forEach(element =>
      element.classList.toggle("hidden", !isAdmin));
    document.querySelectorAll("[data-console-admin-only]").forEach(element =>
      element.classList.toggle("hidden", !state.isConsoleAdmin));
    document.querySelectorAll("[data-console-read-only]").forEach(element =>
      element.classList.toggle("hidden", state.isConsoleAdmin));

    // Disable buttons/forms for read-only users
    if (!state.isConsoleAdmin) {
      disableWriteActions();
    }

    setActivePage(isAdmin ? "overview" : "desktops");
    state.refreshTimer = window.setInterval(() => {
      if (state.token && state.activePage === "desktops" && !document.hidden) {
        runSafely(() => loadDesktops());
      }
    }, 30000);

    await runSafely(refreshAll);
  } catch (error) {
    ui.loginError.textContent = getErrorMessage(error);
  } finally {
    loginButton.disabled = false;
    ui.windowsLoginButton.disabled = false;
    state.loggingIn = false;
  }
}

function disableWriteActions() {
  // Disable all write actions for read-only users
  document.querySelectorAll("button[data-write-action]").forEach(btn => {
    btn.disabled = true;
    btn.title = "Salt okunur mod - düzenleme yetkisi yok";
  });
  document.querySelectorAll("input[data-write-action], select[data-write-action], textarea[data-write-action]").forEach(input => {
    input.disabled = true;
    input.title = "Salt okunur mod";
  });
  document.querySelectorAll("form[data-write-action]").forEach(form => {
    form.addEventListener("submit", (e) => e.preventDefault());
  });
}

function onLogout() {
  window.clearInterval(state.refreshTimer);
  state.refreshTimer = null;
  state.sessionGeneration++;
  state.token = null;
  state.profile = null;
  state.isConsoleAdmin = false;
  state.adConfig = null;
  state.adUserSearchResults = [];
  state.authGroupSearchResults = [];
  state.machineUserSearchResults = [];
  state.machineGroupSearchResults = [];
  state.adminAccessGroups = [];
  state.selectedMachineUsers.clear();
  state.selectedMachineGroups.clear();
  resetMachineEditor();
  resetPageState();
  clearDynamicLists();
  renderSelectedMachineUsers();
  renderSelectedMachineGroups();
  ui.usersFilterInput.value = "";
  ui.usersSourceFilter.value = "ad";
  ui.desktopList.replaceChildren();
  state.desktops.page = 1;
  state.desktops.search = "";
  ui.desktopsFilterInput.value = "";
  state.totalUserCount = 0;
  ui.createUserForm.reset();
  document.querySelectorAll("[data-admin-only]").forEach(element => element.classList.add("hidden"));
  document.querySelectorAll("[data-console-admin-only]").forEach(element => element.classList.add("hidden"));
  document.querySelectorAll("[data-console-read-only]").forEach(element => element.classList.remove("hidden"));
  ui.authUsersFilterInput.value = "";
  ui.machinesFilterInput.value = "";
  ui.machineUserSearchInput.value = "";
  ui.machineGroupSearchInput.value = "";
  ui.authGroupSearchInput.value = "";
  ui.adUserSearchInput.value = "";

  ui.passwordInput.value = "";
  ui.changePasswordForm.reset();
  ui.adBindPasswordInput.value = "";
  ui.adConfigInfo.textContent = "";
  ui.globalError.textContent = "";
  ui.dashboardView.classList.add("hidden");
  ui.loginView.classList.remove("hidden");
}

async function onChangePassword(event) {
  event.preventDefault();
  await runSafely(async () => {
    const result = await apiRequest("/account/password", {
      method: "POST",
      body: {
        currentPassword: ui.currentPasswordInput.value,
        newPassword: ui.newAccountPasswordInput.value
      }
    });
    onLogout();
    ui.loginError.textContent = result.message;
  });
}

function resetPageState() {
  state.users.page = 1;
  state.users.search = "";
  state.users.source = "ad";
  state.authUsers.page = 1;
  state.authUsers.search = "";
  state.machines.page = 1;
  state.machines.search = "";
}

function clearDynamicLists() {
  ui.usersList.innerHTML = "";
  ui.authorizationUsersList.innerHTML = "";
  ui.authGroupSearchResults.innerHTML = "";
  ui.adminAccessGroupsList.innerHTML = "";
  ui.machineUserSearchResults.innerHTML = "";
  ui.machineGroupSearchResults.innerHTML = "";
  ui.selectedMachineUsers.innerHTML = "";
  ui.selectedMachineGroups.innerHTML = "";
  ui.machinesList.innerHTML = "";
  ui.adSearchResults.innerHTML = "";
  ui.machineCreateInfo.textContent = "";
}

async function refreshAll() {
  if (!state.isConsoleAdmin || state.activePage === "desktops") {
    await loadDesktops();
    return;
  }
  await Promise.all([
    loadAdConfig(),
    loadUsers(),
    loadAuthUsers(),
    loadMachines(),
    loadAdminAccessGroups(),
    loadUserTotal()
  ]);

  renderOverview();
}

function setActivePage(pageName) {
  // Check if user has access to this page
  const isAdmin = state.isConsoleAdmin;

  // Admin-only pages
  const adminOnlyPages = ["local", "overview", "users", "authorization", "machines", "ad", "firewall", "installation"];
  const readOnlyPages = ["desktops"]; // Read-only users can only access this

  if (!state.isConsoleAdmin && adminOnlyPages.includes(pageName)) {
    pageName = "desktops";
  }

  state.activePage = pageName;
  Object.entries(ui.pages).forEach(([name, element]) => {
    element.classList.toggle("hidden", name !== pageName);
  });

  ui.navLinks.forEach(link => {
    link.classList.toggle("active", link.dataset.page === pageName);
  });

  ui.pageTitle.textContent = PAGE_TITLES[pageName] ?? "BeRDP Admin";
}

async function applyDesktopsFilter() {
  state.desktops.search = ui.desktopsFilterInput.value.trim();
  state.desktops.page = 1;
  await loadDesktops();
}

async function loadDesktops(page = state.desktops.page) {
  const generation = state.sessionGeneration;
  if (state.desktops.loadingGeneration === generation) return;
  state.desktops.loadingGeneration = generation;
  try {
    const requestedPage = Math.max(1, page);
    const isAdmin = state.isConsoleAdmin;

    // Admin users see all machines without assignedOnly filter
    // Read-only users see only their assigned machines
    const machines = await apiRequest(`/machines?${toQueryString({
      skip: (requestedPage - 1) * state.desktops.pageSize,
      take: state.desktops.pageSize,
      assignedOnly: false,
      search: state.desktops.search
    })}`);
    if (!Array.isArray(machines)) throw new Error("Sunucu geçerli bir masaüstü listesi döndürmedi.");
    state.desktops.page = requestedPage;
    ui.desktopList.replaceChildren();
    if (machines.length === 0) {
      ui.desktopList.appendChild(createHintElement("Makine bulunamadı."));
    }
    for (const machine of machines) {
      const card = document.createElement("article");
      card.className = "list-item";

      const topRow = document.createElement("div");
      topRow.className = "row wrap";

      const title = document.createElement("strong");
      title.textContent = machine.name;
      topRow.appendChild(title);

      const status = document.createElement("span");
      status.className = `status ${machine.isOnline ? "online" : "offline"}`;
      status.textContent = machine.isOnline ? "RDP erişilebilir" : "RDP erişilemiyor";
      status.title = "BeRDP sunucusundan RDP TCP portuna erişim. İstemci ağındaki erişim farklı olabilir.";
      topRow.appendChild(status);
      card.appendChild(topRow);

      const detail = document.createElement("div");
      detail.className = "hint";
      detail.textContent = `Makine: ${machine.name} | Domain: ${machine.rdpHostName ?? "-"} | IP: ${machine.ipAddress} | Port: ${machine.rdpPort} | Kullanıcı: ${machine.assignedUserCount ?? 0} | AD Grup: ${machine.assignedAdGroupCount ?? 0}`;
      card.appendChild(detail);

      if (machine.assignedUserDisplayNames && machine.assignedUserDisplayNames.length > 0) {
        const userDetail = document.createElement("div");
        userDetail.className = "hint";
        userDetail.textContent = `Atanmış kullanıcılar: ${machine.assignedUserDisplayNames.join(", ")}`;
        card.appendChild(userDetail);
      }

      if (machine.assignedUsernames && machine.assignedUsernames.length > 0) {
        const usernameDetail = document.createElement("div");
        usernameDetail.className = "hint";
        usernameDetail.textContent = `Kullanıcı adları: ${machine.assignedUsernames.join(", ")}`;
        card.appendChild(usernameDetail);
      }

      if (machine.assignedAdGroups && machine.assignedAdGroups.length > 0) {
        const adGroupDetail = document.createElement("div");
        adGroupDetail.className = "hint";
        adGroupDetail.textContent = `Atanmış AD grupları: ${machine.assignedAdGroups.join(", ")}`;
        card.appendChild(adGroupDetail);
      }

      // Connection button - only show if user has access
      if (!state.isConsoleAdmin) {
        const connect = document.createElement("button");
        connect.type = "button";
        connect.textContent = "Tarayıcı ile Bağlan";
        connect.addEventListener("click", () => launchBrowserSession(machine.id, connect));
        card.appendChild(connect);
      }

      ui.desktopList.appendChild(card);
    }
    ui.desktopsPageInfo.textContent = `Sayfa ${requestedPage} · ${machines.length} makine`;
    ui.desktopsPrevButton.disabled = requestedPage === 1;
    ui.desktopsNextButton.disabled = machines.length < state.desktops.pageSize;
  } finally {
    if (state.desktops.loadingGeneration === generation) {
      state.desktops.loadingGeneration = null;
    }
  }
}

async function launchBrowserSession(machineId, button) {
  if (button.disabled) return;
  button.disabled = true;
  // Open during the user gesture; asynchronous popup creation is blocked by browsers.
  const tab = window.open("about:blank", "_blank");
  try {
    if (!tab) throw new Error("Bağlantı penceresi engellendi. Bu site için açılır pencereye izin verin.");
    tab.opener = null;
    const result = await apiRequest(`/machines/${encodeURIComponent(machineId)}/browser-session`, { method: "POST" });
    const destination = new URL(result.launchUrl);
    if (destination.protocol !== "https:" || destination.username || destination.password) {
      throw new Error("Sunucu güvenli bir gateway adresi döndürmedi.");
    }
    if (tab.closed) throw new Error("Bağlantı penceresi kapatıldı. Yeniden deneyin.");
    tab.location.replace(destination.href);
  } catch (error) {
    if (tab && !tab.closed) tab.close();
    if (!(error instanceof SessionChangedError)) {
      (state.token ? ui.globalError : ui.loginError).textContent = getErrorMessage(error);
    }
  } finally { button.disabled = false; }
}

async function onCreateLocalUser(event) {
  event.preventDefault();
  await runSafely(async () => {
    await apiRequest("/users", {
      method: "POST",
      body: {
        username: ui.newUsername.value.trim(),
        displayName: ui.newDisplayName.value.trim(),
        password: ui.newPassword.value,
        isAdmin: false
      }
    });

    ui.createUserForm.reset();
    await Promise.all([loadUsers(), loadAuthUsers(), loadUserTotal()]);
    renderOverview();
  });
}

async function loadUserTotal() {
  const response = await apiRequest(`/admin/users?${toQueryString({ page: 1, pageSize: 10 })}`);
  state.totalUserCount = response.totalCount ?? 0;
}

async function loadUsers() {
  const response = await apiRequest(`/admin/users?${toQueryString({
    page: state.users.page,
    pageSize: state.users.pageSize,
    search: state.users.search,
    source: state.users.source
  })}`);

  state.users.items = response.items ?? [];
  state.users.totalCount = response.totalCount ?? 0;
  state.users.page = response.page ?? state.users.page;
  state.users.pageSize = response.pageSize ?? state.users.pageSize;
  renderUsers();
}

function renderUsers() {
  ui.usersList.innerHTML = "";
  const totalPages = calculateTotalPages(state.users.totalCount, state.users.pageSize);
  ui.usersPageInfo.textContent = `Sayfa ${state.users.page}/${totalPages} - Toplam: ${state.users.totalCount}`;
  ui.usersPrevPageButton.disabled = state.users.page <= 1;
  ui.usersNextPageButton.disabled = state.users.page >= totalPages;

  if (state.users.items.length === 0) {
    ui.usersList.appendChild(createHintElement("Kullanıcı bulunamadı."));
    return;
  }

  for (const user of state.users.items) {
    const card = document.createElement("article");
    card.className = "user-card";

    const topRow = document.createElement("div");
    topRow.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = `${user.displayName} (${user.username})`;
    topRow.appendChild(title);

    const badges = document.createElement("div");
    badges.className = "row wrap";
    badges.appendChild(makeBadge(user.isActiveDirectoryUser ? "AD" : "LOCAL", user.isActiveDirectoryUser ? "ad" : "local"));
    badges.appendChild(makeBadge(user.isAdmin ? "ADMIN" : "STANDARD", user.isAdmin ? "admin" : "standard"));
    topRow.appendChild(badges);
    card.appendChild(topRow);

    if (user.userPrincipalName || user.domainName) {
      const info = document.createElement("div");
      info.className = "hint";
      info.textContent = `Domain: ${user.domainName ?? "-"} | UPN: ${user.userPrincipalName ?? "-"}`;
      card.appendChild(info);
    }

    if (user.lastLoginAtUtc) {
      const lastLogin = document.createElement("div");
      lastLogin.className = "hint";
      lastLogin.textContent = `Son giriş: ${new Date(user.lastLoginAtUtc).toLocaleString("tr-TR")}`;
      card.appendChild(lastLogin);
    }

    if (state.profile?.userId !== user.id) {
      const actionRow = document.createElement("div");
      actionRow.className = "row";
      const deleteButton = document.createElement("button");
      deleteButton.type = "button";
      deleteButton.className = "danger";
      deleteButton.textContent = "Kullanıcıyı Sil";
      deleteButton.addEventListener("click", () => runSafely(async () => {
        if (!window.confirm(`${user.displayName} (${user.username}) silinsin mi? Bu kullanıcının bireysel makine atamaları da kaldırılır.`)) return;
        await deleteUser(user.id);
      }));
      actionRow.appendChild(deleteButton);
      card.appendChild(actionRow);
    }

    ui.usersList.appendChild(card);
  }
}

async function applyUsersFilter() {
  state.users.search = ui.usersFilterInput.value.trim();
  state.users.source = ui.usersSourceFilter.value;
  state.users.page = 1;
  await loadUsers();
}

async function deleteUser(userId) {
  await apiRequest(`/users/${userId}`, { method: "DELETE" });
  if (state.selectedMachineUsers.has(userId)) {
    state.selectedMachineUsers.delete(userId);
    renderSelectedMachineUsers();
  }

  await Promise.all([loadUsers(), loadAuthUsers(), loadUserTotal()]);
  renderOverview();
}

async function loadAuthUsers() {
  const response = await apiRequest(`/admin/users?${toQueryString({
    page: state.authUsers.page,
    pageSize: state.authUsers.pageSize,
    search: state.authUsers.search
  })}`);

  state.authUsers.items = response.items ?? [];
  state.authUsers.totalCount = response.totalCount ?? 0;
  state.authUsers.page = response.page ?? state.authUsers.page;
  state.authUsers.pageSize = response.pageSize ?? state.authUsers.pageSize;
  renderAuthorizationUsers();
}

function renderAuthorizationUsers() {
  ui.authorizationUsersList.innerHTML = "";
  const totalPages = calculateTotalPages(state.authUsers.totalCount, state.authUsers.pageSize);
  ui.authUsersPageInfo.textContent = `Sayfa ${state.authUsers.page}/${totalPages} - Toplam: ${state.authUsers.totalCount}`;
  ui.authUsersPrevPageButton.disabled = state.authUsers.page <= 1;
  ui.authUsersNextPageButton.disabled = state.authUsers.page >= totalPages;

  if (state.authUsers.items.length === 0) {
    ui.authorizationUsersList.appendChild(createHintElement("Kullanıcı bulunamadı."));
    return;
  }

  for (const user of state.authUsers.items) {
    const card = document.createElement("article");
    card.className = "list-item";

    const topRow = document.createElement("div");
    topRow.className = "row wrap";

    const userText = document.createElement("strong");
    userText.textContent = `${user.displayName} (${user.username})`;
    topRow.appendChild(userText);

    const badges = document.createElement("div");
    badges.className = "row wrap";
    badges.appendChild(makeBadge(user.isActiveDirectoryUser ? "AD" : "LOCAL", user.isActiveDirectoryUser ? "ad" : "local"));
    badges.appendChild(makeBadge(user.isAdmin ? "ADMIN" : "STANDARD", user.isAdmin ? "admin" : "standard"));
    badges.appendChild(makeBadge(
      user.isAdmin ? "KONSOL ADMİN" : user.canAccessAdminConsole ? "KONSOL STANDART" : "KONSOL YOK",
      user.isAdmin ? "admin" : user.canAccessAdminConsole ? "standard" : "local"));
    topRow.appendChild(badges);
    card.appendChild(topRow);

    const consoleInfo = document.createElement("div");
    consoleInfo.className = "hint";
    consoleInfo.textContent = user.canAccessAdminConsole
      ? (user.isAdmin
        ? "Yönetim konsoluna tam yetkiyle girebilir."
        : "Yönetim konsoluna salt okunur olarak girebilir.")
      : "Yönetim konsoluna giremez; yalnızca atanmış masaüstlerini kullanabilir.";
    card.appendChild(consoleInfo);

    const actions = document.createElement("div");
    actions.className = "row wrap";

    const grantButton = document.createElement("button");
    grantButton.type = "button";
    grantButton.textContent = "Admin Yetkisi Ver";
    grantButton.disabled = user.isAdmin && user.canAccessAdminConsole;
    grantButton.addEventListener("click", () => runSafely(async () => {
      await updateUserRole(user.id, true, true);
    }));
    actions.appendChild(grantButton);

    const standardButton = document.createElement("button");
    standardButton.type = "button";
    standardButton.className = "secondary";
    standardButton.textContent = "Standart Konsol (Salt Okunur)";
    standardButton.disabled = !user.isAdmin && user.canAccessAdminConsole;
    standardButton.addEventListener("click", () => runSafely(async () => {
      await updateUserRole(user.id, false, true);
    }));
    actions.appendChild(standardButton);

    const revokeButton = document.createElement("button");
    revokeButton.type = "button";
    revokeButton.className = "danger";
    revokeButton.textContent = "Konsol Erişimini Kaldır";
    revokeButton.disabled = !user.canAccessAdminConsole;
    revokeButton.addEventListener("click", () => runSafely(async () => {
      if (!window.confirm(`${user.displayName} kullanıcısının yönetim konsolu erişimi kaldırılsın mı?`)) return;
      await updateUserRole(user.id, false, false);
    }));
    actions.appendChild(revokeButton);

    card.appendChild(actions);
    ui.authorizationUsersList.appendChild(card);
  }
}

async function applyAuthUsersFilter() {
  state.authUsers.search = ui.authUsersFilterInput.value.trim();
  state.authUsers.page = 1;
  await loadAuthUsers();
}

async function updateUserRole(userId, isAdmin, canAccessAdminConsole) {
  await apiRequest(`/users/${userId}/role`, {
    method: "PUT",
    body: { isAdmin, canAccessAdminConsole }
  });

  await Promise.all([loadUsers(), loadAuthUsers()]);
  renderOverview();
}

async function onSearchAuthorizationAdGroups(event) {
  event.preventDefault();
  await runSafely(async () => {
    const query = ui.authGroupSearchInput.value.trim();
    if (query.length < 2) {
      throw new Error("AD grup araması için en az 2 karakter girin.");
    }

    const groups = await apiRequest(`/ad/groups?${toQueryString({ query, limit: 50 })}`);
    state.authGroupSearchResults = groups;
    renderAuthorizationGroupSearchResults();
  });
}

function renderAuthorizationGroupSearchResults() {
  ui.authGroupSearchResults.innerHTML = "";
  if (state.authGroupSearchResults.length === 0) {
    ui.authGroupSearchResults.appendChild(createHintElement("Sonuç bulunamadı."));
    return;
  }

  for (const group of state.authGroupSearchResults) {
    const row = document.createElement("article");
    row.className = "result-row";

    const top = document.createElement("div");
    top.className = "row wrap";
    const title = document.createElement("strong");
    title.textContent = `${group.domainName}\\${group.groupName}`;
    top.appendChild(title);

    const addButton = document.createElement("button");
    addButton.type = "button";
    addButton.textContent = "Admin Grubu Olarak Ekle";
    addButton.addEventListener("click", () => runSafely(async () => {
      await addAdminAccessGroup(group);
    }));
    top.appendChild(addButton);
    row.appendChild(top);

    ui.authGroupSearchResults.appendChild(row);
  }
}

async function loadAdminAccessGroups() {
  const groups = await apiRequest("/admin/access-groups");
  state.adminAccessGroups = groups ?? [];
  renderAdminAccessGroups();
}

function renderAdminAccessGroups() {
  ui.adminAccessGroupsList.innerHTML = "";
  if (state.adminAccessGroups.length === 0) {
    ui.adminAccessGroupsList.appendChild(createHintElement("Henüz AD grup yetkisi tanımlanmadı."));
    return;
  }

  for (const group of state.adminAccessGroups) {
    const row = document.createElement("article");
    row.className = "list-item";

    const top = document.createElement("div");
    top.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = group.domainName
      ? `${group.domainName}\\${group.groupName}`
      : group.groupName;
    top.appendChild(title);

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "danger";
    removeButton.textContent = "Kaldır";
    removeButton.addEventListener("click", () => runSafely(async () => {
      if (!window.confirm(`${title.textContent} grubunun yönetici yetkisi kaldırılsın mı?`)) return;
      await apiRequest(`/admin/access-groups/${group.id}`, { method: "DELETE" });
      await loadAdminAccessGroups();
      renderOverview();
    }));
    top.appendChild(removeButton);
    row.appendChild(top);
    ui.adminAccessGroupsList.appendChild(row);
  }
}

async function addAdminAccessGroup(group) {
  await apiRequest("/admin/access-groups", {
    method: "POST",
    body: {
      groupName: group.groupName,
      domainName: group.domainName
    }
  });

  await loadAdminAccessGroups();
  renderOverview();
}

async function loadMachines() {
  const response = await apiRequest(`/admin/machines?${toQueryString({
    page: state.machines.page,
    pageSize: state.machines.pageSize,
    search: state.machines.search
  })}`);

  state.machines.items = response.items ?? [];
  state.machines.totalCount = response.totalCount ?? 0;
  state.machines.page = response.page ?? state.machines.page;
  state.machines.pageSize = response.pageSize ?? state.machines.pageSize;
  renderMachines();
}

function renderMachines() {
  ui.machinesList.innerHTML = "";
  const totalPages = calculateTotalPages(state.machines.totalCount, state.machines.pageSize);
  ui.machinesPageInfo.textContent = `Sayfa ${state.machines.page}/${totalPages} - Toplam: ${state.machines.totalCount}`;
  ui.machinesPrevPageButton.disabled = state.machines.page <= 1;
  ui.machinesNextPageButton.disabled = state.machines.page >= totalPages;

  if (state.machines.items.length === 0) {
    ui.machinesList.appendChild(createHintElement("Makine bulunamadı."));
    return;
  }

  for (const machine of state.machines.items) {
    const card = document.createElement("article");
    card.className = "machine-card";

    const topRow = document.createElement("div");
    topRow.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = machine.name;
    topRow.appendChild(title);

    const status = document.createElement("span");
    status.className = `status ${machine.isOnline ? "online" : "offline"}`;
    status.textContent = machine.isOnline ? "RDP erişilebilir" : "RDP erişilemiyor";
    status.title = "BeRDP sunucusundan RDP TCP portuna erişim. İstemci ağındaki erişim farklı olabilir.";
    topRow.appendChild(status);
    card.appendChild(topRow);

    const detail = document.createElement("div");
    detail.className = "hint";
    detail.textContent = `${machine.rdpHostName ?? machine.ipAddress}:${machine.rdpPort} | IP: ${machine.ipAddress} | Kullanıcı: ${machine.assignedUserCount ?? 0} | AD Grup: ${machine.assignedAdGroupCount ?? 0}`;
    card.appendChild(detail);

    const userDetail = document.createElement("div");
    userDetail.className = "hint";
    userDetail.textContent = `Entitled kullanıcılar: ${formatNamePreview(machine.assignedUserDisplayNames, 6)}`;
    card.appendChild(userDetail);

    const adGroupDetail = document.createElement("div");
    adGroupDetail.className = "hint";
    adGroupDetail.textContent = `Entitled AD grupları: ${formatNamePreview(machine.assignedAdGroups, 6)}`;
    card.appendChild(adGroupDetail);

    const actionRow = document.createElement("div");
    actionRow.className = "row";
    const editButton = document.createElement("button");
    editButton.type = "button";
    editButton.className = "secondary";
    editButton.textContent = "Makine ve Erişimleri Düzenle";
    editButton.addEventListener("click", () => runSafely(() => editMachine(machine.id)));
    actionRow.appendChild(editButton);
    const deleteButton = document.createElement("button");
    deleteButton.type = "button";
    deleteButton.className = "danger";
    deleteButton.textContent = "Makineyi Sil";
    deleteButton.addEventListener("click", () => runSafely(async () => {
      if (!window.confirm(`${machine.name} makinesi ve tüm erişim atamaları silinsin mi?`)) return;
      await apiRequest(`/machines/${machine.id}`, { method: "DELETE" });
      if (state.editingMachineId === machine.id) resetMachineEditor();
      await loadMachines();
      renderOverview();
    }));
    actionRow.appendChild(deleteButton);
    card.appendChild(actionRow);

    ui.machinesList.appendChild(card);
  }
}

async function applyMachinesFilter() {
  state.machines.search = ui.machinesFilterInput.value.trim();
  state.machines.page = 1;
  await loadMachines();
}

async function editMachine(id) {
  if (state.savingMachine) throw new Error("Mevcut kaydetme işleminin bitmesini bekleyin.");
  const details = await apiRequest(`/admin/machines/${id}`);
  resetMachineEditor();
  state.editingMachineId = id;
  ui.newMachineName.value = details.name;
  ui.newMachineIp.value = details.ipAddress;
  ui.newMachineRdpHostName.value = details.rdpHostName ?? "";
  ui.newMachinePort.value = details.rdpPort;
  for (const user of details.entitledUsers) {
    state.selectedMachineUsers.set(user.userId, { ...user, id: user.userId });
  }
  for (const group of details.entitledAdGroups) {
    state.selectedMachineGroups.set(toGroupKey(group.groupName, group.domainName), group);
  }
  renderSelectedMachineUsers();
  renderSelectedMachineGroups();
  ui.cancelMachineEditButton.classList.remove("hidden");
  ui.machineCreateInfo.textContent = `"${details.name}" düzenleniyor. Kaydetmek mevcut erişim listesini seçiminizle değiştirir.`;
  ui.newMachineName.focus();
}

async function onSearchMachineUsers(event) {
  event.preventDefault();
  await runSafely(async () => {
    const query = ui.machineUserSearchInput.value.trim();
    if (query.length < 2) {
      throw new Error("Kullanıcı araması için en az 2 karakter girin.");
    }

    const users = await apiRequest(`/users?${toQueryString({
      search: query,
      source: "ad",
      skip: 0,
      take: 100
    })}`);

    state.machineUserSearchResults = users ?? [];
    renderMachineUserSearchResults();
  });
}

function renderMachineUserSearchResults() {
  ui.machineUserSearchResults.innerHTML = "";
  if (state.machineUserSearchResults.length === 0) {
    ui.machineUserSearchResults.appendChild(createHintElement("Kullanıcı bulunamadı."));
    return;
  }

  for (const user of state.machineUserSearchResults) {
    const row = document.createElement("article");
    row.className = "result-row";

    const top = document.createElement("div");
    top.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = `${user.displayName} (${user.username})`;
    top.appendChild(title);

    const selected = state.selectedMachineUsers.has(user.id);
    const toggleButton = document.createElement("button");
    toggleButton.type = "button";
    toggleButton.className = selected ? "secondary" : "";
    toggleButton.textContent = selected ? "Çıkar" : "Entitlement'a Ekle";
    toggleButton.addEventListener("click", () => {
      if (selected) {
        state.selectedMachineUsers.delete(user.id);
      } else {
        state.selectedMachineUsers.set(user.id, user);
      }

      renderMachineUserSearchResults();
      renderSelectedMachineUsers();
    });
    top.appendChild(toggleButton);
    row.appendChild(top);
    ui.machineUserSearchResults.appendChild(row);
  }
}

function renderSelectedMachineUsers() {
  ui.selectedMachineUsers.innerHTML = "";
  if (state.selectedMachineUsers.size === 0) {
    ui.selectedMachineUsers.appendChild(createHintElement("Seçili kullanıcı yok."));
    return;
  }

  for (const user of state.selectedMachineUsers.values()) {
    const chip = document.createElement("span");
    chip.className = "chip";
    chip.textContent = `${user.displayName} (${user.username})`;

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "secondary";
    removeButton.textContent = "x";
    removeButton.addEventListener("click", () => {
      state.selectedMachineUsers.delete(user.id);
      renderSelectedMachineUsers();
      renderMachineUserSearchResults();
    });

    chip.appendChild(removeButton);
    ui.selectedMachineUsers.appendChild(chip);
  }
}

async function onSearchMachineGroups(event) {
  event.preventDefault();
  await runSafely(async () => {
    const query = ui.machineGroupSearchInput.value.trim();
    if (query.length < 2) {
      throw new Error("AD grup araması için en az 2 karakter girin.");
    }

    const groups = await apiRequest(`/ad/groups?${toQueryString({ query, limit: 100 })}`);
    state.machineGroupSearchResults = groups ?? [];
    renderMachineGroupSearchResults();
  });
}

function renderMachineGroupSearchResults() {
  ui.machineGroupSearchResults.innerHTML = "";
  if (state.machineGroupSearchResults.length === 0) {
    ui.machineGroupSearchResults.appendChild(createHintElement("AD grup bulunamadı."));
    return;
  }

  for (const group of state.machineGroupSearchResults) {
    const groupKey = toGroupKey(group.groupName, group.domainName);
    const selected = state.selectedMachineGroups.has(groupKey);
    const row = document.createElement("article");
    row.className = "result-row";

    const top = document.createElement("div");
    top.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = `${group.domainName}\\${group.groupName}`;
    top.appendChild(title);

    const toggleButton = document.createElement("button");
    toggleButton.type = "button";
    toggleButton.className = selected ? "secondary" : "";
    toggleButton.textContent = selected ? "Çıkar" : "Entitlement'a Ekle";
    toggleButton.addEventListener("click", () => {
      if (selected) {
        state.selectedMachineGroups.delete(groupKey);
      } else {
        state.selectedMachineGroups.set(groupKey, {
          groupName: group.groupName,
          domainName: group.domainName
        });
      }

      renderMachineGroupSearchResults();
      renderSelectedMachineGroups();
    });
    top.appendChild(toggleButton);
    row.appendChild(top);
    ui.machineGroupSearchResults.appendChild(row);
  }
}

function renderSelectedMachineGroups() {
  ui.selectedMachineGroups.innerHTML = "";
  if (state.selectedMachineGroups.size === 0) {
    ui.selectedMachineGroups.appendChild(createHintElement("Seçili AD grup yok."));
    return;
  }

  for (const [groupKey, group] of state.selectedMachineGroups.entries()) {
    const chip = document.createElement("span");
    chip.className = "chip";
    chip.textContent = group.domainName
      ? `${group.domainName}\\${group.groupName}`
      : group.groupName;

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "secondary";
    removeButton.textContent = "x";
    removeButton.addEventListener("click", () => {
      state.selectedMachineGroups.delete(groupKey);
      renderSelectedMachineGroups();
      renderMachineGroupSearchResults();
    });
    chip.appendChild(removeButton);
    ui.selectedMachineGroups.appendChild(chip);
  }
}

async function onCreateMachine(event) {
  event.preventDefault();
  if (state.savingMachine) return;
  state.savingMachine = true;
  const saveButton = document.querySelector('button[form="createMachineForm"]');
  saveButton.disabled = true;
  try {
    await runSafely(async () => {
      const rdpPort = Number(ui.newMachinePort.value);
      if (!Number.isInteger(rdpPort) || rdpPort < 1 || rdpPort > 65535) {
        throw new Error("RDP portu 1-65535 arasında bir tam sayı olmalıdır.");
      }

      const entitledUserIds = Array.from(state.selectedMachineUsers.keys());
      const entitledAdGroups = Array.from(state.selectedMachineGroups.values()).map(group => ({
        groupName: group.groupName,
        domainName: group.domainName
      }));

      const editingId = state.editingMachineId;
      await apiRequest(editingId ? `/machines/${editingId}` : "/machines", {
        method: editingId ? "PUT" : "POST",
        body: {
          name: ui.newMachineName.value.trim(),
          ipAddress: ui.newMachineIp.value.trim(),
          rdpHostName: ui.newMachineRdpHostName.value.trim() || null,
          rdpPort,
          entitledUserIds,
          entitledAdGroups
        }
      });

      resetMachineEditor();
      ui.machineCreateInfo.textContent = editingId ? "Makine ve erişimleri güncellendi." : "Makine ve erişimleri kaydedildi.";

      await loadMachines();
      renderOverview();
    });
  } finally {
    state.savingMachine = false;
    saveButton.disabled = false;
  }
}

function resetMachineEditor() {
  state.editingMachineId = null;
  ui.createMachineForm.reset();
  ui.newMachinePort.value = "3389";
  state.selectedMachineUsers.clear();
  state.selectedMachineGroups.clear();
  state.machineUserSearchResults = [];
  state.machineGroupSearchResults = [];
  ui.machineUserSearchResults.replaceChildren();
  ui.machineGroupSearchResults.replaceChildren();
  ui.cancelMachineEditButton.classList.add("hidden");
  ui.machineCreateInfo.textContent = "";
  renderSelectedMachineUsers();
  renderSelectedMachineGroups();
}

async function loadAdConfig() {
  const configResponse = await apiRequest("/ad/config");
  state.adConfig = configResponse;

  ui.adEnabledInput.checked = !!configResponse.enabled;
  ui.adDomainInput.value = configResponse.domainName ?? "";
  ui.adContainerInput.value = configResponse.ldapContainer ?? "";
  ui.adUseMachineCredentialsInput.checked = !!configResponse.useMachineCredentials;
  ui.adBindUsernameInput.value = configResponse.bindUsername ?? "";
  ui.adBindPasswordInput.value = "";
  ui.adConfigInfo.textContent = configResponse.hasBindPassword
    ? "Bind parolası kayıtlı. Değiştirmek için yeni değer girin."
    : "Bind parolası kaydedilmemiş.";

  syncAdCredentialInputsState();
}

async function onSaveAdConfig(event) {
  event.preventDefault();
  await runSafely(async () => {
    const request = buildAdConfigRequest();

    const saved = await apiRequest("/ad/config", {
      method: "PUT",
      body: request
    });

    state.adConfig = saved;
    ui.adConfigInfo.textContent = "AD ayarları kaydedildi.";
    ui.adBindPasswordInput.value = "";
    syncAdCredentialInputsState();
    renderOverview();
  });
}

async function onTestAdConfig() {
  await runSafely(async () => {
    ui.testAdConfigButton.disabled = true;
    ui.adConfigInfo.textContent = "AD bağlantısı test ediliyor...";
    try {
      const result = await apiRequest("/ad/test", {
        method: "POST",
        body: buildAdConfigRequest()
      });
      ui.adConfigInfo.textContent = result.message;
    } catch (error) {
      ui.adConfigInfo.textContent = getErrorMessage(error);
      throw error;
    } finally {
      ui.testAdConfigButton.disabled = false;
    }
  });
}

function buildAdConfigRequest() {
  return {
    enabled: ui.adEnabledInput.checked,
    domainName: ui.adDomainInput.value.trim(),
    ldapContainer: ui.adContainerInput.value.trim(),
    useMachineCredentials: ui.adUseMachineCredentialsInput.checked,
    bindUsername: ui.adBindUsernameInput.value.trim(),
    bindPassword: ui.adBindPasswordInput.value.length === 0 ? null : ui.adBindPasswordInput.value
  };
}

async function onSearchAdUsers(event) {
  event.preventDefault();
  await runSafely(async () => {
    const query = ui.adUserSearchInput.value.trim();
    if (query.length < 2) {
      throw new Error("AD kullanıcı araması için en az 2 karakter girin.");
    }

    const users = await apiRequest(`/ad/users?${toQueryString({ query, limit: 50 })}`);
    state.adUserSearchResults = users ?? [];
    renderAdUserSearchResults();
  });
}

function renderAdUserSearchResults() {
  ui.adSearchResults.innerHTML = "";
  if (state.adUserSearchResults.length === 0) {
    ui.adSearchResults.appendChild(createHintElement("AD kullanıcı bulunamadı."));
    return;
  }

  for (const user of state.adUserSearchResults) {
    const row = document.createElement("article");
    row.className = "result-row";

    const top = document.createElement("div");
    top.className = "row wrap";

    const title = document.createElement("strong");
    title.textContent = `${user.displayName} (${user.samAccountName})`;
    top.appendChild(title);

    const addButton = document.createElement("button");
    addButton.type = "button";
    addButton.textContent = "Kullanıcı Havuzuna Ekle";
    addButton.addEventListener("click", () => runSafely(async () => {
      await apiRequest("/users/ad", {
        method: "POST",
        body: {
          samAccountName: user.samAccountName,
          displayName: user.displayName,
          domainName: user.domainName,
          userPrincipalName: user.userPrincipalName,
          isAdmin: false
        }
      });

      await Promise.all([loadUsers(), loadAuthUsers(), loadUserTotal()]);
      renderOverview();
    }));
    top.appendChild(addButton);
    row.appendChild(top);

    const info = document.createElement("div");
    info.className = "hint";
    info.textContent = `${user.domainName} | ${user.userPrincipalName ?? "UPN yok"}`;
    row.appendChild(info);

    ui.adSearchResults.appendChild(row);
  }
}

function syncAdCredentialInputsState() {
  const disabled = ui.adUseMachineCredentialsInput.checked;
  ui.adBindUsernameInput.disabled = disabled;
  ui.adBindPasswordInput.disabled = disabled;
}

function renderOverview() {
  ui.totalUsersStat.textContent = String(state.totalUserCount ?? 0);
  ui.totalMachinesStat.textContent = String(state.machines.totalCount ?? 0);
  ui.adminGroupsStat.textContent = String(state.adminAccessGroups.length);
  ui.adStatusStat.textContent = state.adConfig?.enabled ? "Aktif" : "Pasif";
}

function onToggleTheme() {
  const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  applyTheme(next);
  saveTheme(next);
}

function loadSavedTheme() {
  try {
    return localStorage.getItem(THEME_STORAGE_KEY) === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

function saveTheme(theme) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Storage can be blocked by policy; the theme still applies for this page.
  }
}

function applyTheme(theme) {
  const dark = theme === "dark";
  document.documentElement.dataset.theme = dark ? "dark" : "light";
  const label = dark ? "Aydınlık temaya geç" : "Karanlık temaya geç";
  ui.themeToggleButtons.forEach(button => {
    button.setAttribute("aria-pressed", String(dark));
    button.setAttribute("aria-label", label);
    button.title = label;
  });
}

async function changePage(pageState, step, loadAction) {
  const totalPages = calculateTotalPages(pageState.totalCount, pageState.pageSize);
  const nextPage = Math.max(1, Math.min(totalPages, pageState.page + step));
  if (nextPage === pageState.page) {
    return;
  }

  pageState.page = nextPage;
  await loadAction();
}

function calculateTotalPages(totalCount, pageSize) {
  const pages = Math.ceil((totalCount ?? 0) / pageSize);
  return pages < 1 ? 1 : pages;
}

function toGroupKey(groupName, domainName) {
  const normalizedGroup = (groupName ?? "").trim().toLowerCase();
  const normalizedDomain = (domainName ?? "").trim().toLowerCase();
  return normalizedDomain.length > 0
    ? `${normalizedDomain}\\${normalizedGroup}`
    : normalizedGroup;
}

function makeBadge(text, extraClass) {
  const badge = document.createElement("span");
  badge.className = `badge ${extraClass}`.trim();
  badge.textContent = text;
  return badge;
}

function formatNamePreview(values, visibleCount = 5) {
  const normalized = (values ?? [])
    .filter(value => typeof value === "string")
    .map(value => value.trim())
    .filter(value => value.length > 0);

  if (normalized.length === 0) {
    return "-";
  }

  const preview = normalized.slice(0, visibleCount);
  const remaining = normalized.length - preview.length;
  return remaining > 0
    ? `${preview.join(", ")} +${remaining} daha`
    : preview.join(", ");
}

function createHintElement(text) {
  const item = document.createElement("div");
  item.className = "hint";
  item.textContent = text;
  return item;
}

function toQueryString(values) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    params.append(key, String(value));
  }

  return params.toString();
}

async function apiRequest(path, options = {}) {
  const endpoint = new URL(backendBaseUrl, window.location.href);
  if (endpoint.protocol !== "https:" && !["localhost", "127.0.0.1", "[::1]"].includes(endpoint.hostname)) {
    throw new Error("Uzak API erişimi HTTPS gerektirir. Yönetici sunucu adresini düzeltmelidir.");
  }
  const method = options.method ?? "GET";
  const authRequired = options.authRequired ?? true;
  const body = options.body;
  const generation = state.sessionGeneration;

  if (authRequired && !state.token) {
    throw new Error("Yönetim oturumu bulunamadı. Lütfen yeniden giriş yapın.");
  }

  const headers = { "Content-Type": "application/json" };
  if (authRequired && state.token) {
    headers.Authorization = `Bearer ${state.token}`;
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 30000);
  let response;
  let payload = null;
  try {
    response = await fetch(`${backendBaseUrl}${path}`, {
      method,
      headers,
      signal: controller.signal,
      credentials: options.credentials ?? "omit",
      redirect: "error",
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (response.status !== 204) {
      const contentType = response.headers.get("content-type") ?? "";
      payload = contentType.includes("json") ? await response.json() : await response.text();
    }
  } catch (error) {
    if (controller.signal.aborted) {
      throw new Error("Sunucu isteği 30 saniyede tamamlanamadı. Sunucu ve firewall erişimini kontrol edin.");
    }
    throw error;
  } finally {
    clearTimeout(timeout);
  }

  if (generation !== state.sessionGeneration) {
    throw new SessionChangedError();
  }

  if (response.status === 204) {
    return null;
  }

  if (!response.ok) {
    if (authRequired && (response.status === 401 ||
        (response.status === 403 && payload?.code === "admin_required"))) {
      onLogout();
      ui.loginError.textContent = payload?.message ??
        "Yönetim oturumunun süresi dolmuş. Lütfen yeniden giriş yapın.";
    }

    if (payload?.message || payload?.detail || payload?.title) {
      throw new Error(payload.message ?? payload.detail ?? payload.title);
    }

    if (typeof payload === "string" && payload.length > 0) {
      throw new Error(payload);
    }

    throw new Error(`İstek başarısız: HTTP ${response.status}`);
  }

  return payload;
}

async function runSafely(action) {
  ui.globalError.textContent = "";
  try {
    await action();
  } catch (error) {
    if (!(error instanceof SessionChangedError)) {
      const target = state.token ? ui.globalError : ui.loginError;
      target.textContent = getErrorMessage(error);
    }
  }
}

class SessionChangedError extends Error {
  constructor() {
    super("Oturum değişti; önceki isteğin cevabı kullanılmadı.");
  }
}

function getErrorMessage(error) {
  if (error instanceof Error) {
    return error.message;
  }

  return "Beklenmeyen bir hata oluştu.";
}
