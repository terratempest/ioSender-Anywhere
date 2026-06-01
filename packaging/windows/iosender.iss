#define AppName "ioSender"

#ifndef AppVersion
  #error AppVersion must be provided by the packaging script.
#endif

#ifndef SourceDir
  #error SourceDir must be provided by the packaging script.
#endif

#ifndef OutputDir
  #error OutputDir must be provided by the packaging script.
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "ioSender-Setup"
#endif

#ifndef IconPath
  #define IconPath "..\..\Icon\iosendericon.ico"
#endif

[Setup]
AppId={{8F52F15F-65D8-4D4E-B8D8-A2EE6C631C8E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=ioSender
AppPublisherURL=https://github.com/terjeio/ioSender
AppSupportURL=https://github.com/terjeio/ioSender/issues
AppUpdatesURL=https://github.com/terjeio/ioSender/releases
DefaultDirName={autopf}\ioSender
DefaultGroupName=ioSender
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\ioSender.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ioSender"; Filename: "{app}\ioSender.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ioSender"; Filename: "{app}\ioSender.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ioSender.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  AppUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F52F15F-65D8-4D4E-B8D8-A2EE6C631C8E}_is1';

function Unquote(Value: string): string;
begin
  Result := Value;
  if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
    Result := Copy(Result, 2, Length(Result) - 2);
end;

function TryGetExistingUninstaller(RootKey: Integer; var Uninstaller: string): Boolean;
begin
  Result := RegQueryStringValue(RootKey, AppUninstallKey, 'UninstallString', Uninstaller);
  if Result then
    Uninstaller := Unquote(Uninstaller);
end;

function UninstallExistingInstall(RootKey: Integer): Boolean;
var
  ResultCode: Integer;
  Uninstaller: string;
begin
  Result := True;
  if not TryGetExistingUninstaller(RootKey, Uninstaller) then
    exit;

  Log('Existing ioSender install found. Running uninstaller: ' + Uninstaller);
  Result := Exec(
    Uninstaller,
    '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  if not Result then
  begin
    MsgBox('An existing ioSender installation was found, but its uninstaller could not be started. Uninstall ioSender manually, then run this installer again.', mbError, MB_OK);
    exit;
  end;

  if ResultCode <> 0 then
  begin
    MsgBox('An existing ioSender installation was found, but uninstalling it failed with exit code ' + IntToStr(ResultCode) + '. Uninstall ioSender manually, then run this installer again.', mbError, MB_OK);
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := UninstallExistingInstall(HKCU);
  if Result then
    Result := UninstallExistingInstall(HKLM);
end;
