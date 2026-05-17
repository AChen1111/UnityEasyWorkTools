using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Excel2SoImportReport
{
    public string TablePath { get; set; }

    public string TargetPath { get; set; }

    public int ImportedRows { get; set; }

    public int SkippedRows { get; set; }

    public int CreatedAssets { get; set; }

    public int UpdatedAssets { get; set; }

    public int AssignedFields { get; set; }

    public int ConversionErrors { get; set; }

    public bool Canceled { get; set; }

    /// <summary>
    /// 格式化一条简洁的导入结果，供编辑器日志输出。
    /// </summary>
    public override string ToString()
    {
        if (Canceled) return "Excel2SO import canceled.";

        return $"Excel2SO imported {ImportedRows} rows, skipped {SkippedRows}, " +
               $"created {CreatedAssets} assets, updated {UpdatedAssets} assets, " +
               $"assigned {AssignedFields} fields, conversion errors {ConversionErrors}.";
    }
}

public sealed class Excel2SoExportReport
{
    public string SourcePath { get; set; }

    public string CsvPath { get; set; }

    public int ExportedRows { get; set; }

    public int ExportedColumns { get; set; }

    public int ExportedFields { get; set; }

    public int SkippedColumns { get; set; }

    public int ConversionErrors { get; set; }

    public bool Canceled { get; set; }

    public override string ToString()
    {
        if (Canceled) return "SO2Table export canceled.";

        return $"SO2Table exported {ExportedRows} rows, {ExportedColumns} columns, " +
               $"exported {ExportedFields} fields, skipped {SkippedColumns} columns, " +
               $"conversion errors {ConversionErrors}.";
    }
}

public interface IExcel2SoListAssetImporter
{
    string DefaultTargetAssetPath { get; }

    Excel2SoImportReport Import(string tablePath, string assetPath);

    Excel2SoExportReport Export(string assetPath, string csvPath);
}

public abstract class Excel2SoImporterBase
{
    /// <summary>
    /// 编辑器文件选择面板显示的标题。
    /// </summary>
    protected virtual string FilePanelTitle => $"Import {GetType().Name}";

    /// <summary>
    /// 编辑器文件选择面板默认打开的目录。
    /// </summary>
    protected virtual string FilePanelDirectory => Application.dataPath;

    /// <summary>
    /// 编辑器文件选择面板允许选择的扩展名，多个扩展名用逗号分隔。
    /// </summary>
    protected virtual string FilePanelExtensions => "xlsx,csv";

    /// <summary>
    /// 打开文件选择面板，并导入用户选中的表格文件。
    /// </summary>
    public Excel2SoImportReport ImportFromFilePanel()
    {
        var tablePath = EditorUtility.OpenFilePanel(FilePanelTitle, FilePanelDirectory, FilePanelExtensions);
        if (string.IsNullOrEmpty(tablePath))
        {
            return new Excel2SoImportReport { Canceled = true };
        }

        return Import(tablePath);
    }

