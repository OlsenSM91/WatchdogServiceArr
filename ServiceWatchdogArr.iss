[Setup]
AppName=ServiceWatchdogArr
AppVersion=1.0.0
AppPublisher=G&H Dev
AppPublisherURL=https://gandh.dev
AppSupportURL=https://gandh.dev
AppUpdatesURL=https://gandh.dev/updates
DefaultDirName={localappdata}\ServiceWatchdogArr
DefaultGroupName=ServiceWatchdogArr
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=packaging\output
OutputBaseFilename=ServiceWatchdogArr-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=0,6.1
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Files]
Source: "bin\Release\net9.0-windows\ServiceWatchdogArr.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\ServiceWatchdogArr.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\ServiceWatchdogArr.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\ServiceWatchdogArr.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net9.0-windows\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\cs\*"; DestDir: "{app}\cs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\de\*"; DestDir: "{app}\de"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\es\*"; DestDir: "{app}\es"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\fr\*"; DestDir: "{app}\fr"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\it\*"; DestDir: "{app}\it"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\ko\*"; DestDir: "{app}\ko"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\pl\*"; DestDir: "{app}\pl"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\ru\*"; DestDir: "{app}\ru"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\tr\*"; DestDir: "{app}\tr"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\zh-Hans\*"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net9.0-windows\zh-Hant\*"; DestDir: "{app}\zh-Hant"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Service Watchdog Arr"; Filename: "{app}\ServiceWatchdogArr.exe"; IconFilename: "{app}\Resources\icon.ico"
Name: "{group}\{cm:UninstallProgram,Service Watchdog Arr}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Resources\icon.ico"
Name: "{code:GetDesktopPath}\Service Watchdog Arr"; Filename: "{app}\ServiceWatchdogArr.exe"; Tasks: desktopicon; IconFilename: "{app}\Resources\icon.ico"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Service Watchdog Arr"; Filename: "{app}\ServiceWatchdogArr.exe"; Tasks: quicklaunchicon; IconFilename: "{app}\Resources\icon.ico"

[Run]
Filename: "{app}\ServiceWatchdogArr.exe"; Description: "{cm:LaunchProgram,Service Watchdog Arr}"; Flags: nowait postinstall skipifsilent

[Code]
function IsElevated: Boolean;
begin
  Result := IsAdminLoggedOn or IsPowerUserLoggedOn;
end;

function GetDefaultDirName(Param: String): String;
begin
  if IsElevated then
    Result := ExpandConstant('{pf}\ServiceWatchdogArr')
  else
    Result := ExpandConstant('{localappdata}\ServiceWatchdogArr');
end;

function GetDesktopPath(Param: String): String;
begin
  if ExpandConstant('{app}') = ExpandConstant('{localappdata}\ServiceWatchdogArr') then
    Result := ExpandConstant('{userdesktop}')
  else
    Result := ExpandConstant('{commondesktop}');
end;

procedure InitializeWizard();
var
  DirPage: TWizardPage;
  RadioButton1, RadioButton2: TRadioButton;
  Label1: TLabel;
begin
  DirPage := CreateCustomPage(wpSelectDir, 'Select Installation Location', 'Choose where to install Service Watchdog Arr');

  Label1 := TLabel.Create(DirPage);
  Label1.Parent := DirPage.Surface;
  Label1.Caption := 'Choose the installation location:';
  Label1.Left := 0;
  Label1.Top := 0;
  Label1.Width := DirPage.SurfaceWidth;

  RadioButton1 := TRadioButton.Create(DirPage);
  RadioButton1.Parent := DirPage.Surface;
  RadioButton1.Caption := 'Install for current user only (AppData - Recommended)';
  RadioButton1.Left := 20;
  RadioButton1.Top := 30;
  RadioButton1.Width := DirPage.SurfaceWidth - 20;
  RadioButton1.Checked := True;

  RadioButton2 := TRadioButton.Create(DirPage);
  RadioButton2.Parent := DirPage.Surface;
  RadioButton2.Caption := 'Install for all users (Program Files - Requires administrator privileges)';
  RadioButton2.Left := 20;
  RadioButton2.Top := 60;
  RadioButton2.Width := DirPage.SurfaceWidth - 20;
  RadioButton2.Enabled := IsElevated;

  if not IsElevated then
  begin
    Label1 := TLabel.Create(DirPage);
    Label1.Parent := DirPage.Surface;
    Label1.Caption := 'Note: To install for all users, please run this installer as administrator.';
    Label1.Left := 40;
    Label1.Top := 85;
    Label1.Width := DirPage.SurfaceWidth - 40;
    Label1.Font.Style := [fsItalic];
    Label1.Font.Color := clGray;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpSelectDir then
  begin
    // Update the installation directory based on selection
    WizardForm.DirEdit.Text := GetDefaultDirName('');
  end;
end;