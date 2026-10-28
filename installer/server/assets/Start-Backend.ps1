param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$backendExe = Join-Path $InstallRoot "Backend\BeRdp.Backend.exe"
if (-not (Test-Path $backendExe)) {
    throw "Backend executable not found: $backendExe"
}

$service = Get-Service -Name "BeRdpBackend" -ErrorAction SilentlyContinue
if ($null -ne $service) {
    Start-Service -Name "BeRdpBackend" -ErrorAction Stop
} else {
    $directory = Split-Path -Path $backendExe -Parent
    Start-Process -WindowStyle Hidden -FilePath $backendExe -WorkingDirectory $directory -ArgumentList "--urls", "http://127.0.0.1:5001"
}
