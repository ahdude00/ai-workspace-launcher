#ifndef Payload
  #error Build with installer/build.ps1
#endif
#ifndef AppVersion
#define AppVersion GetVersionNumbersString(AddBackslash(Payload) + "AWL.exe")
#endif

[Setup]
AppId=AWL.AiWorkspaceLauncher
AppName=AWL
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\AWL
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
OutputBaseFilename=AWL-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\AWL.exe

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\AWL"; Filename: "{app}\AWL.exe"

[Run]
Filename: "{app}\AWL.exe"; Description: "AWL uygulamasını aç"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Args: String;
  ExitCode: Integer;
begin
  if CurUninstallStep <> usUninstall then Exit;
  Args := '--uninstall-cleanup';
  if not UninstallSilent then begin
    if MsgBox('AWL ile açılmış Claude oturumlarının geçmişi de silinsin mi?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      Args := Args + ' --remove-awl-history';
    if MsgBox('Kullanıcı hesabınızdaki yerel Claude Code çalıştırılabilir dosyası da kaldırılsın mı? Bu seçim diğer projeleri de etkiler.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      Args := Args + ' --remove-claude';
  end;
  if not Exec(ExpandConstant('{app}\AWL.exe'), Args, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
    RaiseException('AWL temizliği tamamlanamadı. AWL ve Claude pencerelerini kapatıp yeniden deneyin.');
end;