    /// <summary>
    /// 读取表格文件，并把解析后的表格交给具体导入器处理。
    /// </summary>
    public Excel2SoImportReport Import(string tablePath)
    {
        try
        {
            var table = ExcelTableReader.Read(tablePath);
            return ImportTable(table, tablePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Excel2SO: Failed to import '{tablePath}'. {ex}");
            return new Excel2SoImportReport
            {
                TablePath = tablePath,
                ConversionErrors = 1
            };
        }
    }

    /// <summary>
    /// 注册列名到序列化字段路径的映射，供基于映射的导入器使用。
    /// </summary>
    protected abstract void Configure(Excel2SoMapping map);

    /// <summary>
    /// 导入已经解析好的表格；子类在这里实现具体目标的写入逻辑。
    /// </summary>
    protected abstract Excel2SoImportReport ImportTable(ExcelTable table, string tablePath);

    /// <summary>
    /// 创建映射对象，并让子类注册所有列到字段的绑定。
    /// </summary>
    protected Excel2SoMapping BuildMapping()
    {
        var mapping = new Excel2SoMapping();
        Configure(mapping);
        return mapping;
    }

    /// <summary>
    /// 统一路径分隔符，并把项目内绝对路径转换为 AssetDatabase 可识别的 Assets/ 路径。
    /// </summary>
    protected static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        path = NormalizePath(path);
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return path;

        if (Path.IsPathRooted(path))
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }
        }

        return path;
    }

    protected static string ResolveFileSystemPath(string path)
    {
        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path)) return string.Empty;

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? path
                : NormalizePath(Path.Combine(projectRoot, path));
        }

        return path;
    }

    protected static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
    }

    /// <summary>
    /// 在 Assets/ 下递归创建缺失的文件夹层级。
    /// </summary>
    protected static void EnsureAssetFolder(string folderPath)
    {
        folderPath = NormalizeAssetPath(folderPath);
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

        if (!folderPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Excel2SO asset folder must be under Assets/: {folderPath}");
        }

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    /// <summary>
    /// 替换非法文件名字符，让表格值可以安全用作资产文件名。
    /// </summary>
    protected static string SanitizeAssetFileName(string rawName)
    {
        rawName = string.IsNullOrWhiteSpace(rawName) ? "NewAsset" : rawName.Trim();

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            rawName = rawName.Replace(invalid, '_');
        }

        return rawName.Replace('/', '_').Replace('\\', '_');
    }

    /// <summary>
    /// 标记资产为已修改，保存并刷新 AssetDatabase，然后在 Project 视图中选中该资产。
    /// </summary>
    protected static void SaveAndSelect(Object selectedObject)
    {
        if (selectedObject != null)
        {
            EditorUtility.SetDirty(selectedObject);
            Selection.activeObject = selectedObject;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

/// <summary>
/// 只复用表格读取能力、不使用字段映射的导入器基类。
/// </summary>
public abstract class ExcelTableImporterBase : Excel2SoImporterBase
{
    /// <summary>
    /// 纯表格导入器不需要配置字段映射。
    /// </summary>
    protected override void Configure(Excel2SoMapping map)
    {
    }
}

/// <summary>
/// 把所有表格行导入到同一个 ScriptableObject 资产的列表字段中。
/// </summary>
public abstract class Excel2SoListAssetImporter<TAsset> : Excel2SoImporterBase, IExcel2SoListAssetImporter
    where TAsset : ScriptableObject
{
    /// <summary>
    /// Import(tablePath) 默认写入的 ScriptableObject 资产路径。
    /// </summary>
    protected abstract string DefaultAssetPath { get; }

    /// <summary>
    /// 目标 ScriptableObject 上的序列化列表字段路径。
    /// </summary>
    protected abstract string ListPropertyPath { get; }

    public string DefaultTargetAssetPath => NormalizeAssetPath(DefaultAssetPath);

    /// <summary>
    /// 为 true 时会跳过没有任何单元格内容的空行。
    /// </summary>
    protected virtual bool SkipEmptyRows => true;

    /// <summary>
    /// 把表格导入到指定的 ScriptableObject 资产路径。
    /// </summary>
    public Excel2SoImportReport Import(string tablePath, string assetPath)
    {
        try
        {
            var table = ExcelTableReader.Read(tablePath);
            return ImportTable(table, tablePath, NormalizeAssetPath(assetPath));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Excel2SO: Failed to import '{tablePath}'. {ex}");
            return new Excel2SoImportReport
            {
                TablePath = tablePath,
                TargetPath = assetPath,
                ConversionErrors = 1
            };
        }
    }

    public Excel2SoExportReport Export(string assetPath, string csvPath)
    {
        assetPath = NormalizeAssetPath(assetPath);
        csvPath = NormalizePath(csvPath);

        var report = new Excel2SoExportReport
        {
            SourcePath = assetPath,
            CsvPath = csvPath
        };

        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
            if (asset == null)
            {
                Debug.LogError($"SO2Table: Source asset '{assetPath}' was not found as {typeof(TAsset).Name}.");
                report.ConversionErrors++;
                return report;
            }

            var mapping = BuildMapping();
            var exportBindings = mapping.ExportableBindings;
            report.SkippedColumns = mapping.Bindings.Count - exportBindings.Count;

            var serializedObject = new SerializedObject(asset);
            serializedObject.Update();

            var listProperty = serializedObject.FindProperty(ListPropertyPath);
            if (listProperty == null || !listProperty.isArray || listProperty.propertyType != SerializedPropertyType.Generic)
            {
                Debug.LogError($"SO2Table: List property '{ListPropertyPath}' was not found on {typeof(TAsset).Name}.");
                report.ConversionErrors++;
                return report;
            }

            var context = new Excel2SoExportContext();
            var headers = new List<string>(exportBindings.Count);
            foreach (var binding in exportBindings)
            {
                headers.Add(binding.ColumnName);
            }

            var rows = new List<IReadOnlyList<string>>(listProperty.arraySize);
            for (var i = 0; i < listProperty.arraySize; i++)
            {
                var element = listProperty.GetArrayElementAtIndex(i);
                var cells = new List<string>(exportBindings.Count);
                foreach (var binding in exportBindings)
                {
                    if (!binding.TryExport(serializedObject, element, asset, context, out var value))
                    {
                        value = string.Empty;
                    }

                    cells.Add(value);
                }

                rows.Add(cells);
                report.ExportedRows++;
            }

            report.ExportedColumns = headers.Count;
            report.ExportedFields = context.ExportedFields;
            report.ConversionErrors += context.ConversionErrors;

            var resolvedCsvPath = ResolveFileSystemPath(csvPath);
            Excel2SoCsvWriter.Write(resolvedCsvPath, headers, rows);

            if (NormalizeAssetPath(csvPath).StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"{GetType().Name}: {report}");
            return report;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SO2Table: Failed to export '{assetPath}' to '{csvPath}'. {ex}");
            report.ConversionErrors++;
            return report;
        }
    }

    /// <summary>
    /// 把表格导入到子类指定的默认资产路径。
    /// </summary>
    protected override Excel2SoImportReport ImportTable(ExcelTable table, string tablePath)
    {
        return ImportTable(table, tablePath, NormalizeAssetPath(DefaultAssetPath));
    }

    /// <summary>
    /// 在目标资产加载完成、列表字段清空之前调用的钩子。
    /// </summary>
    protected virtual void OnBeforeImportAsset(TAsset asset, ExcelTable table)
    {
    }

    /// <summary>
    /// 在所有行完成映射、资产保存之前调用的钩子。
    /// </summary>
    protected virtual void OnAfterImportAsset(TAsset asset, ExcelTable table, Excel2SoImportReport report)
    {
    }

    /// <summary>
    /// 清空目标列表，并为每个非空表格行追加一个映射后的列表元素。
    /// </summary>
    private Excel2SoImportReport ImportTable(ExcelTable table, string tablePath, string assetPath)
    {
        var report = new Excel2SoImportReport
        {
            TablePath = tablePath,
            TargetPath = assetPath
        };

        var asset = LoadOrCreateAsset(assetPath, out var created);
        if (asset == null)
        {
            report.ConversionErrors++;
            return report;
        }

        if (created) report.CreatedAssets++;
        else report.UpdatedAssets++;

        var mapping = BuildMapping();
        var context = new Excel2SoImportContext();
        var serializedObject = new SerializedObject(asset);
        serializedObject.Update();

        var listProperty = serializedObject.FindProperty(ListPropertyPath);
        if (listProperty == null || !listProperty.isArray || listProperty.propertyType != SerializedPropertyType.Generic)
        {
            Debug.LogError($"Excel2SO: List property '{ListPropertyPath}' was not found on {typeof(TAsset).Name}.");
            report.ConversionErrors++;
            return report;
        }

        OnBeforeImportAsset(asset, table);
        listProperty.ClearArray();

        var arrayIndex = 0;
        foreach (var row in table.Rows)
        {
            if (SkipEmptyRows && row.IsEmpty)
            {
                report.SkippedRows++;
                continue;
            }

            listProperty.InsertArrayElementAtIndex(arrayIndex);
            var element = listProperty.GetArrayElementAtIndex(arrayIndex);
            Excel2SoSerializedPropertyUtility.ClearValue(element);
            mapping.Apply(row, serializedObject, element, asset, context);

            arrayIndex++;
            report.ImportedRows++;
        }

        serializedObject.ApplyModifiedProperties();

        report.AssignedFields = context.AssignedFields;
        report.ConversionErrors = context.ConversionErrors;

        OnAfterImportAsset(asset, table, report);
        SaveAndSelect(asset);

        Debug.Log($"{GetType().Name}: {report}");
        return report;
    }

    /// <summary>
    /// 加载已有目标资产；如果路径未被占用，则创建新资产。
    /// </summary>
    private static TAsset LoadOrCreateAsset(string assetPath, out bool created)
    {
        created = false;
        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"Excel2SO: Asset path must be under Assets/: {assetPath}");
            return null;
        }

        var existing = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (mainAsset != null)
        {
            Debug.LogError($"Excel2SO: Existing asset at '{assetPath}' is not a {typeof(TAsset).Name}.");
            return null;
        }

        EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
        var asset = ScriptableObject.CreateInstance<TAsset>();
        AssetDatabase.CreateAsset(asset, assetPath);
        created = true;
        return asset;
    }
}

