# 一次完成“编译 DLL -> 生成 AutoCAD .bundle -> 生成 Setup.exe”。
# 必须在 Windows 上执行，且机器需安装目标版本的完整版 AutoCAD、
# 对应 .NET SDK 和 Inno Setup 6。客户电脑不运行此脚本。
[CmdletBinding()]
param(
  # 必填。版本决定所需的 .NET 目标框架和 AutoCAD RuntimeRequirements。
  [Parameter(Mandatory = $true)]
  [ValidateSet("2024", "2025", "2026")]
  [string]$AutoCADVersion,

  # 为空时默认使用 C:\Program Files\Autodesk\AutoCAD <版本>。
  [string]$AutoCADInstallDir,

  # 为空时自动在 Inno Setup 6 的常规安装目录中寻找 ISCC.exe。
  [string]$InnoSetupCompiler,

  # 写入 PackageContents.xml 和安装程序元数据的插件版本号。
  [string]$AppVersion = "1.0.0"
)

# StrictMode 能及早暴露拼写错误或未初始化变量；Stop 让任一失败立即中断，
# 不会误生成一个缺少 DLL 的安装包。
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# AutoCAD 2025 更换为现代 .NET，因此每个年版都必须明确指定框架和内部版本号。
# Series 会被写入 PackageContents.xml，防止错误版本的 AutoCAD 加载不兼容 DLL。
$versionTargets = @{
  "2024" = @{
    Framework = "net48"
    Series = "R24.3"
  }
  "2025" = @{
    Framework = "net8.0-windows"
    Series = "R25.0"
  }
  "2026" = @{
    Framework = "net8.0-windows"
    Series = "R25.1"
  }
}

function Get-InnoSetupCompilerPath {
  param([string]$RequestedPath)

  # 用户指定路径时优先使用，适用于 Inno Setup 安装在非默认磁盘的情况。
  if ($RequestedPath) {
    if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
      return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    throw "找不到指定的 Inno Setup 编译器：$RequestedPath"
  }

  # Inno Setup 6 的常规 64 位 Windows 安装位置。
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

  # 统一错误信息，方便排查 AutoCAD 安装目录或构建产物路径。
  if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "找不到$Description：$Path"
  }
}

# 编译依赖 dotnet CLI；AutoCAD 自身并不提供此命令。
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw "未找到 dotnet。请安装 .NET SDK 后重新运行。"
}

# 这些路径均相对脚本位置计算，因此必须从仓库任意目录均可执行。
$target = $versionTargets[$AutoCADVersion]
$scriptDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptDirectory
$projectPath = Join-Path $scriptDirectory "OrderImageCad.csproj"
$installerScriptPath = Join-Path $scriptDirectory "installer.iss"

if (!$AutoCADInstallDir) {
  $AutoCADInstallDir = Join-Path $env:ProgramFiles "Autodesk\AutoCAD $AutoCADVersion"
}

# 编译项目通过 HintPath 引用这两个 AutoCAD 托管 API 程序集。
# 它们属于 AutoCAD 本体，不能复制进安装包。
Assert-FileExists `
  -Path (Join-Path $AutoCADInstallDir "AcMgd.dll") `
  -Description "AutoCAD 托管程序集 AcMgd.dll"
Assert-FileExists `
  -Path (Join-Path $AutoCADInstallDir "AcDbMgd.dll") `
  -Description "AutoCAD 托管程序集 AcDbMgd.dll"
Assert-FileExists -Path $installerScriptPath -Description "Inno Setup 脚本"

# artifacts 是构建过程中的 AutoCAD 自动加载包暂存目录；
# dist 是最终交付给客户的 Setup.exe 输出目录。
$innoSetupCompilerPath = Get-InnoSetupCompilerPath $InnoSetupCompiler
$bundleRoot = Join-Path $repositoryRoot "artifacts\OrderImageCad.bundle"
$bundleContentsDirectory = Join-Path $bundleRoot "Contents\Windows"
$installerOutputDirectory = Join-Path $repositoryRoot "dist"

# 每次构建清空暂存包，避免上一次编译残留的错误版本 DLL 被带入安装包。
if (Test-Path -LiteralPath $bundleRoot) {
  Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $bundleContentsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDirectory -Force | Out-Null

# 使用数组传参，确保带空格的 AutoCAD 安装目录被作为一个完整参数传给 dotnet。
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

# SDK 风格项目的 Release 输出目录由目标框架决定。
$pluginDllPath = Join-Path `
  $scriptDirectory `
  "bin\Release\$($target.Framework)\OrderImageCad.dll"
Assert-FileExists -Path $pluginDllPath -Description "编译后的插件 DLL"
Copy-Item -LiteralPath $pluginDllPath -Destination $bundleContentsDirectory -Force

# PackageContents.xml 是 AutoCAD Autoloader 的清单。
# 安装程序将整个 .bundle 放入标准 ApplicationPlugins 目录后，
# AutoCAD 会在启动时依据此清单加载 DLL，无需客户执行 NETLOAD。
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
      <!-- 必须限制内部版本范围，否则 AutoCAD 可能加载不同 .NET 运行时的 DLL。 -->
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

# Inno Setup 通过预处理宏接收本次构建的 .bundle、输出目录和版本号。
# 其 installer.iss 会把包安装到当前 Windows 用户的 ApplicationPlugins 目录。
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

# 最终文件名与 AutoCAD 年版绑定，便于发布时避免发错安装包。
$setupPath = Join-Path `
  $installerOutputDirectory `
  "OrderImageCadSetup-$AutoCADVersion.exe"
Assert-FileExists -Path $setupPath -Description "生成的安装包"

Write-Host ""
Write-Host "生成成功：$setupPath"
Write-Host "请在 AutoCAD $AutoCADVersion 中安装并执行 README 的验收步骤。"
