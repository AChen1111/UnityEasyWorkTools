using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Excel2SoMapping
{
    private readonly List<Excel2SoBinding> bindings = new List<Excel2SoBinding>();

    internal IReadOnlyList<Excel2SoBinding> Bindings => bindings;

    internal IReadOnlyList<Excel2SoBinding> ExportableBindings => bindings.Where(binding => binding.CanExport).ToArray();

    public Excel2SoColumnBuilder Column(string columnName)
    {
        return new Excel2SoColumnBuilder(this, columnName);
    }

    internal void Add(Excel2SoBinding binding)
    {
        bindings.Add(binding);
    }

    internal void Apply(
        ExcelRow row,
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoImportContext context)
    {
        foreach (var binding in bindings)
        {
            binding.Apply(row, serializedObject, rootProperty, target, context);
        }
    }
}

public sealed class Excel2SoColumnBuilder
{
    private readonly Excel2SoMapping mapping;
    private readonly string columnName;

    internal Excel2SoColumnBuilder(Excel2SoMapping mapping, string columnName)
    {
        this.mapping = mapping;
        this.columnName = columnName;
    }

    public Excel2SoValueBuilder To(string propertyPath)
    {
        return new Excel2SoValueBuilder(mapping, columnName, propertyPath);
    }

    public Excel2SoMapping Custom(Action<ExcelRow, ScriptableObject> apply)
    {
        mapping.Add(new Excel2SoCustomBinding(columnName, apply));
        return mapping;
    }
}

public sealed class Excel2SoValueBuilder
{
    private readonly Excel2SoMapping mapping;
    private readonly string columnName;
    private readonly string propertyPath;

    internal Excel2SoValueBuilder(Excel2SoMapping mapping, string columnName, string propertyPath)
    {
        this.mapping = mapping;
        this.columnName = columnName;
        this.propertyPath = propertyPath;
    }

    public Excel2SoMapping AsString() => Add(Excel2SoValueSetters.SetString, Excel2SoValueGetters.GetString);

    public Excel2SoMapping AsInt() => Add(Excel2SoValueSetters.SetInt, Excel2SoValueGetters.GetInt);

    public Excel2SoMapping AsFloat() => Add(Excel2SoValueSetters.SetFloat, Excel2SoValueGetters.GetFloat);

    public Excel2SoMapping AsBool() => Add(Excel2SoValueSetters.SetBool, Excel2SoValueGetters.GetBool);

    public Excel2SoMapping AsVector2() => Add(Excel2SoValueSetters.SetVector2, Excel2SoValueGetters.GetVector2);

    public Excel2SoMapping AsVector3() => Add(Excel2SoValueSetters.SetVector3, Excel2SoValueGetters.GetVector3);

    public Excel2SoMapping AsColor() => Add(Excel2SoValueSetters.SetColor, Excel2SoValueGetters.GetColor);

