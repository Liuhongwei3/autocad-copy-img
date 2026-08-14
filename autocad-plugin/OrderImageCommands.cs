using System;
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
      OrderImageSettings settings = ReadSettings();
      if (
        string.IsNullOrWhiteSpace(settings.ImageDirectory) ||
        !Directory.Exists(settings.ImageDirectory)
      )
      {
        editor.WriteMessage(
          "\n尚未配置有效图片目录。请先执行 ORDERIMGCONFIG。"
        );
        return;
      }

      PromptStringOptions orderCodeOptions = new PromptStringOptions(
        "\n请输入工单编号（record.code）: "
      );
      orderCodeOptions.AllowSpaces = true;
      PromptResult orderCodeResult = editor.GetString(orderCodeOptions);
      if (orderCodeResult.Status != PromptStatus.OK)
      {
        return;
      }

      string imageFileName = GetImageFileName(orderCodeResult.StringResult);
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
          settings.Height
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

    private static void InsertRasterImage(
      Database database,
      string imagePath,
      Point3d insertionPoint,
      double width,
      double height
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
          new Vector3d(width, 0, 0),
          new Vector3d(0, height, 0)
        );
        currentSpace.AppendEntity(image);
        transaction.AddNewlyCreatedDBObject(image, true);

        RasterImage.EnableReactors(true);
        image.AssociateRasterDef(imageDefinition);
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
  }
}
