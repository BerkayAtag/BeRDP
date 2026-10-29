#define AppName "BeRDP Server"
#define AppVersion "1.7.0"
#define AppPublisher "Berkay Atağ"
#define ConsoleUrl "http://localhost:5173/"

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
AppId={{D97649F0-71F9-4A5F-8ED3-21990CCFA6DF}
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
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\BeRDP\Server
DefaultGroupName=BeRDP
; Tıkla-kur: hoş geldiniz, klasör ve program grubu sayfaları sorulmaz.
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
OutputDir={#OutputDir}
OutputBaseFilename=BeRDP.Server.Setup
SetupIconFile={#RepoRoot}\branding\BeRDP.ico
WizardSmallImageFile={#RepoRoot}\branding\setup-small.bmp,{#RepoRoot}\branding\setup-small-150.bmp,{#RepoRoot}\branding\setup-small-200.bmp
; Programlar ve Özellikler (appwiz.cpl) simgesi ve görünen adı.
UninstallDisplayIcon={app}\BeRDP.ico
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
WizardStyle=modern
UninstallFilesDir={commonappdata}\BeRDP\Server\Uninstall
SetupLogging=yes
CloseApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#RepoRoot}\branding\BeRDP.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishRoot}\backend\*"; DestDir: "{app}\Backend"; Excludes: "appsettings.json,*.db,*.db-shm,*.db-wal,DataProtectionKeys\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\admin\*"; DestDir: "{app}\AdminConsole"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\backend\appsettings.json"; DestDir: "{app}\Backend"; Flags: onlyifdoesntexist
Source: "{#PublishRoot}\admin\appsettings.json"; DestDir: "{app}\AdminConsole"; Flags: onlyifdoesntexist
Source: "{#PublishRoot}\service-setup\BeRdp.ServiceSetup.exe"; DestDir: "{app}\ServiceSetup"; Flags: ignoreversion
Source: "{#PublishRoot}\service-setup\BeRdp.ServiceSetup.exe"; Flags: dontcopy
Source: "{#OutputDir}\BeRDP.Client.Setup.exe"; DestDir: "{app}\installers"; Flags: ignoreversion

[Icons]
Name: "{group}\BeRDP Yönetim Konsolu"; Filename: "{#ConsoleUrl}"; IconFilename: "{app}\BeRDP.ico"; Comment: "BeRDP yönetim konsolunu tarayıcıda açar"
Name: "{group}\BeRDP Servisleri"; Filename: "{sys}\services.msc"; IconFilename: "{app}\BeRDP.ico"
Name: "{group}\BeRDP Server Kaldır"; Filename: "{uninstallexe}"; IconFilename: "{app}\BeRDP.ico"

[Run]
Filename: "{#ConsoleUrl}"; Description: "BeRDP yönetim konsolunu aç"; Flags: postinstall shellexec nowait skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\BeRDP.ico"
Type: dirifempty; Name: "{app}\Backend"
Type: dirifempty; Name: "{app}\AdminConsole"
Type: dirifempty; Name: "{app}\Scripts"
Type: dirifempty; Name: "{app}\ServiceSetup"
Type: dirifempty; Name: "{app}\installers"
Type: dirifempty; Name: "{app}"
Type: dirifempty; Name: "{autopf}\BeRDP"

[Code]
#include "..\shared\Firewall.iss"

const
  SetupPasswordVariable = 'BERDP_SETUP_ADMIN_PASSWORD';

var
  AdminPage: TInputQueryWizardPage;
  AdminUsername, AdminPassword: String;

function SetEnvironmentVariable(lpName, lpValue: String): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

function IsFreshDatabase: Boolean;
begin
  // The first administrator can only be created in an empty database; an
  // existing BeRDP database keeps its accounts on upgrade and reinstall.
  Result := not FileExists(ExpandConstant('{commonappdata}\BeRDP\berdp.db'));
end;

function IsValidAdminName(const Name: String): Boolean;
var
  Index: Integer;
begin
  Result := (Length(Name) >= 3) and (Length(Name) <= 64) and
    (Pos(Lowercase(Name[1]), 'abcdefghijklmnopqrstuvwxyz0123456789') > 0);
  if Result then
    for Index := 1 to Length(Name) do
      if Pos(Lowercase(Name[Index]), 'abcdefghijklmnopqrstuvwxyz0123456789._-') = 0 then
        Result := False;
end;

procedure InitializeWizard();
begin
  AdminPage := CreateInputQueryPage(wpWelcome, 'İlk yönetici hesabı',
    'BeRDP yönetim konsoluna girişte kullanacağınız hesabı oluşturun.',
    'Parola en az 14 karakter olmalıdır. Parola kurulum günlüğüne veya herhangi bir dosyaya yazılmaz.');
  AdminPage.Add('Kullanıcı adı:', False);
  AdminPage.Add('Parola:', True);
  AdminPage.Add('Parola (tekrar):', True);
  AdminPage.Values[0] := 'admin';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = AdminPage.ID) and not IsFreshDatabase;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Name: String;
begin
  Result := True;
  if CurPageID <> AdminPage.ID then
    exit;
  Name := Lowercase(Trim(AdminPage.Values[0]));
  if not IsValidAdminName(Name) then
  begin
    MsgBox('Kullanıcı adı 3–64 karakter olmalı ve yalnız harf, rakam, nokta, tire veya alt çizgi içermelidir.', mbError, MB_OK);
    Result := False;
  end
  else if Length(AdminPage.Values[1]) < 14 then
  begin
    MsgBox('Parola en az 14 karakter olmalıdır.', mbError, MB_OK);
    Result := False;
  end
  else if AdminPage.Values[1] <> AdminPage.Values[2] then
  begin
    MsgBox('Parolalar eşleşmiyor.', mbError, MB_OK);
    Result := False;
  end
  else
  begin
    AdminUsername := Name;
    AdminPassword := AdminPage.Values[1];
  end;
end;

function RunServiceSetup(const ToolPath, Stage: String): String;
var
  ExitCode: Integer;
  Parameters, LogPath, SsoParameter: String;
begin
  Result := '';
  if not FileExists(ToolPath) then
  begin
    Result := 'BeRDP servis kurulum aracı bulunamadı: ' + ToolPath;
    Log('ERROR: ' + Result);
    exit;
  end;
  LogPath := ExpandConstant('{commonappdata}\BeRDP\Logs\ServiceSetup-') +
    Stage + '-' + GetDateTimeString('yyyymmdd-hhnnss', '-', ':') + '.log';
  Parameters := '--mode "' + Stage + '" --root "' + ExpandConstant('{app}') +
    '" --log "' + LogPath + '"';
  SsoParameter := ExpandConstant('{param:SSO|}');
  if (Stage = 'install') and (SsoParameter <> '') then
    Parameters := Parameters + ' --sso "' + SsoParameter + '"';
  Log('Service setup stage: ' + Stage);
  Log('Service log: ' + LogPath);
  if not Exec(ToolPath, Parameters, ExtractFileDir(ToolPath),
    SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'BeRDP servis kurulumu başlatılamadı (' + Stage + '). Windows hatası ' +
      IntToStr(ExitCode) + ': ' + SysErrorMessage(ExitCode);
    if ExitCode = 1260 then
      Result := Result + #13#10 + 'Uygulama kurum politikası tarafından engellendi. Sistem yöneticinize başvurun.'
    else if ExitCode = 5 then
      Result := Result + #13#10 + 'Dosya erişim izinlerini ve uç nokta korumasını kontrol edin.'
    else if ExitCode = 740 then
      Result := Result + #13#10 + 'Kurulumu yetkili bir yönetici hesabıyla çalıştırın.';
  end
  else if ExitCode <> 0 then
    Result := 'BeRDP servis adımı başarısız oldu (' + Stage + ', çıkış kodu ' + IntToStr(ExitCode) +
      '). Ayrıntılar: ' + LogPath;
  if Result <> '' then
    Log('ERROR: ' + Result);
end;

procedure CreateFirstAdministrator;
var
  ExitCode: Integer;
  BackendDirectory: String;
begin
  if AdminPassword = '' then
    exit;
  BackendDirectory := ExpandConstant('{app}\Backend');
  WizardForm.StatusLabel.Caption := 'İlk yönetici hesabı oluşturuluyor...';
  SetEnvironmentVariable(SetupPasswordVariable, AdminPassword);
  try
    if not Exec(BackendDirectory + '\BeRdp.Backend.exe',
      '--contentRoot "' + BackendDirectory + '" --bootstrap-admin "' + AdminUsername + '"',
      BackendDirectory, SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
    begin
      Log('ERROR: first administrator bootstrap failed with code ' + IntToStr(ExitCode));
      SuppressibleMsgBox('İlk yönetici hesabı oluşturulamadı. Kurulum tamamlandı; hesabı daha sonra yönetici PowerShell ile oluşturabilirsiniz:' + #13#10#13#10 +
        '"' + BackendDirectory + '\BeRdp.Backend.exe" --contentRoot "' + BackendDirectory +
        '" --bootstrap-admin ' + AdminUsername, mbError, MB_OK, IDOK);
    end
    else
      Log('First administrator created: ' + AdminUsername);
  finally
    SetEnvironmentVariable(SetupPasswordVariable, '');
    AdminPassword := '';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  SsoParameter: String;
begin
  SsoParameter := ExpandConstant('{param:SSO|}');
  if (SsoParameter <> '') and (SsoParameter <> '0') and (SsoParameter <> '1') then
  begin
    Result := 'SSO parametresi 0 veya 1 olmalıdır.';
    exit;
  end;
  ExtractTemporaryFile('BeRdp.ServiceSetup.exe');
  Result := RunServiceSetup(ExpandConstant('{tmp}\BeRdp.ServiceSetup.exe'), 'prepare');
  if Result <> '' then
    Result := Result + #13#10 + 'Uygulama dosyaları değiştirilmedi.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ServiceError: String;
begin
  if CurStep <> ssPostInstall then
    exit;
  // Earlier releases opened HTTP ports; services now listen on loopback only.
  RemoveBeRdpFirewallRule('BeRDP Backend API TCP 5001');
  RemoveBeRdpFirewallRule('BeRDP Admin Console TCP 5173');
  WizardForm.StatusLabel.Caption := 'BeRDP servisleri yapılandırılıyor...';
  ServiceError := RunServiceSetup(ExpandConstant('{app}\ServiceSetup\BeRdp.ServiceSetup.exe'), 'install');
  if ServiceError <> '' then
    RaiseException(ServiceError);
  CreateFirstAdministrator;
end;

function InitializeUninstall(): Boolean;
var
  ServiceError: String;
begin
  ServiceError := RunServiceSetup(ExpandConstant('{app}\ServiceSetup\BeRdp.ServiceSetup.exe'), 'uninstall');
  Result := ServiceError = '';
  if not Result then
    SuppressibleMsgBox(ServiceError + #13#10 +
      'Kurulumun bozulmaması için kaldırma iptal edildi.', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveBeRdpFirewallRule('BeRDP Backend API TCP 5001');
    RemoveBeRdpFirewallRule('BeRDP Admin Console TCP 5173');
  end;
end;
