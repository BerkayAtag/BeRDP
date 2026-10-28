param(
    [string]$BackendBaseUrl = "http://localhost:5001",
    [string]$AdminUsername = "admin",
    [Parameter(Mandatory = $true)]
    [string]$AdminPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-BeRdpApi {
    param([string]$Method, [string]$Path, [string]$Token, [object]$Body)
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers["Authorization"] = "Bearer $Token"
    }
    $request = @{
        Method = $Method
        Uri = $BackendBaseUrl.TrimEnd("/") + $Path
        Headers = $headers
        ContentType = "application/json"
    }
    if ($null -ne $Body) { $request.Body = $Body | ConvertTo-Json -Depth 10 }
    return Invoke-RestMethod @request
}

function Expect-Status {
    param([scriptblock]$Action, [int]$Status)
    try { & $Action | Out-Null }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq $Status) { return }
        throw
    }
    throw "Expected HTTP $Status, but the request succeeded."
}

$userId = $null
$machineId = $null
$token = $null
try {
    Write-Host "1/11 Anonymous AD config is rejected..."
    Expect-Status { Invoke-BeRdpApi "GET" "/ad/config" } 401
    Write-Host "2/11 Admin login and authorization..."
    $login = Invoke-BeRdpApi "POST" "/login" "" @{ username = $AdminUsername; password = $AdminPassword }
    if ([string]::IsNullOrEmpty($login.token) -or -not $login.isAdmin) { throw "Admin login failed." }
    $token = $login.token
    Invoke-BeRdpApi "GET" "/ad/config" $token | Out-Null
    Write-Host "3/11 Health endpoint..."
    if ((Invoke-BeRdpApi "GET" "/health").status -ne "ok") { throw "Backend health failed." }
    Write-Host "4/11 Create standard user..."
    $suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $username = "smoke.$suffix"
    $password = "Regression-" + [Guid]::NewGuid().ToString("N")
    $user = Invoke-BeRdpApi "POST" "/users" $token @{
        username = $username; password = $password; displayName = "Regression User"; isAdmin = $false
    }
    $userId = $user.id
    $userLogin = Invoke-BeRdpApi "POST" "/login" "" @{ username = $username; password = $password }
    Write-Host "5/11 Standard user cannot administer AD..."
    Expect-Status { Invoke-BeRdpApi "GET" "/ad/config" $userLogin.token } 403
    Write-Host "6/11 Invalid AD domain is rejected without modifying settings..."
    Expect-Status {
        Invoke-BeRdpApi "PUT" "/ad/config" $token @{ enabled = $true; domainName = "invalid domain"; useMachineCredentials = $true }
    } 400
    Write-Host "7/11 Create and assign a test machine..."
    $address = "192.0.2." + (Get-Random -Minimum 1 -Maximum 254)
    $machine = Invoke-BeRdpApi "POST" "/machines" $token @{ name = "Regression-$suffix"; ipAddress = $address; rdpPort = 3389 }
    $machineId = $machine.id
    Invoke-BeRdpApi "POST" "/assign-machine" $token @{ userId = $userId; machineId = $machineId } | Out-Null
    $machines = @(Invoke-BeRdpApi "GET" "/machines?skip=0&take=500" $userLogin.token)
    if (-not ($machines | Where-Object { $_.id -eq $machineId })) { throw "Assigned machine is not visible." }
    Write-Host "8/11 Unassign and verify access..."
    Invoke-BeRdpApi "DELETE" "/unassign-machine" $token @{ userId = $userId; machineId = $machineId } | Out-Null
    if ((@(Invoke-BeRdpApi "GET" "/machines" $userLogin.token) | Where-Object { $_.id -eq $machineId })) {
        throw "Unassigned machine is still visible."
    }
    Write-Host "9/11 Role removal changes active-token privileges..."
    Invoke-BeRdpApi "PUT" "/users/$userId/role" $token @{ isAdmin = $true } | Out-Null
    Invoke-BeRdpApi "GET" "/ad/config" $userLogin.token | Out-Null
    Invoke-BeRdpApi "PUT" "/users/$userId/role" $token @{ isAdmin = $false } | Out-Null
    Expect-Status { Invoke-BeRdpApi "GET" "/ad/config" $userLogin.token } 403
    Write-Host "10/11 Password change revokes previous token..."
    Invoke-BeRdpApi "POST" "/account/password" $userLogin.token @{
        currentPassword = $password; newPassword = $password + "2"
    } | Out-Null
    Expect-Status { Invoke-BeRdpApi "GET" "/machines" $userLogin.token } 401
    Write-Host "11/11 Delete user and machine..."
    Invoke-BeRdpApi "DELETE" "/machines/$machineId" $token | Out-Null
    $machineId = $null
    Invoke-BeRdpApi "DELETE" "/users/$userId" $token | Out-Null
    $userId = $null
    Write-Host "SUCCESS: all smoke checks passed."
} finally {
    if ($null -ne $token) {
        if ($null -ne $machineId) {
            try { Invoke-BeRdpApi "DELETE" "/machines/$machineId" $token | Out-Null }
            catch [System.Net.WebException] { Write-Warning "Test machine cleanup failed: $($_.Exception.Message)" }
        }
        if ($null -ne $userId) {
            try { Invoke-BeRdpApi "DELETE" "/users/$userId" $token | Out-Null }
            catch [System.Net.WebException] { Write-Warning "Test user cleanup failed: $($_.Exception.Message)" }
        }
    }
}
