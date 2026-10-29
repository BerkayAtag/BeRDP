#define AppName "BeRDP Client"
#define AppVersion "1.7.0"
#define AppPublisher "Berkay Atağ"

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
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/BerkayAtag/BeRDP
AppSupportURL=https://github.com/BerkayAtag/BeRDP/issues
AppCopyright=© 2026 {#AppPublisher}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Kurulum
VersionInfoProductName=BeRDP
VersionInfoProductVersion={#AppVersion}
DefaultDirName={autopf}\BeRDP\Client
DefaultGroupName=BeRDP
; Tıkla-kur: hoş geldiniz ve klasör sayfaları sorulmaz.
DisableWelcomePage=yes
DisableDirPage=yes
UsePreviousAppDir=yes
OutputDir={#OutputDir}
OutputBaseFilename=BeRDP.Client.Setup
SetupIconFile={#RepoRoot}\branding\BeRDP.ico
WizardSmallImageFile={#RepoRoot}\branding\setup-small.bmp,{#RepoRoot}\branding\setup-small-150.bmp,{#RepoRoot}\branding\setup-small-200.bmp
; Programlar ve Özellikler (appwiz.cpl) simgesi ve görünen adı.
UninstallDisplayIcon={app}\BeRdp.Client.exe,0
UninstallDisplayName={#AppName}
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

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek kısayollar:"
; RDP hedef ayarı yalnız sessiz/kurumsal dağıtımda (/TASKS=rdphost) seçilebilir.
Name: "rdphost"; Description: "Bu bilgisayarı RDP hedefi olarak yapılandır (GPU, AVC444, TCP/UDP firewall)"; GroupDescription: "Makine rolü:"; Flags: unchecked; Check: WizardSilent

[Files]
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#PublishRoot}\client\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\client\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\BeRDP Client"; Filename: "{app}\BeRdp.Client.exe"
Name: "{group}\BeRDP Client Kaldır"; Filename: "{uninstallexe}"
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
Filename: "{app}\BeRdp.Client.exe"; Description: "BeRDP Client'ı başlat"; Flags: nowait postinstall skipifsilent runasoriginaluser

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
    Result := 'Sunucu adresi https:// ile başlamalıdır.';
    exit;
  end;
  if (Pos('@', Url) > 0) or (Pos('?', Url) > 0) or (Pos('#', Url) > 0) or
    (Pos('\', Url) > 0) or (Pos('"', Url) > 0) then
  begin
    Result := 'Sunucu adresi kimlik bilgisi, sorgu, parça, tırnak veya ters eğik çizgi içeremez.';
    exit;
  end;
  for Index := 1 to Length(Url) do
    if Ord(Url[Index]) <= 32 then
    begin
      Result := 'Sunucu adresi boşluk içeremez.';
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
      Result := 'Sunucu adresindeki port 1-65535 arasında olmalıdır.';
      exit;
    end;
  end;
  if Host = '' then
  begin
    Result := 'Sunucu adresinde sunucu adı eksik.';
    exit;
  end;
  if (Length(Host) > 253) or (Pos('..', Host) > 0) or (Host[1] = '.') or
    (Host[1] = '-') or (Host[Length(Host)] = '-') then
  begin
    Result := 'Sunucu adı geçersiz.';
    exit;
  end;
  NumericHost := True;
  for Index := 1 to Length(Host) do
  begin
    if Pos(Host[Index], '0123456789.') = 0 then
      NumericHost := False;
    if Pos(Lowercase(Host[Index]), 'abcdefghijklmnopqrstuvwxyz0123456789.-') = 0 then
    begin
      Result := 'Sunucu adresinde DNS adı veya IPv4 adresi kullanın.';
      exit;
    end;
  end;
  if (Copy(Lowercase(Url), 1, 7) = 'http://') and (Lowercase(Host) <> 'localhost') and (Host <> '127.0.0.1') then
  begin
    Result := 'Uzak sunucu adresi HTTPS gerektirir.';
    exit;
  end;
  if Sso and (NumericHost or (Pos('.', Host) = 0) or (Lowercase(Host) = 'localhost')) then
    Result := 'Windows SSO için sunucu adresinde IP/localhost değil, domain DNS adı kullanılmalıdır.';
end;

function ValidateRdpWarningParameter: String;
var
  WarningParameter: String;
begin
  Result := '';
  WarningParameter := ExpandConstant('{param:RDPWARNING|1}');
  if WarningParameter <> '1' then
    Result := 'Windows RDP güvenlik uyarıları korunur; RDPWARNING=0 artık desteklenmiyor.';
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
      RaiseException('SSO parametresi 0 veya 1 olmalıdır.');
    DeploymentSso := SsoParameter = '1';
  end;
  DeploymentUrl := '';
  RegQueryStringValue(HKLM, 'SOFTWARE\BeRDP\Client', 'BackendBaseUrl', DeploymentUrl);
  UrlParameter := ExpandConstant('{param:BACKENDURL|}');
  if UrlParameter <> '' then
    DeploymentUrl := UrlParameter;
  BackendUrlPage := CreateInputQueryPage(wpWelcome, 'BeRDP sunucusu',
    'İstemcinin bağlanacağı sunucu adresini girin.',
    'Yöneticinizin verdiği HTTPS adresini yazın. Yönetim konsolu adresi girilirse API adresi otomatik bulunur.');
  BackendUrlPage.Add('Sunucu adresi (ör. https://berdp.example.test/api):', False);
  BackendUrlPage.Values[0] := DeploymentUrl;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ValidationError: String;
begin
  Result := True;
  if CurPageID <> BackendUrlPage.ID then
    exit;
  BackendUrlPage.Values[0] := Trim(BackendUrlPage.Values[0]);
  if BackendUrlPage.Values[0] = '' then
    ValidationError := 'Sunucu adresi zorunludur.'
  else
    ValidationError := ValidateBackendUrl(BackendUrlPage.Values[0], DeploymentSso);
  if ValidationError <> '' then
  begin
    MsgBox(ValidationError, mbError, MB_OK);
    Result := False;
  end;
end;

function RdpPort: String;
var
  Port: Integer;
begin
  Result := ExpandConstant('{param:RDPPORT|3389}');
  Port := StrToIntDef(Result, 0);
  if (Port < 1) or (Port > 65535) then
    RaiseException('RDPPORT 1-65535 arasında bir tam sayı olmalıdır.');
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
    Result := 'Sessiz kurulum için /BACKENDURL=https://berdp.example.test/api parametresi zorunludur.';
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
      Result := 'RDP firewall ayarları geçersiz: ' + GetExceptionMessage;
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
