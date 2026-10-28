#define AppName "BeRDP Client"
#define AppVersion "1.6.0"

#ifndef RepoRoot
  #error RepoRoot preprocessor define is required.
#endif

#ifndef OutputDir
  #error OutputDir preprocessor define is required.
#endif

#ifndef PublishRoot
  #define PublishRoot RepoRoot + "\dist\publish"
#endif

[Setup]
AppId={{A6384144-615C-4858-9D23-B1FB889A2D79}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Berkay Atağ
VersionInfoCompany=Berkay Atağ
DefaultDirName={autopf}\BeRDP\Client
DefaultGroupName=BeRDP
OutputDir={#OutputDir}
OutputBaseFilename=BeRDP.Client.Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
WizardStyle=modern
DisableProgramGroupPage=yes
VersionInfoVersion={#AppVersion}
SetupLogging=yes
LicenseFile={#RepoRoot}\LICENSE

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "rdphost"; Description: "Configure this computer as an RDP target (GPU, AVC444, TCP/UDP firewall)"; GroupDescription: "Machine role:"; Flags: unchecked

[Files]
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#PublishRoot}\client\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\client\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\BeRDP Client"; Filename: "{app}\BeRdp.Client.exe"
Name: "{autodesktop}\BeRDP Client"; Filename: "{app}\BeRdp.Client.exe"; IconFilename: "{app}\BeRdp.Client.exe"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "SOFTWARE\BeRDP\Client"; ValueType: string; ValueName: "BackendBaseUrl"; ValueData: "{code:ClientBackendUrl}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\BeRDP\Client"; ValueType: dword; ValueName: "UseWindowsSso"; ValueData: "{code:ClientSsoFlag}"; Flags: uninsdeletevalue
; Windows RDP consent and enterprise client policies are preserved.
; RDP host: prefer UDP/TCP, AVC444, hardware encoding, and the hardware GPU renderer.
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services"; ValueType: dword; ValueName: "SelectTransport"; ValueData: "2"; Tasks: rdphost
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services"; ValueType: dword; ValueName: "AVC444ModePreferred"; ValueData: "1"; Tasks: rdphost
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services"; ValueType: dword; ValueName: "AVCHardwareEncodePreferred"; ValueData: "1"; Tasks: rdphost
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services"; ValueType: dword; ValueName: "bEnumerateHWBeforeSW"; ValueData: "1"; Tasks: rdphost

[Run]
Filename: "{app}\BeRdp.Client.exe"; Description: "Launch BeRDP Client"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
#include "..\shared\Firewall.iss"

var
  BackendUrlPage: TInputQueryWizardPage;
  DeploymentUrl: String;
  DeploymentSso: Boolean;

function ValidateBackendUrl(const Url: String; Sso: Boolean): String;
var
  Authority, Host, PortText: String;
  Index, Separator, Port: Integer;
  NumericHost: Boolean;
begin
  Result := '';
  if Copy(Lowercase(Url), 1, 8) = 'https://' then
    Authority := Copy(Url, 9, Length(Url))
  else if Copy(Lowercase(Url), 1, 7) = 'http://' then
    Authority := Copy(Url, 8, Length(Url))
  else
  begin
    Result := 'BACKENDURL must be an HTTP/HTTPS URL.';
    exit;
  end;
  if (Pos('@', Url) > 0) or (Pos('?', Url) > 0) or (Pos('#', Url) > 0) or
    (Pos('\', Url) > 0) or (Pos('"', Url) > 0) then
  begin
    Result := 'BACKENDURL must not contain credentials, queries, fragments, quotes or backslashes.';
    exit;
  end;
  for Index := 1 to Length(Url) do
    if Ord(Url[Index]) <= 32 then
    begin
      Result := 'BACKENDURL must not contain whitespace.';
      exit;
    end;
  Separator := Pos('/', Authority);
  if Separator > 0 then
    Authority := Copy(Authority, 1, Separator - 1);
  Host := Authority;
  Separator := Pos(':', Authority);
  if Separator > 0 then
  begin
    Host := Copy(Authority, 1, Separator - 1);
    PortText := Copy(Authority, Separator + 1, Length(Authority));
    Port := StrToIntDef(PortText, 0);
    if (Port < 1) or (Port > 65535) then
    begin
      Result := 'BACKENDURL port must be between 1 and 65535.';
      exit;
    end;
  end;
  if Host = '' then
  begin
    Result := 'BACKENDURL hostname is required.';
    exit;
  end;
  if (Length(Host) > 253) or (Pos('..', Host) > 0) or (Host[1] = '.') or
    (Host[1] = '-') or (Host[Length(Host)] = '-') then
  begin
    Result := 'BACKENDURL hostname is invalid.';
    exit;
  end;
  NumericHost := True;
  for Index := 1 to Length(Host) do
  begin
    if Pos(Host[Index], '0123456789.') = 0 then
      NumericHost := False;
    if Pos(Lowercase(Host[Index]), 'abcdefghijklmnopqrstuvwxyz0123456789.-') = 0 then
    begin
      Result := 'Use a DNS hostname or IPv4 address in BACKENDURL.';
      exit;
    end;
  end;
  if (Copy(Lowercase(Url), 1, 7) = 'http://') and (Lowercase(Host) <> 'localhost') and (Host <> '127.0.0.1') then
  begin
    Result := 'Remote BACKENDURL requires HTTPS.';
    exit;
  end;
  if Sso and (NumericHost or (Pos('.', Host) = 0) or (Lowercase(Host) = 'localhost')) then
    Result := 'SSO requires the domain DNS hostname in BACKENDURL, not IP/localhost.';
end;

function ValidateRdpWarningParameter: String;
var
  WarningParameter: String;
begin
  Result := '';
  WarningParameter := ExpandConstant('{param:RDPWARNING|1}');
  if WarningParameter <> '1' then
    Result := 'Windows RDP security warnings are preserved; RDPWARNING=0 is no longer supported.';
end;

procedure InitializeWizard();
var
  SsoParameter, UrlParameter: String;
  ExistingSso: Cardinal;
begin
  DeploymentSso := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\BeRDP\Client', 'UseWindowsSso', ExistingSso) then
    DeploymentSso := ExistingSso = 1;
  SsoParameter := ExpandConstant('{param:SSO|}');
  if SsoParameter <> '' then
  begin
    if (SsoParameter <> '0') and (SsoParameter <> '1') then
      RaiseException('SSO must be 0 or 1.');
    DeploymentSso := SsoParameter = '1';
  end;
  DeploymentUrl := '';
  RegQueryStringValue(HKLM, 'SOFTWARE\BeRDP\Client', 'BackendBaseUrl', DeploymentUrl);
  UrlParameter := ExpandConstant('{param:BACKENDURL|}');
  if UrlParameter <> '' then
    DeploymentUrl := UrlParameter;
  BackendUrlPage := CreateInputQueryPage(wpSelectDir, 'BeRDP Server',
    'Configure the backend address', 'The address is managed by Setup and is not shown in the client login window. The Admin Console address is detected automatically.');
  BackendUrlPage.Add('Backend URL (API or Admin Console, e.g. https://berdp.aknet.akb):', False);
  BackendUrlPage.Values[0] := DeploymentUrl;
end;

function RdpPort: String;
var
  Port: Integer;
begin
  Result := ExpandConstant('{param:RDPPORT|3389}');
  Port := StrToIntDef(Result, 0);
  if (Port < 1) or (Port > 65535) then
    RaiseException('RDPPORT must be an integer from 1 to 65535.');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := ValidateRdpWarningParameter;
  if Result <> '' then
  begin
    Log('ERROR: ' + Result);
    exit;
  end;
  DeploymentUrl := Trim(BackendUrlPage.Values[0]);
  if WizardSilent and (DeploymentUrl = '') then
  begin
    Result := 'BACKENDURL is required for silent Client Setup. Use /BACKENDURL=https://server.example:5001.';
    Log('ERROR: ' + Result);
    exit;
  end;
  Result := ValidateBackendUrl(DeploymentUrl, DeploymentSso);
  if (Result = '') and WizardIsTaskSelected('rdphost') then
  begin
    try
      ValidateBeRdpFirewallScope(RdpPort, ExpandConstant('{param:RDPNETWORKS|LocalSubnet}'), 6);
      ValidateBeRdpFirewallScope(RdpPort, ExpandConstant('{param:RDPNETWORKS|LocalSubnet}'), 17);
    except
      Result := 'Invalid RDP firewall settings: ' + GetExceptionMessage;
    end;
  end;
  if Result <> '' then
    Log('ERROR: ' + Result);
end;

function ClientBackendUrl(Param: String): String;
begin
  Result := DeploymentUrl;
end;

function ClientSsoFlag(Param: String): String;
begin
  if DeploymentSso then Result := '1' else Result := '0';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Port, RemoteAddresses: String;
begin
  if CurStep <> ssPostInstall then
    exit;
  try
    RemoveBeRdpFirewallRule('BeRDP RDP UDP 3389');
    if WizardIsTaskSelected('rdphost') then
    begin
      Port := RdpPort;
      RemoteAddresses := ExpandConstant('{param:RDPNETWORKS|LocalSubnet}');
      SetBeRdpFirewallRule('BeRDP RDP Host TCP',
        ExpandConstant('{sys}\svchost.exe'), Port, RemoteAddresses, 'TermService', 6);
      SetBeRdpFirewallRule('BeRDP RDP Host UDP',
        ExpandConstant('{sys}\svchost.exe'), Port, RemoteAddresses, 'TermService', 17);
    end;
  except
    Log('ERROR: ' + GetExceptionMessage);
    RaiseException(GetExceptionMessage);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveBeRdpFirewallRule('BeRDP RDP Host TCP');
    RemoveBeRdpFirewallRule('BeRDP RDP Host UDP');
    RemoveBeRdpFirewallRule('BeRDP RDP UDP 3389');
  end;
end;
