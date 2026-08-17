[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("2024", "2025", "2027")]
  [string]$AutoCADVersion,

  [string]$AutoCADInstallDir,

  [string]$InnoSetupCompiler,

  [string]$AppVersion = "1.0.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$versionTargets = @{
  "2024" = @{
    Framework = "net48"
    Series = "R24.3"
  }
  "2025" = @{
    Framework = "net8.0-windows"
    Series = "R25.0"
  }
  "2027" = @{
    Framework = "net10.0-windows"
    Series = "R26.0"
  }
}

function Get-InnoSetupCompilerPath {
  param([string]$RequestedPath)

  if ($RequestedPath) {
    if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
      return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    throw "找不到指定的 Inno Setup 编译器：$RequestedPath"
  }

  $candidatePaths = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
  )
  foreach ($candidatePath in $candidatePaths) {
    if ($candidatePath -and (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
      return (Resolve-Path -LiteralPath $candidatePath).Path
    }
  }

  throw "未找到 ISCC.exe。请安装 Inno Setup 6，或传入 -InnoSetupCompiler。"
}

function Assert-FileExists {
  param(
    [string]$Path,
    [string]$Description
  )

  if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "找不到$Description：$Path"
  }
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "未找到 dotnet。请安装 .NET SDK 后重新运行。"
}

$target = $versionTargets[$AutoCADVersion]
$scriptDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptDirectory
$projectPath = Join-Path $scriptDirectory "OrderImageCad.csproj"
$installerScriptPath = Join-Path $scriptDirectory "installer.iss"

if (!$AutoCADInstallDir) {
  $AutoCADInstallDir = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $AutoCADVersion"
}

Assert-FileExists `
  -Path (Join-Path $AutoCADInstallDir "AcMgd.dll") `
  -Description "AutoCAD 托管程序集 AcMgd.dll"
Assert-FileExists `
  -Path (Join-Path $AutoCADInstallDir "AcDbMgd.dll") `
  -Description "AutoCAD 托管程序集 AcDbMgd.dll"
Assert-FileExists -Path $installerScriptPath -Description "Inno Setup 脚本"

$innoSetupCompilerPath = Get-InnoSetupCompilerPath $InnoSetupCompiler
$bundleRoot = Join-Path $repositoryRoot "artifacts\OrderImageCad.bundle"
$bundleContentsDirectory = Join-Path $bundleRoot "Contents\Windows"
$installerOutputDirectory = Join-Path $repositoryRoot "dist"

if (Test-Path -LiteralPath $bundleRoot) {
  Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $bundleContentsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

Write-Host "正在编译 AutoCAD $AutoCADVersion 插件..."
$dotnetArguments = @(
  "build",
  $projectPath,
  "-c",
  "Release",
  "-p:AutoCADInstallDir=$AutoCADInstallDir",
  "-p:AutoCADTargetFramework=$($target.Framework)"
)
& dotnet @dotnetArguments
if ($LASTEXITCODE -ne 0) {
  throw "插件编译失败，退出码：$LASTEXITCODE"
}

$pluginDllPath = Join-Path `
  $scriptDirectory `
  "bin\Release\$($target.Framework)\OrderImageCad.dll"
Assert-FileExists -Path $pluginDllPath -Description "编译后的插件 DLL"
Copy-Item -LiteralPath $pluginDllPath -Destination $bundleContentsDirectory -Force

$packageContentsPath = Join-Path $bundleRoot "PackageContents.xml"
@"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage
  SchemaVersion="1.0"
  AutodeskProduct="AutoCAD"
  Name="OrderImageCad"
  Description="工单图片插入插件"
  AppVersion="$AppVersion"
  ProductType="Application">
  <CompanyDetails Name="OrderImageCad" />
  <Components Description="工单图片插入插件">
    <ComponentEntry
      AppName="OrderImageCad"
      ModuleName="./Contents/Windows/OrderImageCad.dll"
      AppDescription="工单图片插入插件"
      AppType=".Net"
      LoadOnAutoCADStartup="True">
      <RuntimeRequirements
        OS="Win64"
        Platform="AutoCAD*"
        SeriesMin="$($target.Series)"
        SeriesMax="$($target.Series)" />
      <Commands GroupName="OrderImageCad">
        <Command Global="ORDERIMGCONFIG" Local="ORDERIMGCONFIG" />
        <Command Global="ORDERIMG" Local="ORDERIMG" />
        <Command Global="ORDERIMGSLOT" Local="ORDERIMGSLOT" />
        <Command Global="ORDERIMGSLOTS" Local="ORDERIMGSLOTS" />
      </Commands>
    </ComponentEntry>
  </Components>
</ApplicationPackage>
"@ | Set-Content -LiteralPath $packageContentsPath -Encoding UTF8

Write-Host "正在生成 AutoCAD .bundle 包..."
Write-Host "正在生成客户安装程序..."
$innoSetupArguments = @(
  "/DBundleSourceDir=$bundleRoot",
  "/DOutputDir=$installerOutputDirectory",
  "/DAutoCADVersion=$AutoCADVersion",
  "/DAppVersion=$AppVersion",
  $installerScriptPath
)
& $innoSetupCompilerPath @innoSetupArguments
if ($LASTEXITCODE -ne 0) {
  throw "安装包生成失败，退出码：$LASTEXITCODE"
}

$setupPath = Join-Path `
  $installerOutputDirectory `
  "OrderImageCadSetup-$AutoCADVersion.exe"
Assert-FileExists -Path $setupPath -Description "生成的安装包"

Write-Host ""
Write-Host "生成成功：$setupPath"
Write-Host "请在 AutoCAD $AutoCADVersion 中安装并执行 README 的验收步骤。"