    public Excel2SoMapping AsStringList(string separators = ";,|")
    {
        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetStringList(rawValue, property, context, column, path, separators),
            (SerializedProperty property, Excel2SoExportContext context, string column, string path, out string value) =>
                Excel2SoValueGetters.GetStringList(property, context, column, path, separators, out value));
    }

    public Excel2SoMapping AsIntList(string separators = ";,|")
    {
        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetIntList(rawValue, property, context, column, path, separators),
            (SerializedProperty property, Excel2SoExportContext context, string column, string path, out string value) =>
                Excel2SoValueGetters.GetIntList(property, context, column, path, separators, out value));
    }

    public Excel2SoMapping AsFloatList(string separators = ";,|")
    {
        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetFloatList(rawValue, property, context, column, path, separators),
            (SerializedProperty property, Excel2SoExportContext context, string column, string path, out string value) =>
                Excel2SoValueGetters.GetFloatList(property, context, column, path, separators, out value));
    }

    public Excel2SoMapping AsAsset<TAsset>() where TAsset : Object
    {
        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetAsset<TAsset>(rawValue, property, context, column, path),
            Excel2SoValueGetters.GetAsset);
    }

    public Excel2SoMapping AsAssetList<TAsset>(string separators = ";,|") where TAsset : Object
    {
        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetAssetList<TAsset>(rawValue, property, context, column, path, separators),
            (SerializedProperty property, Excel2SoExportContext context, string column, string path, out string value) =>
                Excel2SoValueGetters.GetAssetList(property, context, column, path, separators, out value));
    }

    public Excel2SoMapping AsEnum<TEnum>() where TEnum : struct
    {
        return AsEnum(typeof(TEnum));
    }

    public Excel2SoMapping AsEnum(Type enumType)
    {
        if (enumType == null || !enumType.IsEnum)
        {
            throw new ArgumentException("Excel2SO enum mapping requires an enum type.", nameof(enumType));
        }

        return Add(
            (row, rawValue, property, context, column, path) =>
                Excel2SoValueSetters.SetEnum(rawValue, property, context, column, path, enumType),
            (SerializedProperty property, Excel2SoExportContext context, string column, string path, out string value) =>
                Excel2SoValueGetters.GetEnum(property, context, column, path, enumType, out value));
    }

    private Excel2SoMapping Add(Excel2SoValueSetter setter, Excel2SoValueGetter getter)
    {
        mapping.Add(new Excel2SoFieldBinding(columnName, propertyPath, setter, getter));
        return mapping;
    }
}

internal delegate bool Excel2SoValueSetter(
    ExcelRow row,
    string rawValue,
    SerializedProperty property,
    Excel2SoImportContext context,
    string columnName,
    string propertyPath);

internal delegate bool Excel2SoValueGetter(
    SerializedProperty property,
    Excel2SoExportContext context,
    string columnName,
    string propertyPath,
    out string value);

internal abstract class Excel2SoBinding
{
    protected Excel2SoBinding(string columnName)
    {
        ColumnName = columnName;
    }

    internal string ColumnName { get; }

    internal virtual bool CanExport => false;

    public abstract bool Apply(
        ExcelRow row,
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoImportContext context);

    public virtual bool TryExport(
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoExportContext context,
        out string value)
    {
        value = string.Empty;
        return false;
    }

    protected bool TryGetCell(ExcelRow row, Excel2SoImportContext context, out string value)
    {
        if (row.TryGet(ColumnName, out value))
        {
            return true;
        }

        context.WarnOnce(
            $"missing-column:{ColumnName}",
            $"Excel2SO: Column '{ColumnName}' is not present; mapped fields for this column are skipped.");
        return false;
    }
}

internal sealed class Excel2SoFieldBinding : Excel2SoBinding
{
    private readonly string propertyPath;
    private readonly Excel2SoValueSetter setter;
    private readonly Excel2SoValueGetter getter;

    public Excel2SoFieldBinding(
        string columnName,
        string propertyPath,
        Excel2SoValueSetter setter,
        Excel2SoValueGetter getter)
        : base(columnName)
    {
        this.propertyPath = propertyPath;
        this.setter = setter;
        this.getter = getter;
    }

    internal override bool CanExport => getter != null;

    public override bool Apply(
        ExcelRow row,
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoImportContext context)
    {
        if (!TryGetCell(row, context, out var rawValue))
        {
            return false;
        }

        var property = Excel2SoSerializedPropertyUtility.FindProperty(serializedObject, rootProperty, propertyPath);
        if (property == null)
        {
            context.WarnOnce(
                $"missing-property:{target.GetType().FullName}:{propertyPath}",
                $"Excel2SO: Property '{propertyPath}' was not found on {target.GetType().Name}.");
            return false;
        }

        try
        {
            if (!setter(row, rawValue, property, context, ColumnName, propertyPath))
            {
                return false;
            }

            context.AssignedFields++;
            return true;
        }
        catch (Exception ex)
        {
            context.ConversionErrors++;
            Debug.LogError(
                $"Excel2SO: Row {row.RowNumber}, column '{ColumnName}' failed to set '{propertyPath}'. {ex.Message}");
            return false;
        }
    }

