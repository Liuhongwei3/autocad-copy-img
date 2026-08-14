using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace OrderImageCad
{
  /// <summary>
  /// AutoCAD 命令入口集合。
  /// <para>
  /// 前端导出的图片与此插件共享文件名规则：
  /// {sanitize(record.code)}.png。插件不会请求业务系统或浏览器，
  /// 只从 ORDERIMGCONFIG 保存的本地目录中读取 PNG。
  /// </para>
  /// <para>
  /// ORDERIMG 为手动点位模式；ORDERIMGSLOT 和 ORDERIMGSLOTS
  /// 根据图纸内的占位块自动定位。
  /// </para>
  /// </summary>
  public static class OrderImageCommands
  {
    // 配置写入当前 Windows 用户的 %APPDATA%\OrderImageCad\settings.txt。
    // 采用用户目录而非 DWG 目录，避免每份图纸重复保存同一套图片目录和尺寸。
    private const string SettingsDirectoryName = "OrderImageCad";
    private const string SettingsFileName = "settings.txt";

    // 占位块及其批量映射属性的固定约定。模板块名称或属性标签改动后，
    // 必须同步修改这里以及 README 中的模板制作说明。
    private const string SlotBlockName = "ORDER_IMG_SLOT";
    private const string SlotOrderCodeAttributeTag = "ORDER_CODE";

    /// <summary>
    /// 命令：ORDERIMGCONFIG。
    /// 配置 PNG 目录和插入尺寸；配置会复用于后续全部插图命令。
    /// </summary>
    [CommandMethod("ORDERIMGCONFIG", CommandFlags.Modal)]
    public static void Configure()
    {
      Document document = Application.DocumentManager.MdiActiveDocument;
      if (document == null)
      {
        return;
      }

      Editor editor = document.Editor;
      // 读取上一次的宽高，将其作为本次命令行的默认值。
      OrderImageSettings current = ReadSettings();

      PromptStringOptions folderOptions = new PromptStringOptions(
        "\n请输入已解压的 CAD 图片目录: "
      );
      folderOptions.AllowSpaces = true;
      PromptResult folderResult = editor.GetString(folderOptions);
      if (folderResult.Status != PromptStatus.OK)
      {
        return;
      }

      string imageDirectory = folderResult.StringResult.Trim();
      if (!Directory.Exists(imageDirectory))
      {
        editor.WriteMessage("\n目录不存在，请先解压前端下载的 ZIP 文件。");
        return;
      }

      double width = PromptPositiveDouble(
        editor,
        "\n请输入图片插入宽度（当前图纸单位）: ",
        current.Width
      );
      if (width <= 0)
      {
        return;
      }

      double height = PromptPositiveDouble(
        editor,
        "\n请输入图片插入高度（当前图纸单位）: ",
        current.Height
      );
      if (height <= 0)
      {
        return;
      }

      // 只在目录存在、宽高均为正数时写盘，避免保存无效配置。
      SaveSettings(
        new OrderImageSettings
        {
          ImageDirectory = imageDirectory,
          Width = width,
          Height = height,
        }
      );
      editor.WriteMessage("\nCAD 工单图片插件配置已保存。");
    }

    /// <summary>
    /// 命令：ORDERIMG。
    /// 手动模式：用户输入工单号后，在当前空间点击图片左下角。
    /// </summary>
    [CommandMethod("ORDERIMG", CommandFlags.Modal)]
    public static void InsertOrderImage()
    {
      Document document = Application.DocumentManager.MdiActiveDocument;
      if (document == null)
      {
        return;
      }

      Editor editor = document.Editor;
      OrderImageSettings settings;
      if (!TryGetConfiguredSettings(editor, out settings))
      {
        return;
      }

      string orderCode = PromptOrderCode(editor);
      if (string.IsNullOrWhiteSpace(orderCode))
      {
        return;
      }

      // 必须使用和前端 ZIP 导出相同的文件名转换规则。
      string imageFileName = GetImageFileName(orderCode);
      string imagePath = Path.Combine(settings.ImageDirectory, imageFileName);
      if (!File.Exists(imagePath))
      {
        editor.WriteMessage(
          string.Format("\n未找到图片文件：{0}", imagePath)
        );
        return;
      }

      PromptPointResult insertionPointResult = editor.GetPoint(
        "\n请选择图片左下角插入点: "
      );
      if (insertionPointResult.Status != PromptStatus.OK)
      {
        return;
      }

      try
      {
        // ObjectId.Null 表示本次不是由占位块触发，无需删除任何块。
        InsertRasterImage(
          document.Database,
          imagePath,
          insertionPointResult.Value,
          settings.Width,
          settings.Height,
          0,
          ObjectId.Null
        );
        editor.WriteMessage(
          string.Format("\n已插入工单图片：{0}", imageFileName)
        );
      }
      catch (Exception exception)
      {
        editor.WriteMessage(
          string.Format("\n插入工单图片失败：{0}", exception.Message)
        );
      }
    }

    /// <summary>
    /// 命令：ORDERIMGSLOT。
    /// 将指定工单图片自动插入当前空间中唯一的 ORDER_IMG_SLOT 占位块。
    /// 插入成功后会删除占位块，防止同一位置被重复插图。
    /// </summary>
    [CommandMethod("ORDERIMGSLOT", CommandFlags.Modal)]
    public static void InsertOrderImageAtSlot()
    {
      Document document = Application.DocumentManager.MdiActiveDocument;
      if (document == null)
      {
        return;
      }

      Editor editor = document.Editor;
      OrderImageSettings settings;
      if (!TryGetConfiguredSettings(editor, out settings))
      {
        return;
      }

      string orderCode = PromptOrderCode(editor);
      if (string.IsNullOrWhiteSpace(orderCode))
      {
        return;
      }

      // 只扫描当前模型空间或当前布局空间，不递归扫描嵌套在其他块中的实体。
      List<OrderImageSlot> slots = FindOrderImageSlots(document.Database);
      if (slots.Count == 0)
      {
        editor.WriteMessage(
          string.Format("\n当前空间未找到 {0} 占位块。", SlotBlockName)
        );
        return;
      }
      if (slots.Count > 1)
      {
        editor.WriteMessage(
          string.Format(
            "\n当前空间找到 {0} 个 {1} 占位块。单工单自动定位要求恰好一个占位块；多个占位块请使用 ORDERIMGSLOTS 并填写 ORDER_CODE 属性。",
            slots.Count,
            SlotBlockName
          )
        );
        return;
      }

      string imageFileName = GetImageFileName(orderCode);
      string imagePath = Path.Combine(settings.ImageDirectory, imageFileName);
      if (!File.Exists(imagePath))
      {
        editor.WriteMessage(
          string.Format("\n未找到图片文件：{0}", imagePath)
        );
        return;
      }

      OrderImageSlot slot = slots[0];
      try
      {
        // 占位块的基点和旋转角会传给图片；宽高仍由统一配置控制。
        InsertRasterImage(
          document.Database,
          imagePath,
          slot.Position,
          settings.Width,
          settings.Height,
          slot.Rotation,
          slot.Id
        );
        editor.WriteMessage(
          string.Format(
            "\n已在 {0} 占位块位置插入工单图片：{1}",
            SlotBlockName,
            imageFileName
          )
        );
      }
      catch (Exception exception)
      {
        editor.WriteMessage(
          string.Format("\n占位块插入工单图片失败：{0}", exception.Message)
        );
      }
    }

    /// <summary>
    /// 命令：ORDERIMGSLOTS。
    /// 批量读取当前空间中 ORDER_IMG_SLOT 块的 ORDER_CODE 属性，
    /// 为每个存在对应 PNG 的占位块自动插入图片。
    /// 缺少属性、缺少文件或插入异常时保留占位块，以便人工修正后重试。
    /// </summary>
    [CommandMethod("ORDERIMGSLOTS", CommandFlags.Modal)]
    public static void InsertOrderImagesAtSlots()
    {
      Document document = Application.DocumentManager.MdiActiveDocument;
      if (document == null)
      {
        return;
      }

      Editor editor = document.Editor;
      OrderImageSettings settings;
      if (!TryGetConfiguredSettings(editor, out settings))
      {
        return;
      }

      List<OrderImageSlot> slots = FindOrderImageSlots(document.Database);
      if (slots.Count == 0)
      {
        editor.WriteMessage(
          string.Format("\n当前空间未找到 {0} 占位块。", SlotBlockName)
        );
        return;
      }

      int insertedCount = 0;
      int emptyCodeCount = 0;
      int missingImageCount = 0;
      int failedCount = 0;

      // 先扫描出全部占位块，再逐个写入图片。每次写入会删除成功的占位块，
      // 但不会影响已保存的其他 ObjectId、位置和工单号。
      foreach (OrderImageSlot slot in slots)
      {
        if (string.IsNullOrWhiteSpace(slot.OrderCode))
        {
          emptyCodeCount += 1;
          continue;
        }

        string imageFileName = GetImageFileName(slot.OrderCode);
        string imagePath = Path.Combine(settings.ImageDirectory, imageFileName);
        if (!File.Exists(imagePath))
        {
          missingImageCount += 1;
          continue;
        }

        try
        {
          InsertRasterImage(
            document.Database,
            imagePath,
            slot.Position,
            settings.Width,
            settings.Height,
            slot.Rotation,
            slot.Id
          );
          insertedCount += 1;
        }
        catch (Exception exception)
        {
          failedCount += 1;
          editor.WriteMessage(
            string.Format(
              "\n工单 {0} 插入失败：{1}",
              slot.OrderCode,
              exception.Message
            )
          );
        }
      }

      editor.WriteMessage(
        string.Format(
          "\n占位块批量插入完成：成功 {0}，缺少 ORDER_CODE {1}，缺少图片 {2}，失败 {3}。",
          insertedCount,
          emptyCodeCount,
          missingImageCount,
          failedCount
        )
      );
    }

    /// <summary>
    /// 所有插图命令的统一前置检查。避免每个命令重复验证本地图片目录。
    /// </summary>
    private static bool TryGetConfiguredSettings(
      Editor editor,
      out OrderImageSettings settings
    )
    {
      settings = ReadSettings();
      if (
        !string.IsNullOrWhiteSpace(settings.ImageDirectory) &&
        Directory.Exists(settings.ImageDirectory)
      )
      {
        return true;
      }

      editor.WriteMessage(
        "\n尚未配置有效图片目录。请先执行 ORDERIMGCONFIG。"
      );
      return false;
    }

    /// <summary>
    /// 从命令行读取 record.code。扫码枪通常以键盘输入方式工作，
    /// 因此可以直接在此提示处扫描。
    /// </summary>
    private static string PromptOrderCode(Editor editor)
    {
      PromptStringOptions orderCodeOptions = new PromptStringOptions(
        "\n请输入工单编号（record.code）: "
      );
      orderCodeOptions.AllowSpaces = true;
      PromptResult orderCodeResult = editor.GetString(orderCodeOptions);
      return orderCodeResult.Status == PromptStatus.OK
        ? orderCodeResult.StringResult
        : string.Empty;
    }

    /// <summary>
    /// 读取正数尺寸。UseDefaultValue 允许用户直接回车复用上一次配置。
    /// </summary>
    private static double PromptPositiveDouble(
      Editor editor,
      string message,
      double defaultValue
    )
    {
      PromptDoubleOptions options = new PromptDoubleOptions(message);
      options.AllowNegative = false;
      options.AllowZero = false;
      options.DefaultValue = defaultValue;
      options.UseDefaultValue = true;

      PromptDoubleResult result = editor.GetDouble(options);
      return result.Status == PromptStatus.OK ? result.Value : 0;
    }

    /// <summary>
    /// 遍历当前空间的直接实体，找出名称为 ORDER_IMG_SLOT 的块参照。
    /// 此处使用只读事务：扫描阶段不修改 DWG，真正插入在 InsertRasterImage 中完成。
    /// </summary>
    private static List<OrderImageSlot> FindOrderImageSlots(Database database)
    {
      List<OrderImageSlot> slots = new List<OrderImageSlot>();
      using (Transaction transaction = database.TransactionManager.StartTransaction())
      {
        BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
          database.CurrentSpaceId,
          OpenMode.ForRead
        );
        foreach (ObjectId entityId in currentSpace)
        {
          BlockReference blockReference = transaction.GetObject(
            entityId,
            OpenMode.ForRead
          ) as BlockReference;
          if (
            blockReference == null ||
            !IsOrderImageSlot(blockReference, transaction)
          )
          {
            continue;
          }

          // 在事务结束前读取并保存必要的值对象；事务结束后不再访问 BlockReference。
          slots.Add(
            new OrderImageSlot
            {
              Id = entityId,
              Position = blockReference.Position,
              Rotation = blockReference.Rotation,
              OrderCode = GetOrderCodeFromSlot(blockReference, transaction),
            }
          );
        }
      }
      return slots;
    }

    /// <summary>
    /// 块参照本身没有名称，名称位于其块定义（BlockTableRecord）中。
    /// </summary>
    private static bool IsOrderImageSlot(
      BlockReference blockReference,
      Transaction transaction
    )
    {
      BlockTableRecord definition = (BlockTableRecord)transaction.GetObject(
        blockReference.BlockTableRecord,
        OpenMode.ForRead
      );
      return string.Equals(
        definition.Name,
        SlotBlockName,
        StringComparison.OrdinalIgnoreCase
      );
    }

    /// <summary>
    /// 从块参照的属性实例中读取 ORDER_CODE，而不是读取块定义中的默认值。
    /// 这样同一个块定义的多个占位块可以对应不同工单。
    /// </summary>
    private static string GetOrderCodeFromSlot(
      BlockReference blockReference,
      Transaction transaction
    )
    {
      foreach (ObjectId attributeId in blockReference.AttributeCollection)
      {
        AttributeReference attribute = transaction.GetObject(
          attributeId,
          OpenMode.ForRead
        ) as AttributeReference;
        if (
          attribute != null &&
          string.Equals(
            attribute.Tag,
            SlotOrderCodeAttributeTag,
            StringComparison.OrdinalIgnoreCase
          )
        )
        {
          return attribute.TextString;
        }
      }
      return string.Empty;
    }

    /// <summary>
    /// 在一个 AutoCAD 事务中创建栅格图片定义、图片实体和可选的占位块删除操作。
    /// 如果过程中任一步骤抛异常，事务不会提交，图片与占位块都会保持原状。
    /// </summary>
    private static void InsertRasterImage(
      Database database,
      string imagePath,
      Point3d insertionPoint,
      double width,
      double height,
      double rotation,
      ObjectId slotId
    )
    {
      using (Transaction transaction = database.TransactionManager.StartTransaction())
      {
        // RasterImageDef 是图像文件的 DWG 内引用定义，所有定义存放在图片字典中。
        ObjectId imageDictionaryId = RasterImageDef.GetImageDictionary(database);
        if (imageDictionaryId.IsNull)
        {
          RasterImageDef.CreateImageDictionary(database);
          imageDictionaryId = RasterImageDef.GetImageDictionary(database);
        }

        DBDictionary imageDictionary = (DBDictionary)transaction.GetObject(
          imageDictionaryId,
          OpenMode.ForWrite
        );
        // SourceFileName 记录外部 PNG 路径；这也是交付 DWG 时需保留图片目录的原因。
        RasterImageDef imageDefinition = new RasterImageDef();
        imageDefinition.SourceFileName = imagePath;
        imageDefinition.Load();

        // 每次插入生成独立且不冲突的字典键，防止多个工单互相覆盖图片定义。
        string definitionName = string.Format(
          "ORDERIMG_{0}",
          Guid.NewGuid().ToString("N")
        );
        ObjectId imageDefinitionId = imageDictionary.SetAt(
          definitionName,
          imageDefinition
        );
        transaction.AddNewlyCreatedDBObject(imageDefinition, true);

        // CurrentSpaceId 同时兼容模型空间和布局空间，不把图片硬编码到模型空间。
        BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
          database.CurrentSpaceId,
          OpenMode.ForWrite
        );

        RasterImage image = new RasterImage();
        image.ImageDefId = imageDefinitionId;
        image.ShowImage = true;
        // Orientation 的两个向量分别是图片宽边、图片高边。
        // 通过占位块旋转角计算向量，使自动插入的图片与占位块方向保持一致。
        image.Orientation = new CoordinateSystem3d(
          insertionPoint,
          new Vector3d(
            width * Math.Cos(rotation),
            width * Math.Sin(rotation),
            0
          ),
          new Vector3d(
            -height * Math.Sin(rotation),
            height * Math.Cos(rotation),
            0
          )
        );
        currentSpace.AppendEntity(image);
        transaction.AddNewlyCreatedDBObject(image, true);

        // 建立 RasterImage 与 RasterImageDef 的关联，避免 AutoCAD 将图片定义视为未引用。
        RasterImage.EnableReactors(true);
        image.AssociateRasterDef(imageDefinition);
        if (!slotId.IsNull)
        {
          // 将删除占位块与插入图片放在同一事务：插入失败时占位块不会丢失。
          Entity slot = transaction.GetObject(slotId, OpenMode.ForWrite) as Entity;
          if (slot != null && !slot.IsErased)
          {
            slot.Erase();
          }
        }
        transaction.Commit();
      }
    }

    /// <summary>
    /// 从三行文本配置读取目录、宽、高。缺失或格式异常时返回安全默认值。
    /// </summary>
    private static OrderImageSettings ReadSettings()
    {
      OrderImageSettings defaults = new OrderImageSettings
      {
        ImageDirectory = string.Empty,
        Width = 500,
        Height = 250,
      };
      string settingsPath = GetSettingsPath();
      if (!File.Exists(settingsPath))
      {
        return defaults;
      }

      string[] values = File.ReadAllLines(settingsPath);
      if (values.Length != 3)
      {
        return defaults;
      }

      double width;
      double height;
      if (
        !double.TryParse(
          values[1],
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out width
        ) ||
        !double.TryParse(
          values[2],
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out height
        ) ||
        width <= 0 ||
        height <= 0
      )
      {
        return defaults;
      }

      return new OrderImageSettings
      {
        ImageDirectory = values[0],
        Width = width,
        Height = height,
      };
    }

    /// <summary>
    /// 使用 InvariantCulture 保存小数，避免不同 Windows 区域设置造成小数点解析失败。
    /// </summary>
    private static void SaveSettings(OrderImageSettings settings)
    {
      string settingsPath = GetSettingsPath();
      Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
      File.WriteAllLines(
        settingsPath,
        new[]
        {
          settings.ImageDirectory,
          settings.Width.ToString(CultureInfo.InvariantCulture),
          settings.Height.ToString(CultureInfo.InvariantCulture),
        }
      );
    }

    /// <summary>
    /// 返回当前用户的配置位置，例如：
    /// C:\Users\&lt;用户名&gt;\AppData\Roaming\OrderImageCad\settings.txt。
    /// </summary>
    private static string GetSettingsPath()
    {
      string appDataPath = Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData
      );
      return Path.Combine(appDataPath, SettingsDirectoryName, SettingsFileName);
    }

    /// <summary>
    /// 将 record.code 转为 Windows 可用的 PNG 文件名。
    /// 此规则必须与前端 getCadImageFileName 保持一致。
    /// </summary>
    private static string GetImageFileName(string orderCode)
    {
      string safeCode = (orderCode ?? string.Empty).Trim();
      char[] forbiddenCharacters = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
      foreach (char forbiddenCharacter in forbiddenCharacters)
      {
        safeCode = safeCode.Replace(forbiddenCharacter, '_');
      }

      return string.Format(
        "{0}.png",
        string.IsNullOrWhiteSpace(safeCode) ? "未命名工单" : safeCode
      );
    }

    /// <summary>
    /// 插入图片时复用的本地用户配置。
    /// </summary>
    private sealed class OrderImageSettings
    {
      public string ImageDirectory { get; set; }

      public double Width { get; set; }

      public double Height { get; set; }
    }

    /// <summary>
    /// 扫描事务中提取的占位块快照。保存值类型和 ObjectId，
    /// 供后续独立写入事务使用。
    /// </summary>
    private sealed class OrderImageSlot
    {
      public ObjectId Id { get; set; }

      public Point3d Position { get; set; }

      public double Rotation { get; set; }

      public string OrderCode { get; set; }
    }
  }
}
