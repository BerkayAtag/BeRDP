param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$InnoSetupCompilerPath = "",
    [switch]$SkipPublish,
    [switch]$DeliverOnly,
    [string]$RepoRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$distRoot = Join-Path $repoRoot "dist"
$publishRoot = Join-Path $distRoot "publish"
$installerOutput = Join-Path $distRoot "installers"
$stagingRoot = Join-Path $distRoot "staging"
$stagingInstallers = Join-Path $stagingRoot "installers"
$buildStartedAt = Get-Date

function Invoke-CheckedCommand {
    param([string]$Executable, [string[]]$Arguments)
    Write-Host "[RUN] $Executable $($Arguments -join ' ')"
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE. No staged installers were promoted."
    }
}

function Copy-StagedInstallers {
    foreach ($name in @("BeRDP.Server.Setup.exe", "BeRDP.Client.Setup.exe")) {
        $source = Join-Path $stagingInstallers $name
        $file = Get-Item -LiteralPath $source -ErrorAction Stop
        if ($file.Length -le 0) { throw "Staged installer is empty: $source" }
    }
    New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null
    foreach ($name in @("BeRDP.Server.Setup.exe", "BeRDP.Client.Setup.exe")) {
        $source = Join-Path $stagingInstallers $name
        $destination = Join-Path $installerOutput $name
        $temporary = $destination + ".copying"
        Write-Host "[COPY] $source to $destination"
        try {
            Copy-Item -LiteralPath $source -Destination $temporary -Force -ErrorAction Stop
            $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
            if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $sourceHash) {
                throw "Installer copy differs from its compiled source."
            }
            Move-Item -LiteralPath $temporary -Destination $destination -Force -ErrorAction Stop
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHash) {
                throw "Final installer verification failed."
            }
        } catch {
            throw "Cannot deliver '$name' to '$installerOutput': $($_.Exception.Message). Compiled originals are preserved in '$stagingInstallers'. Check permissions/file locks and retry with -DeliverOnly."
        }
    }
    Write-Host "[SUCCESS] Installers delivered to: $installerOutput"
    foreach ($name in @("BeRDP.Server.Setup.exe", "BeRDP.Client.Setup.exe")) {
        $file = Get-Item -LiteralPath (Join-Path $installerOutput $name)
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        Write-Host ("{0} | {1} bytes | {2} | SHA256 {3}" -f $file.Name, $file.Length, $file.LastWriteTime, $hash)
    }
    $manifest = foreach ($name in @("BeRDP.Server.Setup.exe", "BeRDP.Client.Setup.exe")) {
        "{0}  {1}" -f (Get-FileHash -LiteralPath (Join-Path $installerOutput $name) -Algorithm SHA256).Hash.ToLowerInvariant(), $name
    }
    [IO.File]::WriteAllLines((Join-Path $installerOutput "SHA256SUMS.txt"), $manifest, [Text.UTF8Encoding]::new($false))
}

if ($DeliverOnly) {
    Write-Host "[INFO] Delivering already compiled staged installers. No rebuild is performed."
    Copy-StagedInstallers
    exit 0
}

if (-not $SkipPublish -and $null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK not found. Install an approved SDK and retry."
}
if ([string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $compiler) {
        $InnoSetupCompilerPath = $compiler.Source
    } else {
        foreach ($candidate in @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        )) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $InnoSetupCompilerPath = $candidate
                break
            }
        }
    }
}
if (-not (Test-Path -LiteralPath $InnoSetupCompilerPath -PathType Leaf)) {
    throw "Inno Setup 6 compiler not found. Supply -InnoSetupCompilerPath."
}
$projects = @(
    @{ Name = "backend"; Project = "BeRdp.Backend"; Exe = "BeRdp.Backend.exe" },
    @{ Name = "admin"; Project = "BeRdp.AdminConsole"; Exe = "BeRdp.AdminConsole.exe" },
    @{ Name = "client"; Project = "BeRdp.Client"; Exe = "BeRdp.Client.exe" },
    @{ Name = "service-setup"; Project = "BeRdp.ServiceSetup"; Exe = "BeRdp.ServiceSetup.exe" }
)
$installerScripts = @(
    (Join-Path $PSScriptRoot "client\BeRdp.Client.iss"),
    (Join-Path $PSScriptRoot "server\BeRdp.Server.iss")
)
foreach ($script in $installerScripts) {
    if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { throw "Installer script missing: $script" }
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
foreach ($directory in @("backend", "admin", "client", "service-setup", "installers")) {
    $path = Join-Path $stagingRoot $directory
    if (-not ([IO.Path]::GetFullPath($path).StartsWith([IO.Path]::GetFullPath($stagingRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) { throw "Unsafe staging path." }
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path | Out-Null
}
$selectedPublishRoot = $stagingRoot
if ($SkipPublish) {
    $selectedPublishRoot = $publishRoot
    Write-Warning "-SkipPublish packages existing binaries. It does not verify that they contain current source changes."
} else {
    Invoke-CheckedCommand "dotnet" @("run", "--project",
        (Join-Path $repoRoot "tests\BeRdp.Regression\BeRdp.Regression.csproj"), "-c", $Configuration,
        "-r", $Runtime, "--self-contained", "true")
    foreach ($project in $projects) {
        $projectPath = Join-Path $repoRoot ("src\{0}\{0}.csproj" -f $project.Project)
        Invoke-CheckedCommand "dotnet" @("restore", $projectPath, "-r", $Runtime)
        Invoke-CheckedCommand "dotnet" @("clean", $projectPath, "-c", $Configuration, "-r", $Runtime)
        $publishArguments = @("publish", $projectPath, "-c", $Configuration, "-r", $Runtime,
            "--self-contained", "true", "-o", (Join-Path $stagingRoot $project.Name))
        if ($project.Name -eq "service-setup") {
            $publishArguments += @("-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true")
        }
        Invoke-CheckedCommand "dotnet" $publishArguments
    }
    Invoke-CheckedCommand (Join-Path $stagingRoot "service-setup\BeRdp.ServiceSetup.exe") @("--self-test")
}
foreach ($project in $projects) {
    $path = Join-Path (Join-Path $selectedPublishRoot $project.Name) $project.Exe
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "Published executable missing or empty: $path"
    }
    $unsafeFiles = Get-ChildItem -LiteralPath (Join-Path $selectedPublishRoot $project.Name) -Recurse -File |
        Where-Object { $_.Name -match '(?i)(\.db(-wal|-shm)?$|\.pfx$|\.p12$|\.log$|\.bak$|DemoFixture|BeRdp\.Regression)' -or $_.FullName -match '[\\/]DataProtectionKeys[\\/]' }
    if ($unsafeFiles) { throw "Release tree contains runtime data, credentials, logs or test binaries: $($unsafeFiles.Name -join ', ')" }
}
foreach ($script in $installerScripts) {
    Invoke-CheckedCommand $InnoSetupCompilerPath @("/DRepoRoot=$repoRoot", "/DPublishRoot=$selectedPublishRoot",
        "/DOutputDir=$stagingInstallers", $script)
}
$installerNames = @("BeRDP.Server.Setup.exe", "BeRDP.Client.Setup.exe")
foreach ($name in $installerNames) {
    $file = Get-Item -LiteralPath (Join-Path $stagingInstallers $name) -ErrorAction Stop
    if ($file.Length -le 0 -or $file.LastWriteTime -lt $buildStartedAt.AddSeconds(-2)) {
        throw "Installer output is empty or stale: $name"
    }
}
Copy-StagedInstallers
