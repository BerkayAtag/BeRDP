Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$serviceNames = @("BeRdpBackend", "BeRdpAdminConsole")
foreach ($serviceName in $serviceNames) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -ne "Stopped") {
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
        $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }
}
