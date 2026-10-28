#define AppName "BeRDP Server"
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
AppId={{D97649F0-71F9-4A5F-8ED3-21990CCFA6DF}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Berkay Atağ
VersionInfoCompany=Berkay Atağ
DefaultDirName={autopf}\BeRDP\Server
DefaultGroupName=BeRDP
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=BeRDP.Server.Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
WizardStyle=modern
UninstallFilesDir={commonappdata}\BeRDP\Server\Uninstall
VersionInfoVersion={#AppVersion}
SetupLogging=yes
LicenseFile={#RepoRoot}\LICENSE

[Files]
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#PublishRoot}\backend\*"; DestDir: "{app}\Backend"; Excludes: "appsettings.json,*.db,*.db-shm,*.db-wal,DataProtectionKeys\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\admin\*"; DestDir: "{app}\AdminConsole"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishRoot}\backend\appsettings.json"; DestDir: "{app}\Backend"; Flags: onlyifdoesntexist
Source: "{#PublishRoot}\admin\appsettings.json"; DestDir: "{app}\AdminConsole"; Flags: onlyifdoesntexist
Source: "{#PublishRoot}\service-setup\BeRdp.ServiceSetup.exe"; DestDir: "{app}\ServiceSetup"; Flags: ignoreversion
Source: "{#PublishRoot}\service-setup\BeRdp.ServiceSetup.exe"; Flags: dontcopy
Source: "{#OutputDir}\BeRDP.Client.Setup.exe"; DestDir: "{app}\installers"; Flags: ignoreversion

[Icons]
Name: "{group}\BeRDP Service Manager"; Filename: "{sys}\services.msc"

[UninstallDelete]
Type: dirifempty; Name: "{app}\Backend"
Type: dirifempty; Name: "{app}\AdminConsole"
Type: dirifempty; Name: "{app}\Scripts"
Type: dirifempty; Name: "{app}\ServiceSetup"
Type: dirifempty; Name: "{app}"
Type: dirifempty; Name: "{autopf}\BeRDP"

[Code]
#include "..\shared\Firewall.iss"

function WebNetworks: String;
begin
  Result := ExpandConstant('{param:PORTALNETWORKS|}');
  if Result = '' then
    Result := ExpandConstant('{param:ADMINNETWORKS|LocalSubnet}');
end;

function RunServiceSetup(const ToolPath, Stage: String): String;
var
  ExitCode: Integer;
  Parameters, LogPath, SsoParameter: String;
begin
  Result := '';
  if not FileExists(ToolPath) then
  begin
    Result := 'BeRDP service setup executable was not found: ' + ToolPath;
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
  Log('Service setup executable: ' + ToolPath);
  Log('Service log: ' + LogPath);
  if not Exec(ToolPath, Parameters, ExtractFileDir(ToolPath),
    SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Cannot start BeRDP service setup for ' + Stage +
      '. Windows error ' + IntToStr(ExitCode) + ': ' + SysErrorMessage(ExitCode);
    if ExitCode = 1260 then
      Result := Result + #13#10 +
        'Windows blocked the BeRDP executable by organizational policy. Contact your administrator. No policy bypass is attempted.'
    else if ExitCode = 5 then
      Result := Result + #13#10 +
        'Check executable access permissions and endpoint protection with your administrator.'
    else if ExitCode = 740 then
      Result := Result + #13#10 + 'Run Setup with an authorized administrator account.';
  end
  else if ExitCode <> 0 then
    Result := 'BeRDP service ' + Stage + ' failed with exit code ' + IntToStr(ExitCode) +
      '. See the setup log and service setup log: ' + LogPath;
  if Result <> '' then
    Log('ERROR: ' + Result);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  SsoParameter: String;
begin
  SsoParameter := ExpandConstant('{param:SSO|}');
  if (SsoParameter <> '') and (SsoParameter <> '0') and (SsoParameter <> '1') then
  begin
    Result := 'SSO must be 0 or 1.';
    exit;
  end;
  try
    ValidateBeRdpFirewallScope('5001', ExpandConstant('{param:CLIENTNETWORKS|LocalSubnet}'), 6);
    ValidateBeRdpFirewallScope('5173', WebNetworks, 6);
  except
    Result := 'Invalid server firewall settings: ' + GetExceptionMessage;
    Log('ERROR: ' + Result);
    exit;
  end;
  ExtractTemporaryFile('BeRdp.ServiceSetup.exe');
  Result := RunServiceSetup(ExpandConstant('{tmp}\BeRdp.ServiceSetup.exe'), 'prepare');
  if Result <> '' then
    Result := Result + #13#10 + 'Setup has not replaced application files.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ServiceError, ClientNetworks, AdminNetworks: String;
begin
  if CurStep <> ssPostInstall then
    exit;
  ClientNetworks := ExpandConstant('{param:CLIENTNETWORKS|LocalSubnet}');
  AdminNetworks := WebNetworks;
  try
    RemoveBeRdpFirewallRule('BeRDP Backend API TCP 5001');
    RemoveBeRdpFirewallRule('BeRDP Admin Console TCP 5173');
    ServiceError := RunServiceSetup(ExpandConstant('{app}\ServiceSetup\BeRdp.ServiceSetup.exe'), 'install');
    if ServiceError <> '' then
      RaiseException(ServiceError);
  except
    Log('ERROR: ' + GetExceptionMessage);
    RaiseException(GetExceptionMessage);
  end;
end;

function InitializeUninstall(): Boolean;
var
  ServiceError: String;
begin
  ServiceError := RunServiceSetup(ExpandConstant('{app}\ServiceSetup\BeRdp.ServiceSetup.exe'), 'uninstall');
  Result := ServiceError = '';
  if not Result then
    SuppressibleMsgBox(ServiceError + #13#10 +
      'Uninstall cancelled to preserve the installation.', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveBeRdpFirewallRule('BeRDP Backend API TCP 5001');
    RemoveBeRdpFirewallRule('BeRDP Admin Console TCP 5173');
  end;
end;
