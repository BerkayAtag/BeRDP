# Changelog

## 1.7.0

Installers
- Click-to-install Server and Client setups in Turkish: no welcome, folder or license pages.
- Server setup asks for the first administrator and creates it after the services start; the password is passed through a process-private environment variable, never a command line, log or file. Existing databases skip the page.
- Finish page and Start menu open the management console; Start menu uninstall shortcuts.
- Red BeRDP logo for setup executables, all application executables and the Programs and Features (appwiz.cpl) entry; Publisher remains Berkay Atağ.
- Client setup validates the server address on its own page; the RDP-target task is available only to silent enterprise deployments.
- Removed unused legacy server firewall parameter validation and an internal domain example.

Security and fixes
- Entitlement-only users no longer receive the names of other users and groups assigned to their machines.
- Active Directory group admin grants now open the management console with full rights, as the Authorization page describes.
- Revoking console access also removes the explicit admin role.
- Local user names are validated and cannot imitate `DOMAIN\user` or `user@domain` identities.
- Console at `http://127.0.0.1:5173` is allowed by the default CORS policy.
- Console: overview counts all users instead of the AD-only filter; confirmation before deleting users, machines, admin groups or revoking console access; filters are cleared on sign-out; theme choice survives blocked browser storage.
- Console network and silent-install pages describe the current TLS proxy and loopback-only services instead of the removed direct ports.
- Client error hints suggest HTTPS server addresses.

Design and documentation
- Theme toggle button, logo and footer (© 2026 Berkay Atağ · BeRDP) in the management console; left-aligned navigation and inline filters.
- New landing page with light and dark themes, light/dark screenshots and direct download links.
- README, installation guide and validation report updated; release workflow publishes any verified tag.

## 1.6.0 — release candidate

- Publisher and assembly company: Berkay Atağ; MIT license.
- .NET 10 runtime and dependency lock files.
- Removed automatic production demo seeding; explicit administrator bootstrap/recovery.
- Quarantined unchanged historical demo credentials on marked legacy databases.
- Loopback-only installed services, remote HTTPS enforcement and private storage ACLs.
- Blocked client credential redirects, cross-host discovery and HTTPS downgrade.
- Revalidated AD group membership and directory domain on authenticated requests.
- Refreshed native launch entitlement immediately before connecting.
- Fixed assigned-only filtering, read-only console navigation and last-admin protection.
- Removed automatic Windows RDP consent suppression; fixed theme subscription lifetimes.
- Per-session single-instance client to prevent overlapping credential cleanup.
- Hardened web headers and preserved configuration during upgrades.
- Consolidated installer build entry points; client builds before embedded server package.
- Added live HTTP/browser checks, screenshots and enterprise deployment documentation.

Older notes in history-release.md are historical and do not describe current defaults.
