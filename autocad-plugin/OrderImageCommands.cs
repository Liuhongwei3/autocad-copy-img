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
  /// 从本地目录按工单号读取 PNG，并插入到当前 AutoCAD 图纸。
  /// 前端与此插件共享文件名规则：{sanitize(record.code)}.png。
  /// </summary>
  public static class OrderImageCommands
  {
    private const string SettingsDirectoryName = "OrderImageCad";
    private const string SettingsFileName = "settings.txt";
    private const string SlotBlockName = "ORDER_IMG_SLOT";
    private const string SlotOrderCodeAttributeTag = "ORDER_CODE";

    [CommandMethod("ORDERIMGCONFIG", CommandFlags.Modal)]
    public static void Configure()
    {
      Document document = Application.DocumentManager.MdiActiveDocument;
      if (document == null)
      {
        return;
      }

      Editor editor = document.Editor;
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
    /// 将指定工单图片自动插入当前空间中唯一的 ORDER_IMG_SLOT 占位块。
    /// 插入成功后会删除占位块。
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
    /// 批量读取当前空间中 ORDER_IMG_SLOT 块的 ORDER_CODE 属性，
    /// 为每个存在对应 PNG 的占位块自动插入图片。
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
        RasterImageDef imageDefinition = new RasterImageDef();
        imageDefinition.SourceFileName = imagePath;
        imageDefinition.Load();

        string definitionName = string.Format(
          "ORDERIMG_{0}",
          Guid.NewGuid().ToString("N")
        );
        ObjectId imageDefinitionId = imageDictionary.SetAt(
          definitionName,
          imageDefinition
        );
        transaction.AddNewlyCreatedDBObject(imageDefinition, true);

        BlockTableRecord currentSpace = (BlockTableRecord)transaction.GetObject(
          database.CurrentSpaceId,
          OpenMode.ForWrite
        );

        RasterImage image = new RasterImage();
        image.ImageDefId = imageDefinitionId;
        image.ShowImage = true;
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

        RasterImage.EnableReactors(true);
        image.AssociateRasterDef(imageDefinition);
        if (!slotId.IsNull)
        {
          Entity slot = transaction.GetObject(slotId, OpenMode.ForWrite) as Entity;
          if (slot != null && !slot.IsErased)
          {
            slot.Erase();
          }
        }
        transaction.Commit();
      }
    }

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

    private static string GetSettingsPath()
    {
      string appDataPath = Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData
      );
      return Path.Combine(appDataPath, SettingsDirectoryName, SettingsFileName);
    }

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

    private sealed class OrderImageSettings
    {
      public string ImageDirectory { get; set; }

      public double Width { get; set; }

      public double Height { get; set; }
    }

    private sealed class OrderImageSlot
    {
      public ObjectId Id { get; set; }

      public Point3d Position { get; set; }

      public double Rotation { get; set; }

      public string OrderCode { get; set; }
    }
  }
}
