# Tests

Windows + .NET 10 SDK:

```powershell
dotnet run --project tests/BeRdp.Regression -c Release
dotnet run --project src/BeRdp.ServiceSetup -c Release -- --self-test
dotnet build src/BeRdp.AdminConsole -c Release
```

Live API/browser checks use Playwright installed outside production projects. Install Playwright in a temporary QA directory, set NODE_PATH to its node_modules, and optionally set PLAYWRIGHT_CHROMIUM_EXECUTABLE to an approved Edge/Chrome executable. Then run `node tests/Smoke.Web.cjs`. It requires ports 5501 and 5517, generates random credentials, creates a temporary database and terminates its server processes. Screenshots are synthetic documentation assets. Temporary test databases remain under the OS temp folder, outside release artifacts.

`DemoFixture.cs` is test-only; it must never be linked into backend production code. Historical tests use synthetic identities; their credentials are not valid product credentials.

Old PowerShell smoke scripts describe legacy deployments and are not the release acceptance gate; use the .NET runner and Smoke.Web.cjs for 1.6.0. See docs/VALIDATION.md for environment-dependent test gaps.

Docker Desktop Linux containers, PowerShell 7 and Python 3: `pwsh -NoProfile -File tests/Smoke.Docker.ps1`. This checks real HTTPS certificate/hostname validation, remote HTTP rejection, authentication and an empty fresh database. It does not test Windows setup, WPF or Active Directory.
