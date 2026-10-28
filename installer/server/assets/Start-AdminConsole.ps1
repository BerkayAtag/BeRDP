param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$adminExe = Join-Path $InstallRoot "AdminConsole\BeRdp.AdminConsole.exe"
if (-not (Test-Path $adminExe)) {
    throw "Admin console executable not found: $adminExe"
}

$service = Get-Service -Name "BeRdpAdminConsole" -ErrorAction SilentlyContinue
if ($null -ne $service) {
    Start-Service -Name "BeRdpAdminConsole" -ErrorAction Stop
} else {
    $directory = Split-Path -Path $adminExe -Parent
    Start-Process -WindowStyle Hidden -FilePath $adminExe -WorkingDirectory $directory -ArgumentList "--urls", "http://127.0.0.1:5173"
}
