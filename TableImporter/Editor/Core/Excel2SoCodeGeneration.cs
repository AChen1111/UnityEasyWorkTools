using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEasyWorkTools.Editor;

public enum Excel2SoCodeGenFieldKind
{
    String,
    Int,
    Float,
    Bool,
    Vector2,
    Vector3,
    Color,
    StringList,
    IntList,
    FloatList,
    SpriteAsset,
    GameObjectAsset,
    AudioClipAsset,
    Texture2DAsset,
    MaterialAsset,
    ScriptableObjectAsset,
    ObjectAsset,
    SpriteAssetList,
    GameObjectAssetList,
    AudioClipAssetList,
    Texture2DAssetList,
    MaterialAssetList,
    ScriptableObjectAssetList,
    ObjectAssetList,
    Enum
}

public sealed class Excel2SoCodeGenField
{
    public string ColumnName { get; set; }

    public string PropertyName { get; set; }

    public Excel2SoCodeGenFieldKind Kind { get; set; }

    public string EnumTypeName { get; set; }
}

public sealed class Excel2SoCodeGenOptions
{
    public string NamePrefix { get; set; }

    public string DataClassName { get; set; }

    public string DatabaseClassName { get; set; }

    public string ImporterClassName { get; set; }

    public string ListPropertyName { get; set; }

    public string RuntimeScriptsFolder { get; set; }

    public string EditorScriptsFolder { get; set; }

    public string DefaultAssetPath { get; set; }

    public string CsvPath { get; set; }

    public List<Excel2SoCodeGenField> Fields { get; } = new List<Excel2SoCodeGenField>();
}

public sealed class Excel2SoCodeGenReport
{
    public int CreatedFiles { get; set; }

    public int UpdatedFiles { get; set; }

    public int SkippedUserFiles { get; set; }

    public int Fields { get; set; }

    public bool Canceled { get; set; }

    public List<string> WrittenPaths { get; } = new List<string>();

    public override string ToString()
    {
        if (Canceled) return "Excel2SO code generation canceled. / Excel2SO 代码生成已取消。";

        return $"Excel2SO generated {Fields} fields / 生成 {Fields} 个字段, " +
               $"created {CreatedFiles} files / 新建 {CreatedFiles} 个文件, " +
               $"updated {UpdatedFiles} files / 更新 {UpdatedFiles} 个文件, " +
               $"skipped {SkippedUserFiles} user files / 跳过 {SkippedUserFiles} 个用户文件。";
    }
}

public sealed class Excel2SoCodeGenTypeDefinition
{
    public Excel2SoCodeGenTypeDefinition(
        Excel2SoCodeGenFieldKind kind,
        string displayName,
        string cSharpType,
        string mappingCall,
        bool requiresEnumType = false)
    {
        Kind = kind;
        DisplayName = displayName;
        CSharpType = cSharpType;
        MappingCall = mappingCall;
        RequiresEnumType = requiresEnumType;
    }

    public Excel2SoCodeGenFieldKind Kind { get; }

    public string DisplayName { get; }

    public string CSharpType { get; }

    public string MappingCall { get; }

    public bool RequiresEnumType { get; }

    public string GetCSharpType(Excel2SoCodeGenField field)
    {
        return RequiresEnumType ? field.EnumTypeName?.Trim() : CSharpType;
    }

    public string GetMappingCall(Excel2SoCodeGenField field)
    {
        return RequiresEnumType
            ? $".AsEnum<{field.EnumTypeName?.Trim()}>()"
            : MappingCall;
    }
}

