# This test is intentionally restricted to a disposable GitHub-hosted Windows runner.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installer smoke tests require a disposable GitHub-hosted Windows runner.'
}
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'dist/installers'
$testRoot = Join-Path $env:RUNNER_TEMP 'BeRDP-PackageSmoke'
$serverKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{D97649F0-71F9-4A5F-8ED3-21990CCFA6DF}_is1'
$clientKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A6384144-615C-4858-9D23-B1FB889A2D79}_is1'
if ((Test-Path $serverKey) -or (Test-Path $clientKey) -or (Get-Service BeRdpBackend,BeRdpAdminConsole -ErrorAction SilentlyContinue)) {
    throw 'Existing BeRDP installation detected. Refusing to modify it.'
}
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Run-Setup([string]$Executable, [string[]]$SetupArguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $SetupArguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup failed with exit code $($process.ExitCode): $Executable" }
}
$common = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-')
$serverInstalled = $false
$clientInstalled = $false
try {
    Run-Setup "$output/BeRDP.Client.Setup.exe" ($common + @('/SSO=0','/BACKENDURL="https://berdp.example.test/api"',"/DIR=`"$testRoot/Client`"", "/LOG=`"$testRoot/client.log`""))
    $clientInstalled = $true
    Run-Setup "$output/BeRDP.Server.Setup.exe" ($common + @('/SSO=0',"/DIR=`"$testRoot/Server`"", "/LOG=`"$testRoot/server.log`""))
    $serverInstalled = $true
    $results = foreach ($entry in @(@{Kind='Client';Key=$clientKey},@{Kind='Server';Key=$serverKey})) {
        $value = Get-ItemProperty -LiteralPath $entry.Key
        if ($value.Publisher -cne 'Berkay Atağ') { throw "Incorrect appwiz Publisher for $($entry.Kind)" }
        if ($value.DisplayVersion -ne '1.7.0') { throw "Incorrect installed version for $($entry.Kind)" }
        $iconPath = ($value.DisplayIcon -replace ',\s*-?\d+$', '').Trim('"')
        if (-not $iconPath -or -not (Test-Path -LiteralPath $iconPath -PathType Leaf)) { throw "Missing appwiz icon for $($entry.Kind)" }
        Write-Host "PASS appwiz.cpl $($entry.Kind): Publisher=$($value.Publisher), Version=$($value.DisplayVersion), Icon=$($value.DisplayIcon)"
        [pscustomobject]@{Component=$entry.Kind;DisplayName=$value.DisplayName;Publisher=$value.Publisher;Version=$value.DisplayVersion;DisplayIcon=$value.DisplayIcon}
    }
    foreach ($port in @(5001,5173)) {
        if ((Invoke-WebRequest "http://127.0.0.1:$port/health").StatusCode -ne 200) { throw "Service health failed: $port" }
        $listeners = Get-NetTCPConnection -LocalPort $port -State Listen
        if ($listeners.LocalAddress | Where-Object { $_ -notin @('127.0.0.1','::1') }) { throw "Service exposed beyond loopback: $port" }
    }
    Write-Host 'PASS installed services are healthy and listen only on loopback'
    $defaultLogin = Invoke-WebRequest 'http://127.0.0.1:5001/login' -Method Post -ContentType 'application/json' -Body '{"username":"admin","password":"Admin123!"}' -SkipHttpErrorCheck
    if ($defaultLogin.StatusCode -ne 401) { throw 'Unexpected default administrator access' }
    Write-Host 'PASS installed server rejects historical default credentials'
    # Same path the Server Setup wizard uses for its first-administrator page.
    $backendRoot = Join-Path $testRoot 'Server/Backend'
    $adminPassword = 'Ci-' + [guid]::NewGuid().ToString('N')
    $env:BERDP_SETUP_ADMIN_PASSWORD = $adminPassword
    try {
        $bootstrap = Start-Process -FilePath (Join-Path $backendRoot 'BeRdp.Backend.exe') -ArgumentList @('--contentRoot', "`"$backendRoot`"", '--bootstrap-admin', 'ci-admin') -WorkingDirectory $backendRoot -WindowStyle Hidden -PassThru -Wait
    } finally { Remove-Item Env:BERDP_SETUP_ADMIN_PASSWORD -ErrorAction SilentlyContinue }
    if ($bootstrap.ExitCode -ne 0) { throw "First administrator bootstrap failed with exit code $($bootstrap.ExitCode)" }
    $adminLogin = Invoke-WebRequest 'http://127.0.0.1:5001/login' -Method Post -ContentType 'application/json' -Body (@{ username = 'ci-admin'; password = $adminPassword } | ConvertTo-Json) -SkipHttpErrorCheck
    if ($adminLogin.StatusCode -ne 200 -or -not ($adminLogin.Content | ConvertFrom-Json).canAccessAdminConsole) { throw 'Bootstrapped administrator cannot sign in to the console' }
    Write-Host 'PASS setup-style first administrator bootstrap signs in with console access'
    $configurationPath = Join-Path $testRoot 'Server/Backend/appsettings.json'
    $before = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
    Run-Setup "$output/BeRDP.Server.Setup.exe" ($common + @('/SSO=0',"/DIR=`"$testRoot/Server`"", "/LOG=`"$testRoot/upgrade.log`""))
    $after = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
    if ($before.Jwt.Key -ne $after.Jwt.Key) { throw 'Upgrade changed the existing signing key' }
    Write-Host 'PASS reinstall preserves the signing key and restarts services'
    $results | ConvertTo-Json | Set-Content -LiteralPath "$output/installation-verification.json" -Encoding utf8
} finally {
    if ($serverInstalled) {
        Run-Setup (Join-Path $env:ProgramData 'BeRDP/Server/Uninstall/unins000.exe') $common
    }
    if ($clientInstalled) { Run-Setup "$testRoot/Client/unins000.exe" $common }
}
if ((Test-Path $serverKey) -or (Test-Path $clientKey)) { throw 'Uninstall registration was not removed' }
if (Get-Service BeRdpBackend,BeRdpAdminConsole -ErrorAction SilentlyContinue) { throw 'Uninstall left BeRDP services registered' }
Write-Host 'PASS both uninstall registrations and services were removed'
Write-Host 'SUCCESS: real Windows installation, appwiz Publisher, health, reinstall and uninstall checks passed.'
