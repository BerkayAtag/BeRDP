// Applies the saved console theme before first paint so dark mode does not flash.
(function () {
  var theme = "light";
  try {
    if (localStorage.getItem("berdp-admin-theme") === "dark") theme = "dark";
  } catch (e) { /* Storage can be blocked; the light default still renders. */ }
  document.documentElement.setAttribute("data-theme", theme);
})();