public static class Excel2SoCodeGenTypeRegistry
{
    private static readonly IReadOnlyList<Excel2SoCodeGenTypeDefinition> definitions =
        new List<Excel2SoCodeGenTypeDefinition>
        {
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.String, "string / 字符串", "string", ".AsString()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Int, "int / 整数", "int", ".AsInt()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Float, "float / 浮点数", "float", ".AsFloat()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Bool, "bool / 布尔值", "bool", ".AsBool()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Vector2, "Vector2 / 二维向量", "Vector2", ".AsVector2()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Vector3, "Vector3 / 三维向量", "Vector3", ".AsVector3()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Color, "Color / 颜色", "Color", ".AsColor()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.StringList, "List<string> / 字符串列表", "List<string>", ".AsStringList(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.IntList, "List<int> / 整数列表", "List<int>", ".AsIntList(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.FloatList, "List<float> / 浮点列表", "List<float>", ".AsFloatList(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.SpriteAsset, "Sprite asset / 精灵资产", "Sprite", ".AsAsset<Sprite>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.GameObjectAsset, "GameObject asset / 预制体资产", "GameObject", ".AsAsset<GameObject>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.AudioClipAsset, "AudioClip asset / 音频资产", "AudioClip", ".AsAsset<AudioClip>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Texture2DAsset, "Texture2D asset / 贴图资产", "Texture2D", ".AsAsset<Texture2D>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.MaterialAsset, "Material asset / 材质资产", "Material", ".AsAsset<Material>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.ScriptableObjectAsset, "ScriptableObject asset / SO资产", "ScriptableObject", ".AsAsset<ScriptableObject>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.ObjectAsset, "UnityEngine.Object asset / Unity对象资产", "UnityEngine.Object", ".AsAsset<UnityEngine.Object>()"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.SpriteAssetList, "List<Sprite> / 精灵列表", "List<Sprite>", ".AsAssetList<Sprite>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.GameObjectAssetList, "List<GameObject> / 预制体列表", "List<GameObject>", ".AsAssetList<GameObject>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.AudioClipAssetList, "List<AudioClip> / 音频列表", "List<AudioClip>", ".AsAssetList<AudioClip>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Texture2DAssetList, "List<Texture2D> / 贴图列表", "List<Texture2D>", ".AsAssetList<Texture2D>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.MaterialAssetList, "List<Material> / 材质列表", "List<Material>", ".AsAssetList<Material>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.ScriptableObjectAssetList, "List<ScriptableObject> / SO列表", "List<ScriptableObject>", ".AsAssetList<ScriptableObject>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.ObjectAssetList, "List<UnityEngine.Object> / Unity对象列表", "List<UnityEngine.Object>", ".AsAssetList<UnityEngine.Object>(\";\")"),
            new Excel2SoCodeGenTypeDefinition(Excel2SoCodeGenFieldKind.Enum, "enum / 枚举", null, null, requiresEnumType: true)
        };

    public static IReadOnlyList<Excel2SoCodeGenTypeDefinition> Definitions => definitions;

    public static Excel2SoCodeGenTypeDefinition Get(Excel2SoCodeGenFieldKind kind)
    {
        return definitions.First(definition => definition.Kind == kind);
    }

    public static string Format(Excel2SoCodeGenFieldKind kind)
    {
        return Get(kind).DisplayName;
    }
}

public static class Excel2SoCodeGenerator
{
    private const string GeneratedHeader =
        "// <auto-generated>\n" +
        "// Generated by UnityEasyWorkTools/Table Importer. Manual edits in this file may be overwritten.\n" +
        "// </auto-generated>\n";