    public override bool TryExport(
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoExportContext context,
        out string value)
    {
        value = string.Empty;
        if (getter == null)
        {
            return false;
        }

        var property = Excel2SoSerializedPropertyUtility.FindProperty(serializedObject, rootProperty, propertyPath);
        if (property == null)
        {
            context.WarnOnce(
                $"missing-export-property:{target.GetType().FullName}:{propertyPath}",
                $"SO2Table: Property '{propertyPath}' was not found on {target.GetType().Name}.");
            context.ConversionErrors++;
            return false;
        }

        try
        {
            if (!getter(property, context, ColumnName, propertyPath, out value))
            {
                context.ConversionErrors++;
                value = string.Empty;
                return false;
            }

            context.ExportedFields++;
            return true;
        }
        catch (Exception ex)
        {
            context.ConversionErrors++;
            Debug.LogError(
                $"SO2Table: Column '{ColumnName}' failed to export '{propertyPath}'. {ex.Message}");
            value = string.Empty;
            return false;
        }
    }
}

internal sealed class Excel2SoCustomBinding : Excel2SoBinding
{
    private readonly Action<ExcelRow, ScriptableObject> apply;

    public Excel2SoCustomBinding(string columnName, Action<ExcelRow, ScriptableObject> apply)
        : base(columnName)
    {
        this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public override bool Apply(
        ExcelRow row,
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        ScriptableObject target,
        Excel2SoImportContext context)
    {
        if (!TryGetCell(row, context, out _))
        {
            return false;
        }

        try
        {
            apply(row, target);
            EditorUtility.SetDirty(target);
            context.AssignedFields++;
            return true;
        }
        catch (Exception ex)
        {
            context.ConversionErrors++;
            Debug.LogError(
                $"Excel2SO: Row {row.RowNumber}, custom column '{ColumnName}' failed on {target.GetType().Name}. {ex.Message}");
            return false;
        }
    }
}

internal sealed class Excel2SoImportContext
{
    private readonly HashSet<string> warningKeys = new HashSet<string>();

    public int AssignedFields { get; set; }

    public int ConversionErrors { get; set; }

    public void WarnOnce(string key, string message)
    {
        if (warningKeys.Add(key))
        {
            Debug.LogWarning(message);
        }
    }
}

internal sealed class Excel2SoExportContext
{
    private readonly HashSet<string> warningKeys = new HashSet<string>();

    public int ExportedFields { get; set; }

    public int ConversionErrors { get; set; }

    public void WarnOnce(string key, string message)
    {
        if (warningKeys.Add(key))
        {
            Debug.LogWarning(message);
        }
    }
}

internal static class Excel2SoValueGetters
{
    public static bool GetString(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.String, context, columnName, propertyPath))
        {
            return false;
        }

