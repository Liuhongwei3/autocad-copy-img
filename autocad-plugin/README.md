# CAD 工单图片插件

该插件不需要新增服务器。前端将工单图片导出为 ZIP，AutoCAD 从本地读取 PNG 并按用户点位插入。本文档覆盖开发人员编译、测试、生成一键安装包以及客户使用的完整流程。

## 一、开始前确认

此项目仅支持 Windows 上的**完整版 AutoCAD**；AutoCAD LT 不能加载本 C# DLL。先在客户的 AutoCAD 命令行输入 `ABOUT` 确认版本，再选择对应的一项：

| AutoCAD 版本 | 内部版本 | 编译框架 | 本脚本支持 |
| --- | --- | --- | --- |
| 2024 | `R24.3` | `net48` | 是 |
| 2025 | `R25.0` | `net8.0-windows` | 是 |
| 2026 | `R25.1` | `net8.0-windows` | 是 |

不要用 2024 编译出的 DLL 给 2025/2026 使用，也不要反过来使用。AutoCAD 2025 起更换了 .NET 运行时，混用可能造成加载失败。

用于编译和打包的 Windows 电脑需要安装：

1. 对应版本的完整版 AutoCAD；
2. [.NET SDK](https://dotnet.microsoft.com/download)；
3. 若编译 AutoCAD 2024，还需安装 .NET Framework 4.8 Developer Pack；
4. [Inno Setup 6](https://jrsoftware.org/isinfo.php)（生成客户可双击安装的 `Setup.exe`）。

## 二、前端导出工单图片

1. 在工单列表勾选需要处理的工单。
2. 在“批量操作”中点击“下载 CAD 图片”。
3. 浏览器下载 ZIP，其中每个图片的命名为：

   ```text
   {record.code}.png
   ```

为兼容 Windows 文件系统，前端和插件都会将 `\ / : * ? " < > |` 替换为 `_`。

浏览器没有权限将多个文件直接写入指定目录，因此 ZIP 是必要的交付形式。客户使用前将 ZIP 解压到固定目录，例如 `D:\OrderImages`。

## 三、生成客户安装包

在仓库根目录打开 PowerShell，按 AutoCAD 版本执行**一条命令**：

```powershell
# 首次在当前 PowerShell 窗口允许执行本项目脚本
Set-ExecutionPolicy -Scope Process Bypass

# 为 AutoCAD 2024 生成安装包
.\autocad-plugin\Build-Installer.ps1 -AutoCADVersion 2024

# 为 AutoCAD 2025 生成安装包
.\autocad-plugin\Build-Installer.ps1 -AutoCADVersion 2025

# 为 AutoCAD 2026 生成安装包
.\autocad-plugin\Build-Installer.ps1 -AutoCADVersion 2026
```

如果 AutoCAD 不在默认目录（`C:\Program Files\Autodesk\AutoCAD <版本>`），明确传入安装目录：

```powershell
.\autocad-plugin\Build-Installer.ps1 `
  -AutoCADVersion 2025 `
  -AutoCADInstallDir "D:\Autodesk\AutoCAD 2025"
```

脚本会依次完成以下工作：

1. 检查 AutoCAD 程序集 `AcMgd.dll`、`AcDbMgd.dll`；
2. 以匹配版本的 .NET 框架编译 Release DLL；
3. 生成 AutoCAD 标准 `.bundle` 自动加载包和正确的 `RuntimeRequirements`；
4. 调用 Inno Setup 生成安装程序。

成功后将得到：

```text
dist\
└── OrderImageCadSetup-<版本>.exe
```

每次只会生成本次选择版本对应的一个文件。不要把不同 AutoCAD 版本的安装包发给同一客户。

若提示找不到 `ISCC.exe`，请安装 Inno Setup 6；或者传入编译器路径：

```powershell
.\autocad-plugin\Build-Installer.ps1 `
  -AutoCADVersion 2025 `
  -InnoSetupCompiler "D:\Tools\Inno Setup 6\ISCC.exe"
```

## 四、发布前测试

打包前必须在与目标客户相同的 AutoCAD 大版本上测试一次。

### 4.1 开发测试（仅开发人员）

手工加载 DLL 仅用于测试，客户不需要执行这些步骤：

```powershell
# 示例：AutoCAD 2025
dotnet build .\autocad-plugin\OrderImageCad.csproj -c Release `
  -p:AutoCADInstallDir="C:\Program Files\Autodesk\AutoCAD 2025" `
  -p:AutoCADTargetFramework=net8.0-windows
```

在 AutoCAD 输入 `NETLOAD`，选择：

```text
autocad-plugin\bin\Release\net8.0-windows\OrderImageCad.dll
```

如果 AutoCAD 弹出安全路径提示，将该 DLL 所在目录加入 `TRUSTEDPATHS`。不要通过关闭 `SECURELOAD` 绕过安全限制。

### 4.2 安装包验收

1. 双击本次生成的 `OrderImageCadSetup-<版本>.exe`；
2. 完成安装后重启 AutoCAD；
3. 在 AutoCAD 命令行输入 `ORDERIMGCONFIG`，若命令可识别，说明插件已自动加载；
4. 按“客户日常操作”完成一次真实工单的插入；
5. 卸载安装包后重启 AutoCAD，确认命令不再存在。

安装包将插件安装到当前 Windows 用户的标准 AutoCAD `ApplicationPlugins` 目录，因此客户不需要 `NETLOAD`、手动配置 `TRUSTEDPATHS` 或管理员权限。

## 五、客户日常操作

客户不需要接触编译命令或 DLL。

### 第一次使用

1. 从前端下载 CAD 图片 ZIP，并解压到例如 `D:\OrderImages`；
2. 打开 AutoCAD；
3. 输入 `ORDERIMGCONFIG`；
4. 输入图片目录 `D:\OrderImages`；
5. 输入图片在图纸中的宽和高。数值使用当前图纸单位，例如图纸单位为毫米时可设置 `90` 和 `45`。

配置会自动保存到当前 Windows 用户目录；通常仅需设置一次。

### 每次插入

1. 从前端下载并解压最新图片 ZIP 到已配置的目录；
2. 打开图纸，输入 `ORDERIMG`；
3. 输入或使用扫码枪录入工单号（`record.code`）；
4. 在图纸中点击图片左下角的位置。

插件会按保存的宽高插入图片，并使用当前模型空间或当前布局空间。首次版本故意保留点位选择，因此无需假设所有图纸有相同版式。

## 六、图纸交付注意事项

AutoCAD 将 PNG 作为外部栅格图像引用。移动或交付 DWG 时，必须同时保留图片目录，或使用 AutoCAD 的 `ETRANSMIT` 打包 DWG 与关联图片；否则打开图纸时可能找不到图片。

## 七、常见问题

| 问题 | 处理方式 |
| --- | --- |
| `ORDERIMG` 是未知命令 | 确认安装包与 AutoCAD 大版本一致，重启 AutoCAD 后重试。 |
| 找不到 `xxx.png` | 确认前端 ZIP 已解压到 `ORDERIMGCONFIG` 保存的目录，且输入的是 `record.code`。 |
| 图片尺寸不对 | 重新运行 `ORDERIMGCONFIG`，按当前图纸单位重新设置宽、高。 |
| 移动 DWG 后图片丢失 | 将 PNG 目录随 DWG 一并交付，或使用 `ETRANSMIT`。 |
| 构建脚本找不到 AutoCAD DLL | 传入 `-AutoCADInstallDir`，并确认选择的是完整版 AutoCAD。 |
| 构建脚本找不到 Inno Setup | 安装 Inno Setup 6，或使用 `-InnoSetupCompiler` 指定 `ISCC.exe`。 |