    private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    public static bool TryCreateOptions(
        string namePrefix,
        string listPropertyName,
        string runtimeScriptsFolder,
        string editorScriptsFolder,
        string defaultAssetPath,
        string csvPath,
        IEnumerable<Excel2SoCodeGenField> fields,
        out Excel2SoCodeGenOptions options,
        out string message)
    {
        options = null;
        message = string.Empty;

        namePrefix = NormalizeIdentifierInput(namePrefix);
        if (!IsValidIdentifier(namePrefix))
        {
            message = "CodeGen name prefix must be a valid C# identifier, for example Weapon or Item. / CodeGen 名称前缀必须是合法 C# 标识符，例如 Weapon 或 Item。";
            return false;
        }

        var dataClassName = $"{namePrefix}Data";
        var databaseClassName = $"{namePrefix}Database";
        var importerClassName = $"{databaseClassName}ExcelImporter";
        listPropertyName = NormalizeIdentifierInput(listPropertyName);

        var candidate = new Excel2SoCodeGenOptions
        {
            NamePrefix = namePrefix,
            DataClassName = dataClassName,
            DatabaseClassName = databaseClassName,
            ImporterClassName = importerClassName,
            ListPropertyName = listPropertyName,
            RuntimeScriptsFolder = NormalizeAssetPath(runtimeScriptsFolder),
            EditorScriptsFolder = NormalizeAssetPath(editorScriptsFolder),
            DefaultAssetPath = NormalizeAssetPath(defaultAssetPath),
            CsvPath = NormalizeAssetPath(csvPath)
        };

        if (fields != null)
        {
            candidate.Fields.AddRange(fields);
        }

        if (!TryValidate(candidate, out message))
        {
            return false;
        }

        options = candidate;
        return true;
    }

    public static Excel2SoCodeGenReport Generate(Excel2SoCodeGenOptions options)
    {
        var report = new Excel2SoCodeGenReport
        {
            Fields = options?.Fields.Count ?? 0
        };

        if (!TryValidate(options, out var validationMessage))
        {
            EditorUtility.DisplayDialog("Excel2SO CodeGen / 代码生成", validationMessage, "OK / 确定");
            report.Canceled = true;
            return report;
        }

        var generatedFiles = BuildGeneratedFiles(options);
        var userFiles = BuildUserFiles(options);
        if (!TryValidateExistingUserFiles(userFiles))
        {
            report.Canceled = true;
            return report;
        }

        var existingGeneratedFiles = generatedFiles
            .Where(file => File.Exists(ResolveFileSystemPath(file.AssetPath)))
            .Select(file => file.AssetPath)
            .ToArray();

        if (existingGeneratedFiles.Length > 0)
        {
            var confirmMessage =
                "The following generated files already exist and will be overwritten / 以下生成文件已存在，将被覆盖:\n\n" +
                string.Join("\n", existingGeneratedFiles);
            if (!EditorUtility.DisplayDialog("Overwrite Generated Files? / 覆盖生成文件？", confirmMessage, "Overwrite / 覆盖", "Cancel / 取消"))
            {
                report.Canceled = true;
                return report;
            }
        }

        foreach (var file in generatedFiles)
        {
            WriteTextFile(file.AssetPath, file.Content, report, overwrite: true);
        }

        foreach (var file in userFiles)
        {
            var resolvedPath = ResolveFileSystemPath(file.AssetPath);
            if (File.Exists(resolvedPath))
            {
                report.SkippedUserFiles++;
                continue;
            }

            WriteTextFile(file.AssetPath, file.Content, report, overwrite: false);
        }

        AssetDatabase.Refresh();
        Debug.Log($"{nameof(Excel2SoCodeGenerator)}: {report}");
        return report;
    }

    public static string BuildDefaultAssetPath(string namePrefix)
    {
        namePrefix = NormalizeIdentifierInput(namePrefix);
        if (string.IsNullOrEmpty(namePrefix))
        {
            namePrefix = "NewTable";
        }

        return UnityEasyWorkToolsPathSettings.GetOrCreate().BuildGeneratedAssetPath(namePrefix);
    }

    public static string BuildDefaultCsvPath(string namePrefix)
    {
        namePrefix = NormalizeIdentifierInput(namePrefix);
        if (string.IsNullOrEmpty(namePrefix))
        {
            namePrefix = "NewTable";
        }

        return UnityEasyWorkToolsPathSettings.GetOrCreate().BuildGeneratedCsvPath(namePrefix);
    }

    public static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        path = NormalizePath(path);
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return path;

