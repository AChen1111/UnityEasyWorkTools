using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Game.Gameplay;
using UnityEasyWorkTools.Editor;

/// <summary>
/// Buff 数据库 Excel 导入器, 每行写入一个 Buff 配置.
/// </summary>
public sealed class BuffDatabaseExcelImporter : Excel2SoListAssetImporter<BuffDataBase>
{
    protected override string DefaultAssetPath => UnityEasyWorkToolsPathSettings.GetOrCreate().BuffDatabaseAssetPath;

    protected override string ListPropertyPath => "buffs";

    protected override void Configure(Excel2SoMapping map)
    {
        map.Column("id").To("id").AsInt();
        map.Column("buffName").To("buffName").AsString();
        map.Column("luaFile").To("luaFile").AsAsset<TextAsset>();
        map.Column("duration").To("duration").AsFloat();
        map.Column("isPermanent").To("isPermanent").AsBool();
        map.Column("interval").To("interval").AsFloat();
    }

    protected override void OnAfterImportAsset(BuffDataBase asset, ExcelTable table, Excel2SoImportReport report)
    {
        var importedBuffs = asset.Buffs.ToArray();
        var rows = table.Rows.Where(row => !row.IsEmpty).ToArray();
        var rowCount = Mathf.Min(importedBuffs.Length, rows.Length);

        for (var i = 0; i < rowCount; i++)
        {
            if (!rows[i].HasColumn("modifiers")) continue;

            importedBuffs[i].ReplaceModifiers(ParseModifiers(rows[i], report));
        }

        asset.ReplaceBuffs(importedBuffs);
    }

    /// <summary>
    /// 解析 Buff 属性修正列, 格式为 StatType:ModifierType:Value;StatType:ModifierType:Value.
    /// </summary>
    /// <param name="row">当前导入行.</param>
    /// <param name="report">导入报告.</param>
    /// <returns>解析后的属性修正列表.</returns>
    private static IEnumerable<StatModifier> ParseModifiers(ExcelRow row, Excel2SoImportReport report)
    {
        if (!row.TryGet("modifiers", out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<StatModifier>();
        }

        var modifiers = new List<StatModifier>();
        var entries = rawValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                report.ConversionErrors++;
                Debug.LogError($"BuffDatabaseExcelImporter: Row {row.RowNumber} modifiers 格式错误: {entry}.");
                continue;
            }

            if (!Enum.TryParse(parts[0].Trim(), true, out StatType statType))
            {
                report.ConversionErrors++;
                Debug.LogError($"BuffDatabaseExcelImporter: Row {row.RowNumber} StatType 无效: {parts[0]}.");
                continue;
            }

            if (!Enum.TryParse(parts[1].Trim(), true, out ModifierType modifierType))
            {
                report.ConversionErrors++;
                Debug.LogError($"BuffDatabaseExcelImporter: Row {row.RowNumber} ModifierType 无效: {parts[1]}.");
                continue;
            }

            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                report.ConversionErrors++;
                Debug.LogError($"BuffDatabaseExcelImporter: Row {row.RowNumber} Modifier 数值无效: {parts[2]}.");
                continue;
            }

            modifiers.Add(new StatModifier(statType, modifierType, value));
        }

        return modifiers;
    }
}