        value = property.stringValue ?? string.Empty;
        return true;
    }

    public static bool GetInt(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Integer, context, columnName, propertyPath))
        {
            return false;
        }

        value = property.intValue.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool GetFloat(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Float, context, columnName, propertyPath))
        {
            return false;
        }

        value = property.floatValue.ToString("G9", CultureInfo.InvariantCulture);
        return true;
    }

    public static bool GetBool(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Boolean, context, columnName, propertyPath))
        {
            return false;
        }

        value = property.boolValue ? "TRUE" : "FALSE";
        return true;
    }

    public static bool GetVector2(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Vector2, context, columnName, propertyPath))
        {
            return false;
        }

        var vector = property.vector2Value;
        value = JoinFloats(vector.x, vector.y);
        return true;
    }

    public static bool GetVector3(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Vector3, context, columnName, propertyPath))
        {
            return false;
        }

        var vector = property.vector3Value;
        value = JoinFloats(vector.x, vector.y, vector.z);
        return true;
    }

    public static bool GetColor(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.Color, context, columnName, propertyPath))
        {
            return false;
        }

        value = $"#{ColorUtility.ToHtmlStringRGBA(property.colorValue)}";
        return true;
    }

    public static bool GetEnum(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        Type enumType,
        out string value)
    {
        value = string.Empty;
        if (property.propertyType == SerializedPropertyType.Enum)
        {
            if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length)
            {
                value = property.enumNames[property.enumValueIndex];
                return true;
            }

            value = property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (property.propertyType == SerializedPropertyType.Integer)
        {
            value = Enum.GetName(enumType, property.intValue) ??
                    property.intValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        context.WarnOnce(
            $"wrong-export-enum-property:{property.serializedObject.targetObject.GetType().FullName}:{propertyPath}",
            $"SO2Table: Property '{propertyPath}' is not compatible with enum column '{columnName}'.");
        return false;
    }

    public static bool GetStringList(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        string separators,
        out string value)
    {
        value = string.Empty;
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = new List<string>(property.arraySize);
        for (var i = 0; i < property.arraySize; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.String, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            values.Add(element.stringValue ?? string.Empty);
        }

        value = string.Join(GetExportSeparator(separators), values);
        return true;
    }

    public static bool GetIntList(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        string separators,
        out string value)
    {
        value = string.Empty;
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = new List<string>(property.arraySize);
        for (var i = 0; i < property.arraySize; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.Integer, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            values.Add(element.intValue.ToString(CultureInfo.InvariantCulture));
        }

        value = string.Join(GetExportSeparator(separators), values);
        return true;
    }

    public static bool GetFloatList(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        string separators,
        out string value)
    {
        value = string.Empty;
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = new List<string>(property.arraySize);
        for (var i = 0; i < property.arraySize; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.Float, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            values.Add(element.floatValue.ToString("G9", CultureInfo.InvariantCulture));
        }

        value = string.Join(GetExportSeparator(separators), values);
        return true;
    }

    public static bool GetAsset(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        out string value)
    {
        value = string.Empty;
        if (!EnsurePropertyType(property, SerializedPropertyType.ObjectReference, context, columnName, propertyPath))
        {
            return false;
        }

        value = property.objectReferenceValue == null
            ? string.Empty
            : AssetDatabase.GetAssetPath(property.objectReferenceValue);
        return true;
    }

    public static bool GetAssetList(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath,
        string separators,
        out string value)
    {
        value = string.Empty;
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = new List<string>(property.arraySize);
        for (var i = 0; i < property.arraySize; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.ObjectReference, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            values.Add(element.objectReferenceValue == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(element.objectReferenceValue));
        }

        value = string.Join(GetExportSeparator(separators), values);
        return true;
    }

    private static bool EnsurePropertyType(
        SerializedProperty property,
        SerializedPropertyType expected,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath)
    {
        if (property.propertyType == expected)
        {
            return true;
        }

        context.WarnOnce(
            $"wrong-export-property-type:{property.serializedObject.targetObject.GetType().FullName}:{propertyPath}:{expected}",
            $"SO2Table: Property '{propertyPath}' is {property.propertyType}, but column '{columnName}' expects {expected}.");
        return false;
    }

    private static bool EnsureArrayProperty(
        SerializedProperty property,
        Excel2SoExportContext context,
        string columnName,
        string propertyPath)
    {
        if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
        {
            return true;
        }

        context.WarnOnce(
            $"wrong-export-array-property:{property.serializedObject.targetObject.GetType().FullName}:{propertyPath}",
            $"SO2Table: Property '{propertyPath}' must be an array or List for column '{columnName}'.");
        return false;
    }

    private static string JoinFloats(params float[] values)
    {
        return string.Join(";", values.Select(value => value.ToString("G9", CultureInfo.InvariantCulture)));
    }

    private static string GetExportSeparator(string separators)
    {
        return string.IsNullOrEmpty(separators) ? ";" : separators[0].ToString();
    }
}

