; GRoute installer - Inno Setup 6
; Build with: iscc GRoute.iss

#define AppName "GRoute"
#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#define AppPublisher "Oracle"
#define AppExe "GRoute.exe"
; look in both possible publish locations and fail early with a clear message
#define PubRid SourcePath + "bin\Release\net10.0\win-x64\publish"
#define PubPlain SourcePath + "bin\Release\net10.0\publish"
#if FileExists(PubRid + "\GRoute.exe")
  #define PublishDir PubRid
#elif FileExists(PubPlain + "\GRoute.exe")
  #define PublishDir PubPlain
#else
  #pragma message "Looked in: " + PubRid
  #pragma message "Looked in: " + PubPlain
  #error GRoute.exe not found. Run "dotnet publish -c Release" in this folder first.
#endif

[Setup]
AppId={{7C4B1E92-3A6D-4F58-9B21-5E8A0D3C7F14}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
#if FileExists(SourcePath + "Assets\GRoute.ico")
  #define IcoFile SourcePath + "Assets\GRoute.ico"
#elif FileExists(SourcePath + "Assets\icon.ico")
  #define IcoFile SourcePath + "Assets\icon.ico"
#elif FileExists(SourcePath + "Assets\app.ico")
  #define IcoFile SourcePath + "Assets\app.ico"
#elif FileExists(PublishDir + "\Assets\GRoute.ico")
  #define IcoFile PublishDir + "\Assets\GRoute.ico"
#endif
#ifdef IcoFile
SetupIconFile={#IcoFile}
#else
  #pragma message "No Assets\GRoute.ico found - installer will use the default Inno icon."
#endif
OutputDir={#SourcePath}dist
OutputBaseFilename=GRoute-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no
DisableProgramGroupPage=yes
MinVersion=10.0

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start {#AppName} when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "GRoute"; ValueData: """{app}\{#AppExe}"" --autostart"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; stop anything the app may have left running
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM tor.exe"; Flags: runhidden skipifdoesntexist; RunOnceId: "killtor"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM xray.exe"; Flags: runhidden skipifdoesntexist; RunOnceId: "killxray"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM sing-box.exe"; Flags: runhidden skipifdoesntexist; RunOnceId: "killsing"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM aether.exe"; Flags: runhidden skipifdoesntexist; RunOnceId: "killaether"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM tun2socks-windows-amd64.exe"; Flags: runhidden skipifdoesntexist; RunOnceId: "killtun"

; undo machine state the app can set
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound"; Flags: runhidden; RunOnceId: "fwrestore"
Filename: "{sys}\route.exe"; Parameters: "delete 0.0.0.0 mask 128.0.0.0"; Flags: runhidden; RunOnceId: "route1"
Filename: "{sys}\route.exe"; Parameters: "delete 128.0.0.0 mask 128.0.0.0"; Flags: runhidden; RunOnceId: "route2"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Temp\groute-config.json"

[Code]
procedure CurUninstallStepChanged(CurStep: TUninstallStep);
var
  KeepData: Boolean;
begin
  if CurStep = usPostUninstall then
  begin
    KeepData := MsgBox('Keep your servers, subscriptions and usage history?' + #13#10 +
                       'Choose No to remove them completely.',
                       mbConfirmation, MB_YESNO) = IDYES;
    if not KeepData then
    begin
      DelTree(ExpandConstant('{userappdata}\GRoute'), True, True, True);
    end;
  end;
end;
