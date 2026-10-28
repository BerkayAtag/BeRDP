#requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSEdition -ne "Desktop" -or $PSVersionTable.PSVersion.Major -ne 5) {
    throw "Run these installer compatibility tests with Windows PowerShell 5.1."
}

. (Join-Path $PSScriptRoot "..\installer\server\assets\Save-Configuration.ps1")
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("BeRdp-ConfigurationTests-" + [Guid]::NewGuid().ToString("N"))
New-Item -Path $testRoot -ItemType Directory -ErrorAction Stop | Out-Null
$script:passed = 0

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:passed++
    Write-Host "PASS: $Message"
}

try {
    $directory = Join-Path $testRoot "Program Files\BeRDP Server\Backend"
    New-Item -Path $directory -ItemType Directory -Force | Out-Null
    $legacyPath = Join-Path $directory "legacy.json"
    $legacyTemporary = $legacyPath + ".tmp"
    [System.IO.File]::WriteAllText($legacyPath, '{"version":"old"}')
    [System.IO.File]::WriteAllText($legacyTemporary, '{"version":"new"}')
    $legacyHash = (Get-FileHash -LiteralPath $legacyPath -Algorithm SHA256).Hash
    $legacyRejected = $false
    try {
        [System.IO.File]::Replace($legacyTemporary, $legacyPath, $null)
    } catch [System.Management.Automation.MethodInvocationException] {
        if ($_.Exception.InnerException -isnot [System.ArgumentException]) { throw }
        $legacyRejected = $true
    }
    Assert-Condition $legacyRejected "Legacy null backup reproduces the PowerShell 5.1 invalid-path failure"
    Assert-Condition ((Get-FileHash -LiteralPath $legacyPath -Algorithm SHA256).Hash -eq $legacyHash) "Legacy failure preserves the original file"

    $path = Join-Path $directory "appsettings.json"
    $settings = [pscustomobject]@{
        Jwt = [pscustomobject]@{
            Key = "regression-only-existing-signing-key-32-bytes"
            Issuer = "Custom.Issuer"
        }
        Storage = [pscustomobject]@{ DataDirectory = "C:\Custom Data\BeRDP" }
        ActiveDirectory = [pscustomobject]@{
            DomainName = "aknet.akb"
            BindPassword = " regression-only password with spaces "
        }
        Cors = [pscustomobject]@{ AllowedOrigins = @("https://admin.example.test", "http://server:5173") }
        CustomSetting = "Existing value"
    }
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
    $originalHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $settings.CustomSetting = "Updated value " + [char]0x0130
    $backup = Save-BeRdpConfiguration -Path $path -Settings $settings
    $saved = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-Condition ([System.IO.File]::Exists($backup)) "Replacement creates a real backup next to the configuration"
    Assert-Condition ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -eq $originalHash) "Backup preserves exact original bytes"
    Assert-Condition ($saved.CustomSetting -eq $settings.CustomSetting) "Replacement writes valid UTF-8 JSON at a path containing spaces"
    Assert-Condition ($saved.Jwt.Key -eq $settings.Jwt.Key -and $saved.Jwt.Issuer -eq "Custom.Issuer") "Custom JWT values are preserved"
    Assert-Condition ($saved.Storage.DataDirectory -eq "C:\Custom Data\BeRDP") "Custom database location is preserved"
    Assert-Condition ($saved.ActiveDirectory.BindPassword -eq " regression-only password with spaces ") "AD password spaces are preserved"
    Assert-Condition (@($saved.Cors.AllowedOrigins).Count -eq 2) "CORS arrays are preserved"
    Assert-Condition (@(Get-ChildItem -LiteralPath $directory -Filter "appsettings.json.setup-*.tmp").Count -eq 0) "Successful replacement leaves no temporary configuration"

    $updatedHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $secondBackup = Save-BeRdpConfiguration -Path $path -Settings $saved
    Assert-Condition ($secondBackup -ne $backup -and [System.IO.File]::Exists($backup)) "Repeated saves retain separate backups"
    Assert-Condition ((Get-FileHash -LiteralPath $secondBackup -Algorithm SHA256).Hash -eq $updatedHash) "Repeated save backs up the previous configuration"

    $beforeFailure = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $backupsBefore = @(Get-ChildItem -LiteralPath $directory -Filter "appsettings.json.setup-*.bak").Count
    $locked = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $writeRejected = $false
    try {
        try {
            Save-BeRdpConfiguration -Path $path -Settings $saved | Out-Null
        } catch [System.Management.Automation.MethodInvocationException] {
            if ($_.Exception.InnerException -isnot [System.IO.IOException] -and
                $_.Exception.InnerException -isnot [System.UnauthorizedAccessException]) { throw }
            $writeRejected = $true
        }
    } finally {
        $locked.Dispose()
    }
    Assert-Condition $writeRejected "Locked configuration replacement reports an error"
    Assert-Condition ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $beforeFailure) "Failed replacement does not overwrite the current configuration"
    Assert-Condition (@(Get-ChildItem -LiteralPath $directory -Filter "appsettings.json.setup-*.bak").Count -eq $backupsBefore) "Failed replacement does not claim a successful backup"
    Assert-Condition (@(Get-ChildItem -LiteralPath $directory -Filter "appsettings.json.setup-*.tmp").Count -eq 0) "Failed replacement cleans only its own temporary file"

    $missingRejected = $false
    try {
        Save-BeRdpConfiguration -Path (Join-Path $directory "missing.json") -Settings $saved | Out-Null
    } catch [System.IO.FileNotFoundException] {
        $missingRejected = $true
    }
    Assert-Condition $missingRejected "Missing original configuration is rejected explicitly"
    Write-Host "SUCCESS: $script:passed Windows PowerShell 5.1 configuration checks passed."
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Stop
}
