function Save-BeRdpConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [object]$Settings
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        throw [System.IO.FileNotFoundException]::new("Configuration file not found.", $resolvedPath)
    }

    $saveId = [Guid]::NewGuid().ToString("N")
    $temporaryPath = $resolvedPath + ".setup-" + $saveId + ".tmp"
    $backupPath = $resolvedPath + ".setup-" + $saveId + ".bak"
    try {
        $json = ConvertTo-Json -InputObject $Settings -Depth 20
        $json | ConvertFrom-Json -ErrorAction Stop | Out-Null
        [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.Encoding]::UTF8)
        # A real backup path avoids PowerShell 5.1 converting $null to an empty string.
        [System.IO.File]::Replace($temporaryPath, $resolvedPath, $backupPath)
    } finally {
        if ([System.IO.File]::Exists($temporaryPath)) {
            try {
                Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction Stop
            } catch [System.IO.IOException] {
                Write-Warning "Temporary configuration cleanup failed: $temporaryPath. $($_.Exception.Message)"
            } catch [System.UnauthorizedAccessException] {
                Write-Warning "Temporary configuration cleanup failed: $temporaryPath. $($_.Exception.Message)"
            }
        }
    }

    return $backupPath
}