        if (Path.IsPathRooted(path))
        {
            var dataPath = NormalizePath(Application.dataPath);
            if (path.Equals(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (path.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }
        }

        return path;
    }

    private static bool TryValidate(Excel2SoCodeGenOptions options, out string message)
    {
        message = string.Empty;
        if (options == null)
        {
            message = "CodeGen options are empty. / CodeGen 配置为空。";
            return false;
        }

        if (!IsValidIdentifier(options.DataClassName)
            || !IsValidIdentifier(options.DatabaseClassName)
            || !IsValidIdentifier(options.ImporterClassName))
        {
            message = "Generated class names must be valid C# identifiers. / 生成类名必须是合法 C# 标识符。";
            return false;
        }

        if (!IsValidIdentifier(options.ListPropertyName))
        {
            message = "List property name must be a valid C# identifier. / 列表字段名必须是合法 C# 标识符。";
            return false;
        }

        if (!IsAssetFolder(options.RuntimeScriptsFolder))
        {
            message = "Runtime script folder must be under Assets/. / 运行时脚本目录必须在 Assets/ 下。";
            return false;
        }

        if (!IsAssetFolder(options.EditorScriptsFolder))
        {
            message = "Importer script folder must be under Assets/. / 导入器脚本目录必须在 Assets/ 下。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.DefaultAssetPath)
            || !options.DefaultAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            || !options.DefaultAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            message = "Default SO asset path must be an Assets/*.asset path. / 默认 SO 资产路径必须是 Assets/*.asset。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.CsvPath)
            || !options.CsvPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            message = "CSV path must end with .csv. / CSV 路径必须以 .csv 结尾。";
            return false;
        }

        if (!options.CsvPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(options.CsvPath))
        {
            message = "CSV path must be under Assets/ or be an absolute path. / CSV 路径必须在 Assets/ 下或使用绝对路径。";
            return false;
        }

        if (options.Fields.Count == 0)
        {
            message = "Add at least one field. / 至少添加一个字段。";
            return false;
        }

        var duplicateColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in options.Fields)
        {
            if (field == null)
            {
                message = "Field list contains an empty field. / 字段列表中存在空字段。";
                return false;
            }

            field.ColumnName = NormalizeColumnName(field.ColumnName);
            field.PropertyName = NormalizeIdentifierInput(field.PropertyName);
            field.EnumTypeName = field.EnumTypeName?.Trim();

            if (string.IsNullOrEmpty(field.ColumnName))
            {
                message = "Every field must have a CSV column name. / 每个字段都必须有 CSV 列名。";
                return false;
            }

            if (!IsValidIdentifier(field.PropertyName))
            {
                message = $"Property name '{field.PropertyName}' is not a valid C# identifier. / 属性名 '{field.PropertyName}' 不是合法 C# 标识符。";
                return false;
            }

            if (!duplicateColumns.Add(field.ColumnName))
            {
                message = $"Duplicate CSV column / CSV 列名重复: {field.ColumnName}";
                return false;
            }

            if (!duplicateProperties.Add(field.PropertyName))
            {
                message = $"Duplicate property name / 属性名重复: {field.PropertyName}";
                return false;
            }

            var definition = Excel2SoCodeGenTypeRegistry.Get(field.Kind);
            if (definition.RequiresEnumType && !IsValidTypeExpression(field.EnumTypeName))
            {
                message = $"Enum field '{field.PropertyName}' needs a valid enum type name. / 枚举字段 '{field.PropertyName}' 需要合法枚举类型名。";
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<GeneratedFile> BuildGeneratedFiles(Excel2SoCodeGenOptions options)
    {
        return new[]
        {
            new GeneratedFile(
                CombineAssetPath(options.RuntimeScriptsFolder, $"{options.DataClassName}.Generated.cs"),
                BuildDataGeneratedScript(options)),
            new GeneratedFile(
                CombineAssetPath(options.RuntimeScriptsFolder, $"{options.DatabaseClassName}.Generated.cs"),
                BuildDatabaseGeneratedScript(options)),
            new GeneratedFile(
                CombineAssetPath(options.EditorScriptsFolder, $"{options.ImporterClassName}.Generated.cs"),
                BuildImporterGeneratedScript(options)),
            new GeneratedFile(
                options.CsvPath,
                BuildCsvContent(options))
        };
    }

    private static IReadOnlyList<GeneratedFile> BuildUserFiles(Excel2SoCodeGenOptions options)
    {
        return new[]
        {
            new GeneratedFile(
                CombineAssetPath(options.RuntimeScriptsFolder, $"{options.DataClassName}.cs"),
                BuildDataUserScript(options)),
            new GeneratedFile(
                CombineAssetPath(options.RuntimeScriptsFolder, $"{options.DatabaseClassName}.cs"),
                BuildDatabaseUserScript(options))
        };
    }

    private static bool TryValidateExistingUserFiles(IEnumerable<GeneratedFile> userFiles)
    {
        foreach (var file in userFiles)
        {
            var resolvedPath = ResolveFileSystemPath(file.AssetPath);
            if (!File.Exists(resolvedPath))
            {
                continue;
            }

            var content = File.ReadAllText(resolvedPath);
            if (file.AssetPath.EndsWith("Data.cs", StringComparison.Ordinal)
                && content.Contains($"partial struct {Path.GetFileNameWithoutExtension(file.AssetPath)}"))
            {
                continue;
            }

            if (file.AssetPath.EndsWith("Database.cs", StringComparison.Ordinal)
                && content.Contains($"partial class {Path.GetFileNameWithoutExtension(file.AssetPath)}"))
            {
                continue;
            }

            EditorUtility.DisplayDialog(
                "Excel2SO CodeGen / 代码生成",
                $"Existing user file is not partial, so generation could create a duplicate type. / 已有用户文件不是 partial，继续生成可能造成重复类型:\n\n{file.AssetPath}",
                "OK / 确定");
            return false;
        }

        return true;
    }

    private static string BuildDataGeneratedScript(Excel2SoCodeGenOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(GeneratedHeader);
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine();
        builder.AppendLine("[Serializable]");
        builder.AppendLine($"public partial struct {options.DataClassName}");
        builder.AppendLine("{");

        foreach (var field in options.Fields)
        {
            var definition = Excel2SoCodeGenTypeRegistry.Get(field.Kind);
            builder.AppendLine($"    public {definition.GetCSharpType(field)} {field.PropertyName};");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildDatabaseGeneratedScript(Excel2SoCodeGenOptions options)
    {
        var listProperty = ToPascalCase(options.ListPropertyName);
        var replaceMethodName = $"Replace{listProperty}";

        var builder = new StringBuilder();
        builder.Append(GeneratedHeader);
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine();
        builder.AppendLine($"public partial class {options.DatabaseClassName} : ScriptableObject");
        builder.AppendLine("{");
        builder.AppendLine($"    [SerializeField] private List<{options.DataClassName}> {options.ListPropertyName} = new List<{options.DataClassName}>();");
        builder.AppendLine();
        builder.AppendLine($"    public IReadOnlyList<{options.DataClassName}> {listProperty} => {options.ListPropertyName};");
        builder.AppendLine();
        builder.AppendLine($"    public void {replaceMethodName}(IEnumerable<{options.DataClassName}> newItems)");
        builder.AppendLine("    {");
        builder.AppendLine($"        {options.ListPropertyName}.Clear();");
        builder.AppendLine();
        builder.AppendLine("        if (newItems != null)");
        builder.AppendLine("        {");
        builder.AppendLine($"            {options.ListPropertyName}.AddRange(newItems);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildImporterGeneratedScript(Excel2SoCodeGenOptions options)
    {
        var listProperty = ToPascalCase(options.ListPropertyName);
        var builder = new StringBuilder();
        builder.Append(GeneratedHeader);
        builder.AppendLine("using System.Linq;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine();
        builder.AppendLine($"public sealed class {options.ImporterClassName} : Excel2SoListAssetImporter<{options.DatabaseClassName}>");
        builder.AppendLine("{");
        builder.AppendLine($"    protected override string DefaultAssetPath => \"{EscapeCSharpString(options.DefaultAssetPath)}\";");
        builder.AppendLine();
        builder.AppendLine($"    protected override string ListPropertyPath => \"{EscapeCSharpString(options.ListPropertyName)}\";");
        builder.AppendLine();
        builder.AppendLine("    protected override void Configure(Excel2SoMapping map)");
        builder.AppendLine("    {");

        foreach (var field in options.Fields)
        {
            var definition = Excel2SoCodeGenTypeRegistry.Get(field.Kind);
            builder.AppendLine(
                $"        map.Column(\"{EscapeCSharpString(field.ColumnName)}\").To(\"{EscapeCSharpString(field.PropertyName)}\"){definition.GetMappingCall(field)};");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    protected override void OnAfterImportAsset({options.DatabaseClassName} asset, ExcelTable table, Excel2SoImportReport report)");
        builder.AppendLine("    {");
        builder.AppendLine($"        asset.Replace{listProperty}(asset.{listProperty}.ToArray());");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildDataUserScript(Excel2SoCodeGenOptions options)
    {
        return
            $"public partial struct {options.DataClassName}\n" +
            "{\n" +
            "}\n";
    }

    private static string BuildDatabaseUserScript(Excel2SoCodeGenOptions options)
    {
        var menuName = $"PG/Generated/{SplitPascalCase(options.DatabaseClassName)}";
        return
            "using UnityEngine;\n" +
            "\n" +
            $"[CreateAssetMenu(fileName = \"{EscapeCSharpString(options.DatabaseClassName)}\", menuName = \"{EscapeCSharpString(menuName)}\", order = 0)]\n" +
            $"public partial class {options.DatabaseClassName}\n" +
            "{\n" +
            "}\n";
    }

    private static string BuildCsvContent(Excel2SoCodeGenOptions options)
    {
        var headers = options.Fields
            .Select(field => field.ColumnName)
            .ToArray();

        var builder = new StringBuilder();
        using (var writer = new StringWriter(builder))
        {
            WriteCsvRow(writer, headers);
        }

        return builder.ToString();
    }

    private static void WriteCsvRow(TextWriter writer, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write(EscapeCsv(cells[i]));
        }

        writer.WriteLine();
    }

    private static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void WriteTextFile(string assetPath, string content, Excel2SoCodeGenReport report, bool overwrite)
    {
        var resolvedPath = ResolveFileSystemPath(assetPath);
        var existed = File.Exists(resolvedPath);
        if (existed && !overwrite)
        {
            report.SkippedUserFiles++;
            return;
        }

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoding = assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? new UTF8Encoding(true)
            : new UTF8Encoding(false);

        File.WriteAllText(resolvedPath, content, encoding);
        if (existed) report.UpdatedFiles++;
        else report.CreatedFiles++;
        report.WrittenPaths.Add(assetPath);
    }

    private static bool IsAssetFolder(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && (path.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
               && !Path.HasExtension(path);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim();
        if (CSharpKeywords.Contains(value)) return false;
        if (!IsIdentifierStart(value[0])) return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTypeExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim();
        const string globalPrefix = "global::";
        if (value.StartsWith(globalPrefix, StringComparison.Ordinal))
        {
            value = value.Substring(globalPrefix.Length);
        }

        return value
            .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
            .All(IsValidIdentifier);
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static string NormalizeColumnName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeIdentifierInput(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string CombineAssetPath(string folder, string fileName)
    {
        return $"{NormalizeAssetPath(folder).TrimEnd('/')}/{fileName}";
    }

    private static string ResolveFileSystemPath(string path)
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

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
    }

    private static string EscapeCSharpString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Entries";

        value = value.Trim();
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private sealed class GeneratedFile
    {
        public GeneratedFile(string assetPath, string content)
        {
            AssetPath = assetPath;
            Content = content;
        }

        public string AssetPath { get; }

        public string Content { get; }
    }
}
