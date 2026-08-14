; 此文件由 Build-Installer.ps1 调用，不建议直接双击编译。
; 三个必需宏由脚本传入，用于保证安装的 DLL 与目标 AutoCAD 年版一致。
#ifndef BundleSourceDir
  #error Build-Installer.ps1 必须传入 BundleSourceDir
#endif

#ifndef OutputDir
  #error Build-Installer.ps1 必须传入 OutputDir
#endif

#ifndef AutoCADVersion
  #error Build-Installer.ps1 必须传入 AutoCADVersion
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; 固定 AppId 使 Inno Setup 能识别同一插件的升级和卸载。
AppId={{C2092FBC-AB82-4CDE-A7B5-73D019E324AF}
AppName=工单图片 AutoCAD 插件
AppVersion={#AppVersion}
AppPublisher=OrderImageCad
; 插件实际不安装到 Program Files，而是安装到当前用户的 AutoCAD 自动加载目录。
; 保留 DefaultDirName 仅满足 Inno Setup 元数据要求，CreateAppDir=no 防止创建无用目录。
DefaultDirName={autopf}\OrderImageCad
CreateAppDir=no
DisableProgramGroupPage=yes
; 使用 {userappdata} 安装，因此不需要管理员权限；每个 Windows 用户独立安装插件。
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=OrderImageCadSetup-{#AutoCADVersion}
Compression=lzma
SolidCompression=yes
UninstallDisplayName=工单图片 AutoCAD 插件（AutoCAD {#AutoCADVersion}）

[Files]
; AutoCAD 启动时会扫描此标准 .bundle 路径，并读取 PackageContents.xml 自动加载 DLL。
Source: "{#BundleSourceDir}\*"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\OrderImageCad.bundle"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; 卸载时只清理插件包，不删除用户保存的图片目录和 %APPDATA% 中的尺寸配置。
Type: filesandordirs; Name: "{userappdata}\Autodesk\ApplicationPlugins\OrderImageCad.bundle"
