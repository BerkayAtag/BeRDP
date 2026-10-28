# Contributing

Use Windows and the SDK selected by global.json. Keep changes focused; explain the failing scenario and validation. Run the regression runner, native service setup self-test and relevant live browser checks before submitting a pull request. Never commit real databases, signing keys, certificates, deployment secrets, machine inventories or setup logs.

Test fixtures belong only in tests/. Production startup must remain empty. Preserve TLS certificate validation, RDP consent and enterprise GPO decisions. New access paths must check authorization server-side. Documentation must distinguish tested behavior from environment-dependent AD/RDP behavior.

Contributions are under the MIT license. For security reports follow SECURITY.md.
