# Changelog

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
