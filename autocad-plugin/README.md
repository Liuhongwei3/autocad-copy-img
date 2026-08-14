# CAD 工单图片插件

此目录提供一个本地 AutoCAD .NET 插件原型，不需要新增服务器。它读取前端下载并解压到本机的 PNG 文件，文件名规则为：

```text
{record.code}.png
```

为兼容 Windows 文件系统，前端和插件都会将 `\ / : * ? " < > |` 替换为 `_`。

## 前端使用

1. 在工单列表选择要处理的工单。
2. 在“批量操作”中点击“下载 CAD 图片”。
3. 浏览器下载 ZIP；将它完整解压到一个固定目录，例如 `D:\OrderImages`。

ZIP 内每个 PNG 的内容是现有页面生成的工单信息和两个二维码，文件名由 `record.code` 生成。浏览器没有权限直接批量写入任意本地目录，因此使用 ZIP 是稳定的交付方式。

## 编译插件

插件必须在安装了完整版 AutoCAD 的 Windows 电脑上编译或运行；AutoCAD LT 不能通过 `NETLOAD` 加载此 DLL。

先确认本机 AutoCAD 版本，再在 Windows 终端执行下列命令。`AutoCADInstallDir` 必须指向该版本安装目录，且框架必须与 AutoCAD 版本匹配。

```powershell
# AutoCAD 2024 及更早版本
dotnet build .\autocad-plugin\OrderImageCad.csproj `
  -p:AutoCADInstallDir="C:\Program Files\Autodesk\AutoCAD 2024" `
  -p:AutoCADTargetFramework=net48

# AutoCAD 2025 及更新版本（按实际安装目录调整）
dotnet build .\autocad-plugin\OrderImageCad.csproj `
  -p:AutoCADInstallDir="C:\Program Files\Autodesk\AutoCAD 2025" `
  -p:AutoCADTargetFramework=net8.0-windows
```

生成的 DLL 位于 `autocad-plugin\bin\Debug\<目标框架>\OrderImageCad.dll`。插件引用 AutoCAD 安装目录中的 `AcMgd.dll` 和 `AcDbMgd.dll`，这些文件不应随 DLL 一起复制或发布。

## 在 AutoCAD 中加载并使用

1. 将 DLL 所在目录加入 AutoCAD 的 `TRUSTEDPATHS`，不要通过降低 `SECURELOAD` 绕过安全限制。
2. 在命令行执行 `NETLOAD`，选择编译生成的 `OrderImageCad.dll`。
3. 执行 `ORDERIMGCONFIG`：
   - 输入前端 ZIP 的解压目录，例如 `D:\OrderImages`；
   - 输入图片宽度与高度。这两个数值使用当前图纸单位，例如图纸单位为毫米时可设置为 `90` 和 `45`。
4. 执行 `ORDERIMG`：
   - 输入工单号（`record.code`）；
   - 在图中点击图片左下角的插入位置。

插件会按设定尺寸插入对应 PNG，并插入当前模型空间或当前布局空间。首次版保留用户点位，因此不要求所有图纸使用相同的版式。

## 图纸交付注意事项

AutoCAD 将 PNG 作为外部栅格图像引用。移动或交付 DWG 时，必须同时保留图片目录，或使用 AutoCAD 的 `ETRANSMIT` 打包 DWG 与关联图片；否则打开图纸时可能找不到图片。
