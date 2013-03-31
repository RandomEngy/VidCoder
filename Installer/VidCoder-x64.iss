; Setup for VidCoder. NOTE: To build this installer you must first build
;  VidCoder.sln in Release|x64 mode.

[Setup]
AppName=VidCoder
AppVerName=VidCoder 1.4.17 Beta (x64)
AppVersion=1.4.17
AppPublisher=RandomEngy
AppPublisherURL=http://vidcoder.codeplex.com/

DefaultDirName={pf}\VidCoder
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\VidCoder.exe
Compression=lzma
SolidCompression=yes

OutputDir=BuiltInstallers
OutputBaseFilename=VidCoder-1.4.17-Beta-x64

AppId=VidCoder-x64
UsePreviousAppDir=yes

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "Files\x64\*"; DestDir: "{app}"; Flags: recursesubdirs

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.

[Icons]
Name: "{commonprograms}\VidCoder"; Filename: "{app}\VidCoder.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\VidCoder.exe"; Description: "Run VidCoder"; Flags: postinstall shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\VidCoder\Updates"
Type: filesandordirs; Name: "{localappdata}\VidCoder"

[CustomMessages]
dotnetmissing=VidCoder requires Microsoft .NET Framework 4, which is not installed. Would you like to download it now?

[Code]

function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
  netFrameWorkInstalled : Boolean;
  isInstalled: Cardinal;
  ErrorCode: Integer;
begin
	GetWindowsVersionEx(Version);
	
	if (Version.Major < 5) or ((Version.Major = 5) and (Version.Minor < 1)) or ((Version.Major = 5) and (Version.Minor = 1) and (Version.ServicePackMajor < 2)) then
	begin
    MsgBox('VidCoder cannot install on your operating system.', mbError, MB_OK);
    exit;
	end;
	
  result := true;

  isInstalled := 0;
  netFrameworkInstalled := RegQueryDWordValue(HKLM, 'Software\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', isInstalled);
  if ((netFrameworkInstalled)  and (isInstalled <> 1)) then netFrameworkInstalled := false;

  if netFrameworkInstalled = false then
  begin
    if (MsgBox(ExpandConstant('{cm:dotnetmissing}'),
        mbConfirmation, MB_YESNO) = idYes) then
    begin
      ShellExec('open',
      'http://www.microsoft.com/downloads/details.aspx?FamilyID=e5ad0459-cbcc-4b4f-97b6-fb17111cf544',
      '','',SW_SHOWNORMAL,ewNoWait,ErrorCode);
    end;
    result := false;
  end;
end;

// Delete app data
procedure DeleteUserFiles();
var
  iAns: Integer;
begin
  iAns := MsgBox('Do you want to delete your settings and presets as well?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2);

  if iAns = IDYES then
  begin
    DelTree(ExpandConstant('{userappdata}') + '\VidCoder', True, True, True);
  end; 
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
      begin
        DeleteUserFiles;
      end;
  end;
end;



