internal static class Excel2SoCsvWriter
{
    public static void Write(
        string csvPath,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("SO2Table CSV path is empty.", nameof(csvPath));
        }

        var directory = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true));
        WriteRow(writer, headers);
        foreach (var row in rows)
        {
            WriteRow(writer, row);
        }
    }

    private static void WriteRow(TextWriter writer, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(Escape(cells[i]));
        }

        writer.WriteLine();
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

/// <summary>
/// 把每一行表格数据分别导入为一个 ScriptableObject 资产。
/// </summary>
public abstract class Excel2SoRowAssetImporter<TAsset> : Excel2SoImporterBase
    where TAsset : ScriptableObject
{
    /// <summary>
    /// 当表格行没有显式提供资产路径时使用的默认输出目录。
    /// </summary>
    protected abstract string DefaultOutputFolder { get; }

    /// <summary>
    /// 用来显式指定输出资产路径的列名。
    /// </summary>
    protected virtual string AssetPathColumn => "assetPath";

    /// <summary>
    /// 当 assetPath 为空时，用来生成资产文件名的列名。
    /// </summary>
    protected virtual string AssetNameColumn => "assetName";

    /// <summary>
    /// 为 true 时会跳过没有任何单元格内容的空行。
    /// </summary>
    protected virtual bool SkipEmptyRows => true;

    /// <summary>
    /// 为每个非空表格行创建或更新一个 ScriptableObject 资产。
    /// </summary>
    protected override Excel2SoImportReport ImportTable(ExcelTable table, string tablePath)
    {
        var report = new Excel2SoImportReport
        {
            TablePath = tablePath,
            TargetPath = NormalizeAssetPath(DefaultOutputFolder)
        };

        var mapping = BuildMapping();
        var context = new Excel2SoImportContext();

        foreach (var row in table.Rows)
        {
            if (SkipEmptyRows && row.IsEmpty)
            {
                report.SkippedRows++;
                continue;
            }

            var assetPath = ResolveAssetPath(row);
            if (string.IsNullOrEmpty(assetPath))
            {
                report.SkippedRows++;
                Debug.LogWarning($"Excel2SO: Row {row.RowNumber} has no usable assetPath or assetName.");
                continue;
            }

            var asset = LoadOrCreateAsset(assetPath, out var created);
            if (asset == null)
            {
                report.SkippedRows++;
                report.ConversionErrors++;
                continue;
            }

            if (created) report.CreatedAssets++;
            else report.UpdatedAssets++;

            var serializedObject = new SerializedObject(asset);
            serializedObject.Update();
            OnBeforeImportRow(asset, row);
            mapping.Apply(row, serializedObject, null, asset, context);
            serializedObject.ApplyModifiedProperties();
            OnAfterImportRow(asset, row);
            EditorUtility.SetDirty(asset);
            report.ImportedRows++;
        }

        report.AssignedFields = context.AssignedFields;
        report.ConversionErrors += context.ConversionErrors;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{GetType().Name}: {report}");
        return report;
    }

    /// <summary>
    /// 在当前行的目标资产加载完成、字段映射应用之前调用的钩子。
    /// </summary>
    protected virtual void OnBeforeImportRow(TAsset asset, ExcelRow row)
    {
    }

    /// <summary>
    /// 在字段映射已经应用到当前行目标资产之后调用的钩子。
    /// </summary>
    protected virtual void OnAfterImportRow(TAsset asset, ExcelRow row)
    {
    }

    /// <summary>
    /// 按 assetPath、assetName、name、id 的优先级解析当前行的输出资产路径。
    /// </summary>
    protected virtual string ResolveAssetPath(ExcelRow row)
    {
        var explicitAssetPath = NormalizeAssetPath(row.Get(AssetPathColumn));
        if (!string.IsNullOrWhiteSpace(explicitAssetPath))
        {
            return explicitAssetPath;
        }

        var rawName = row.Get(AssetNameColumn);
        if (string.IsNullOrWhiteSpace(rawName)) rawName = row.Get("name");
        if (string.IsNullOrWhiteSpace(rawName)) rawName = row.Get("id");
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        var outputFolder = NormalizeAssetPath(DefaultOutputFolder).TrimEnd('/');
        return $"{outputFolder}/{SanitizeAssetFileName(rawName)}.asset";
    }

    /// <summary>
    /// 加载已有行资产；如果路径未被占用，则创建新资产。
    /// </summary>
    private static TAsset LoadOrCreateAsset(string assetPath, out bool created)
    {
        created = false;
        assetPath = NormalizeAssetPath(assetPath);
        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"Excel2SO: Asset path must be under Assets/: {assetPath}");
            return null;
        }

        var existing = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (mainAsset != null)
        {
            Debug.LogError($"Excel2SO: Existing asset at '{assetPath}' is not a {typeof(TAsset).Name}.");
            return null;
        }

        EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
        var asset = ScriptableObject.CreateInstance<TAsset>();
        AssetDatabase.CreateAsset(asset, assetPath);
        created = true;
        return asset;
    }
}
