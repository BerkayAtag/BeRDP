"use strict";
const repository = window.BERDP_SITE?.repositoryUrl;
if (typeof repository === "string" && /^https:\/\/github\.com\/[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+\/?$/.test(repository)) {
  const container = document.getElementById("releaseLinks");
  container.replaceChildren();
  const link = document.createElement("a");
  link.className = "button";
  link.href = repository.replace(/\/$/, "") + "/releases";
  link.textContent = "GitHub Releases →";
  container.append(link);
}
