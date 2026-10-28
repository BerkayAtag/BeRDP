# Security policy

1.6.0 is a release candidate. Review [deployment requirements](docs/INSTALLATION_GUIDE.md) and [validation limits](docs/VALIDATION.md) before production adoption.

After the public GitHub repository is created, enable **Private vulnerability reporting** in repository settings. Report sensitive findings through its Security → Advisories → Report a vulnerability flow. Until that channel exists, contact the maintainer privately through an established channel; do not publish passwords, tokens, personal data or exploit details in a public issue. No response SLA or dedicated mailbox is claimed.

Include affected version, configuration, reproduction using synthetic data, expected/observed behavior and impact. Test only systems you own or are authorized to assess. Dependencies and bundled runtimes require regular rebuilds; self-contained installers do not receive runtime patches merely because the host .NET installation changes.
