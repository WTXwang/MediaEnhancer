; ================================================================
; 影音智增强系统 — Inno Setup 安装脚本
; 使用前请先执行: dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:UseAppHost=true
; ================================================================

[Setup]
AppName=影音智增强系统
AppVersion=1.8.0
AppPublisher=MediaEnhancer
DefaultDirName={autopf}\MediaEnhancer
DefaultGroupName=影音智增强系统
OutputDir=.\installer
OutputBaseFilename=MediaEnhancer_Setup_v1.8.0
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=lowest
DisableDirPage=no
; 以下两行让控制面板显示正确的大小和图标
UninstallDisplayName=影音智增强系统
UninstallDisplayIcon={app}\MediaEnhancer.exe

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; 发布输出目录下的所有文件 — 若文件不存在则编译时警告但不中断
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; 可选：如果 ffmpeg 没有随发布一起打包，安装后自动下载逻辑由程序本身处理

[Icons]
; 开始菜单
Name: "{group}\影音智增强系统"; \
    Filename: "{app}\MediaEnhancer.exe"; \
    WorkingDir: "{app}"
; 卸载
Name: "{group}\卸载 影音智增强系统"; \
    Filename: "{uninstallexe}"
; 桌面快捷方式
Name: "{userdesktop}\影音智增强系统"; \
    Filename: "{app}\MediaEnhancer.exe"; \
    WorkingDir: "{app}"; \
    Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: checkedonce

[Run]
Filename: "{app}\MediaEnhancer.exe"; \
    Description: "{cm:LaunchProgram,影音智增强系统}"; \
    Flags: nowait postinstall skipifsilent

; ================================================================
; 安装前检查
; ================================================================
[Code]
function IsDotNetRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  // .NET 10 自包含发布已内置运行时，无需额外检查
end;
