param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [switch]$Uninstall,
    [switch]$StopOnly,
    [string]$LogPath = "",
    [ValidateSet("", "0", "1")][string]$WindowsSso = ""
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
# Compatibility entry point: use the same ACL/configuration/service logic as Setup.
$root = [IO.Path]::GetFullPath($InstallRoot)
$tool = Join-Path $root "ServiceSetup/BeRdp.ServiceSetup.exe"
if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Install the current BeRDP Server package first." }
if ($Uninstall -and $StopOnly) { throw "Choose either Uninstall or StopOnly." }
$mode = if ($Uninstall) { "uninstall" } elseif ($StopOnly) { "prepare" } else { "install" }
if (-not $LogPath) { $LogPath = Join-Path $env:TEMP ("BeRDP-service-" + [Guid]::NewGuid().ToString('N') + '.log') }
$arguments = @('--root', $root, '--mode', $mode, '--log', $LogPath)
if ($WindowsSso) { $arguments += @('--sso', $WindowsSso) }
& $tool @arguments
if ($LASTEXITCODE) { throw "Native service setup failed: $LASTEXITCODE. See $LogPath" }