internal static class Excel2SoValueSetters
{
    public static bool SetString(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.String, context, columnName, propertyPath))
        {
            return false;
        }

        property.stringValue = rawValue ?? string.Empty;
        return true;
    }

    public static bool SetInt(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Integer, context, columnName, propertyPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            property.intValue = 0;
            return true;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) ||
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue))
        {
            property.intValue = intValue;
            return true;
        }

        context.WarnOnce(
            $"invalid-int:{columnName}:{rawValue}",
            $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not a valid int.");
        return false;
    }

    public static bool SetFloat(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Float, context, columnName, propertyPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            property.floatValue = 0f;
            return true;
        }

        if (TryParseFloat(rawValue, out var floatValue))
        {
            property.floatValue = floatValue;
            return true;
        }

        context.WarnOnce(
            $"invalid-float:{columnName}:{rawValue}",
            $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not a valid float.");
        return false;
    }

    public static bool SetBool(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Boolean, context, columnName, propertyPath))
        {
            return false;
        }

        property.boolValue = ParseBool(rawValue);
        return true;
    }

    public static bool SetVector2(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Vector2, context, columnName, propertyPath))
        {
            return false;
        }

        var values = ParseFloatComponents(rawValue, 2);
        if (values == null)
        {
            context.WarnOnce(
                $"invalid-vector2:{columnName}:{rawValue}",
                $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not a valid Vector2.");
            return false;
        }

        property.vector2Value = new Vector2(values[0], values[1]);
        return true;
    }

    public static bool SetVector3(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Vector3, context, columnName, propertyPath))
        {
            return false;
        }

        var values = ParseFloatComponents(rawValue, 3);
        if (values == null)
        {
            context.WarnOnce(
                $"invalid-vector3:{columnName}:{rawValue}",
                $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not a valid Vector3.");
            return false;
        }

        property.vector3Value = new Vector3(values[0], values[1], values[2]);
        return true;
    }

    public static bool SetColor(
        ExcelRow row,
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.Color, context, columnName, propertyPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            property.colorValue = default;
            return true;
        }

        if (ColorUtility.TryParseHtmlString(rawValue, out var color))
        {
            property.colorValue = color;
            return true;
        }

        var values = ParseFloatComponents(rawValue, 3, 4);
        if (values == null)
        {
            context.WarnOnce(
                $"invalid-color:{columnName}:{rawValue}",
                $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not a valid color.");
            return false;
        }

        property.colorValue = new Color(
            values[0],
            values[1],
            values[2],
            values.Length > 3 ? values[3] : 1f);
        return true;
    }

    public static bool SetEnum(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath,
        Type enumType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = 0;
                return true;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = 0;
                return true;
            }
        }

        if (!TryGetEnumInt(rawValue, enumType, out var enumInt))
        {
            context.WarnOnce(
                $"invalid-enum:{columnName}:{rawValue}",
                $"Excel2SO: Value '{rawValue}' in column '{columnName}' is not valid for enum {enumType.Name}.");
            return false;
        }

        if (property.propertyType == SerializedPropertyType.Integer)
        {
            property.intValue = enumInt;
            return true;
        }

        if (property.propertyType == SerializedPropertyType.Enum)
        {
            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType);
            for (var i = 0; i < values.Length; i++)
            {
                if (Convert.ToInt32(values.GetValue(i), CultureInfo.InvariantCulture) == enumInt)
                {
                    var unityEnumIndex = Array.IndexOf(property.enumNames, names[i]);
                    if (unityEnumIndex < 0 && i < property.enumNames.Length)
                    {
                        unityEnumIndex = i;
                    }

                    if (unityEnumIndex >= 0)
                    {
                        property.enumValueIndex = unityEnumIndex;
                        return true;
                    }
                }
            }
        }

        context.WarnOnce(
            $"wrong-enum-property:{propertyPath}",
            $"Excel2SO: Property '{propertyPath}' is not compatible with enum column '{columnName}'.");
        return false;
    }

    public static bool SetStringList(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath,
        string separators)
    {
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = Split(rawValue, separators).ToArray();
        property.ClearArray();
        for (var i = 0; i < values.Length; i++)
        {
            property.InsertArrayElementAtIndex(i);
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.String, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            element.stringValue = values[i];
        }

        return true;
    }

    public static bool SetIntList(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath,
        string separators)
    {
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = Split(rawValue, separators).ToArray();
        property.ClearArray();
        for (var i = 0; i < values.Length; i++)
        {
            if (!int.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) &&
                !int.TryParse(values[i], NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue))
            {
                context.WarnOnce(
                    $"invalid-int-list:{columnName}:{values[i]}",
                    $"Excel2SO: Value '{values[i]}' in column '{columnName}' is not a valid int list item.");
                return false;
            }

            property.InsertArrayElementAtIndex(i);
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.Integer, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            element.intValue = intValue;
        }

        return true;
    }

    public static bool SetFloatList(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath,
        string separators)
    {
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = Split(rawValue, separators).ToArray();
        property.ClearArray();
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryParseFloat(values[i], out var floatValue))
            {
                context.WarnOnce(
                    $"invalid-float-list:{columnName}:{values[i]}",
                    $"Excel2SO: Value '{values[i]}' in column '{columnName}' is not a valid float list item.");
                return false;
            }

            property.InsertArrayElementAtIndex(i);
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.Float, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            element.floatValue = floatValue;
        }

        return true;
    }

    public static bool SetAsset<TAsset>(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath) where TAsset : Object
    {
        if (!EnsurePropertyType(property, SerializedPropertyType.ObjectReference, context, columnName, propertyPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            property.objectReferenceValue = null;
            return true;
        }

        var asset = LoadAsset<TAsset>(rawValue);
        if (asset == null)
        {
            context.WarnOnce(
                $"missing-asset:{typeof(TAsset).FullName}:{rawValue}",
                $"Excel2SO: Asset '{rawValue}' for column '{columnName}' was not found as {typeof(TAsset).Name}.");
            return false;
        }

        property.objectReferenceValue = asset;
        return true;
    }

    public static bool SetAssetList<TAsset>(
        string rawValue,
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath,
        string separators) where TAsset : Object
    {
        if (!EnsureArrayProperty(property, context, columnName, propertyPath))
        {
            return false;
        }

        var values = Split(rawValue, separators).ToArray();
        property.ClearArray();
        for (var i = 0; i < values.Length; i++)
        {
            var asset = LoadAsset<TAsset>(values[i]);
            if (asset == null)
            {
                context.WarnOnce(
                    $"missing-asset-list:{typeof(TAsset).FullName}:{values[i]}",
                    $"Excel2SO: Asset '{values[i]}' for column '{columnName}' was not found as {typeof(TAsset).Name}.");
                return false;
            }

            property.InsertArrayElementAtIndex(i);
            var element = property.GetArrayElementAtIndex(i);
            if (!EnsurePropertyType(element, SerializedPropertyType.ObjectReference, context, columnName, $"{propertyPath}[{i}]"))
            {
                return false;
            }

            element.objectReferenceValue = asset;
        }

        return true;
    }

    private static bool EnsurePropertyType(
        SerializedProperty property,
        SerializedPropertyType expected,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (property.propertyType == expected)
        {
            return true;
        }

        context.WarnOnce(
            $"wrong-property-type:{property.serializedObject.targetObject.GetType().FullName}:{propertyPath}:{expected}",
            $"Excel2SO: Property '{propertyPath}' is {property.propertyType}, but column '{columnName}' expects {expected}.");
        return false;
    }

    private static bool EnsureArrayProperty(
        SerializedProperty property,
        Excel2SoImportContext context,
        string columnName,
        string propertyPath)
    {
        if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
        {
            return true;
        }

        context.WarnOnce(
            $"wrong-array-property:{property.serializedObject.targetObject.GetType().FullName}:{propertyPath}",
            $"Excel2SO: Property '{propertyPath}' must be an array or List for column '{columnName}'.");
        return false;
    }

    private static bool TryParseFloat(string rawValue, out float value)
    {
        return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               float.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool ParseBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "y" || value == "on";
    }

    private static float[] ParseFloatComponents(string rawValue, int expectedCount)
    {
        return ParseFloatComponents(rawValue, expectedCount, expectedCount);
    }

    private static float[] ParseFloatComponents(string rawValue, int minCount, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Enumerable.Repeat(0f, minCount).ToArray();
        }

        var parts = Split(rawValue, ";,|").ToArray();
        if (parts.Length < minCount || parts.Length > maxCount)
        {
            return null;
        }

        var values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryParseFloat(parts[i], out values[i]))
            {
                return null;
            }
        }

        return values;
    }

    private static bool TryGetEnumInt(string rawValue, Type enumType, out int enumInt)
    {
        enumInt = 0;
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out enumInt) ||
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out enumInt))
        {
            return true;
        }

        try
        {
            var enumValue = Enum.Parse(enumType, rawValue.Trim(), ignoreCase: true);
            enumInt = Convert.ToInt32(enumValue, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> Split(string rawValue, string separators)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) yield break;

        var separatorChars = string.IsNullOrEmpty(separators)
            ? new[] { ';', ',', '|' }
            : separators.ToCharArray();

        foreach (var value in rawValue.Split(separatorChars, StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = value.Trim();
            if (!string.IsNullOrEmpty(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static TAsset LoadAsset<TAsset>(string assetPath) where TAsset : Object
    {
        assetPath = NormalizeAssetPath(assetPath);
        if (string.IsNullOrEmpty(assetPath)) return null;

        var asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
        if (asset != null) return asset;

        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<TAsset>().FirstOrDefault();
    }

    private static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        path = path.Trim().Replace('\\', '/');
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
}

internal static class Excel2SoSerializedPropertyUtility
{
    public static SerializedProperty FindProperty(
        SerializedObject serializedObject,
        SerializedProperty rootProperty,
        string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            return rootProperty;
        }

        return rootProperty != null
            ? rootProperty.FindPropertyRelative(propertyPath)
            : serializedObject.FindProperty(propertyPath);
    }

    public static void ClearValue(SerializedProperty property)
    {
        if (property == null) return;

        if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
        {
            property.ClearArray();
            return;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                property.intValue = 0;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = false;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = 0f;
                break;
            case SerializedPropertyType.String:
                property.stringValue = string.Empty;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = default;
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = null;
                break;
            case SerializedPropertyType.LayerMask:
                property.intValue = 0;
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = 0;
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = Vector2.zero;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = Vector3.zero;
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = Vector4.zero;
                break;
            case SerializedPropertyType.Rect:
                property.rectValue = default;
                break;
            case SerializedPropertyType.AnimationCurve:
                property.animationCurveValue = new AnimationCurve();
                break;
            case SerializedPropertyType.Bounds:
                property.boundsValue = default;
                break;
            case SerializedPropertyType.Quaternion:
                property.quaternionValue = Quaternion.identity;
                break;
            case SerializedPropertyType.Generic:
                ClearChildren(property);
                break;
        }
    }

    private static void ClearChildren(SerializedProperty property)
    {
        var iterator = property.Copy();
        var end = property.GetEndProperty();
        var enterChildren = true;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (iterator.depth == property.depth + 1)
            {
                ClearValue(iterator);
            }
        }
    }
}
