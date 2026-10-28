param([string]$Image = 'mcr.microsoft.com/dotnet/aspnet:10.0.14')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$qa = Join-Path ([IO.Path]::GetTempPath()) ('BeRDP-Docker-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa, "$qa/app", "$qa/data", "$qa/tls" | Out-Null
$container = 'berdp-qa-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
function Assert-True($Condition, $Message) {
    if (-not $Condition) { throw $Message }
    Write-Host "PASS $Message"
}
try {
    dotnet publish "$root/src/BeRdp.Backend" -c Release -r linux-x64 --self-contained false -o "$qa/app" --nologo
    if ($LASTEXITCODE) { throw 'Linux test publish failed' }
    $password = 'Test!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(20))
    $certPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName('localhost'); $san.AddIpAddress([Net.IPAddress]::Loopback)
    $request.CertificateExtensions.Add($san.Build())
    $cert = $request.CreateSelfSigned([DateTimeOffset]::Now.AddMinutes(-5),[DateTimeOffset]::Now.AddDays(1))
    [IO.File]::WriteAllBytes("$qa/tls/test.pfx", $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$certPassword))
    [IO.File]::WriteAllText("$qa/tls/test.pem", $cert.ExportCertificatePem())
    $environment = @(
        'ASPNETCORE_ENVIRONMENT=Production', 'ASPNETCORE_URLS=https://+:8443;http://+:8080',
        'Storage__DataDirectory=/data',
        ('Jwt__Key=' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))),
        'Kestrel__Certificates__Default__Path=/tls/test.pfx',
        ('Kestrel__Certificates__Default__Password=' + $certPassword)
    )
    [IO.File]::WriteAllLines("$qa/test.env",$environment,[Text.UTF8Encoding]::new($false))
    $mounts = @('--env-file',"$qa/test.env",'--mount',"type=bind,source=$qa/app,target=/app,readonly",
        '--mount',"type=bind,source=$qa/data,target=/data",'--mount',"type=bind,source=$qa/tls,target=/tls,readonly",'-w','/app')
    $password | docker run --rm -i @mounts $Image dotnet BeRdp.Backend.dll --bootstrap-admin docker-admin *> "$qa/bootstrap.log"
    if ($LASTEXITCODE) { throw "Bootstrap failed; local log: $qa/bootstrap.log" }
    docker run -d --name $container --read-only --tmpfs /tmp --cap-drop ALL @mounts -p 127.0.0.1:15501:8443 -p 127.0.0.1:15500:8080 $Image dotnet BeRdp.Backend.dll | Out-Null
    if ($LASTEXITCODE) { throw 'Container launch failed' }
    @{ password=$password; certificate="$qa/tls/test.pem" } | ConvertTo-Json -Compress | python "$PSScriptRoot/Docker.Probe.py"
    if ($LASTEXITCODE) { throw 'Docker HTTPS probe failed' }
} finally {
    docker rm -f $container 2>$null | Out-Null
    # Test data remains in a uniquely named OS temp folder, never in the release tree.
}
