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
AppId={{C2092FBC-AB82-4CDE-A7B5-73D019E324AF}
AppName=工单图片 AutoCAD 插件
AppVersion={#AppVersion}
AppPublisher=OrderImageCad
DefaultDirName={autopf}\OrderImageCad
CreateAppDir=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=OrderImageCadSetup-{#AutoCADVersion}
Compression=lzma
SolidCompression=yes
UninstallDisplayName=工单图片 AutoCAD 插件（AutoCAD {#AutoCADVersion}）

[Files]
Source: "{#BundleSourceDir}\*"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\OrderImageCad.bundle"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Autodesk\ApplicationPlugins\OrderImageCad.bundle"
