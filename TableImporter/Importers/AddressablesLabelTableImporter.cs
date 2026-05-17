using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public sealed class AddressablesLabelTableImporter : ExcelTableImporterBase
{
    private const string MenuRoot = "Tools/UnityEasyWorkTools/Addressables/";

    private static readonly string[] AssetPathColumns =
    {
        "assetPath", "path", "asset", "resourcePath", "资源路径", "资产路径"
    };

    private static readonly string[] AddressColumns =
    {
        "address", "addr", "key", "地址"
    };

    private static readonly string[] GroupColumns =
    {
        "group", "groupName", "包名", "分组"
    };

    private static readonly string[] LabelColumns =
    {
        "labels", "label", "lables", "addressablesLabels", "标签"
    };

    private static readonly string[] ClearLabelsColumns =
    {
        "clearLabels", "replaceLabels", "清空标签", "替换标签"
    };

    protected override string FilePanelTitle => "Import Addressables Labels";

    [MenuItem(MenuRoot + "Import Labels From Excel")]
    public static void ImportFromMenu()
    {
        new AddressablesLabelTableImporter().ImportFromFilePanel();
    }

    [MenuItem(MenuRoot + "Create Labels CSV Template")]
    public static void CreateCsvTemplate()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Addressables Labels Template",
            "AddressablesLabelsTemplate",
            "csv",
            "Choose where to save the Addressables labels CSV template."
        );

        if (string.IsNullOrEmpty(path)) return;

        var csv = string.Join(
            Environment.NewLine,
            "assetPath,address,group,labels,clearLabels",
            "Assets/Prefab/GunList/AK.prefab,weapon/ak,Local_Weapons_Base,weapons_base;weapon_ak,false"
        );

        File.WriteAllText(path, csv, new UTF8Encoding(true));
        AssetDatabase.Refresh();
        Debug.Log($"{nameof(AddressablesLabelTableImporter)}: Created template at {path}");
    }

    public Excel2SoImportReport ImportLabels(string tablePath)
    {
        return base.Import(tablePath);
    }

    protected override Excel2SoImportReport ImportTable(ExcelTable table, string tablePath)
    {
        var report = new Excel2SoImportReport
        {
            TablePath = tablePath,
            TargetPath = "Addressables Settings"
        };

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            report.ConversionErrors++;
            Debug.LogError($"{nameof(AddressablesLabelTableImporter)}: Addressables settings not found.");
            return report;
        }

        if (table.Rows.Count == 0)
        {
            Debug.LogWarning($"{nameof(AddressablesLabelTableImporter)}: Table has no data rows: {tablePath}");
            return report;
        }

        if (!HasAnyColumn(table, AssetPathColumns) || !HasAnyColumn(table, LabelColumns))
        {
            report.ConversionErrors++;
            Debug.LogError($"{nameof(AddressablesLabelTableImporter)}: Required columns are missing. Need assetPath and labels.");
            return report;
        }

        var addressablesReport = new AddressablesLabelImportReport();
        foreach (var row in table.Rows)
        {
            if (row.IsEmpty)
            {
                addressablesReport.Skipped++;
                continue;
            }

            ImportRow(settings, row, addressablesReport);
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.ImportedRows = addressablesReport.Imported;
        report.SkippedRows = addressablesReport.Skipped;
        report.CreatedAssets = addressablesReport.CreatedOrMovedEntries;
        report.AssignedFields = addressablesReport.AssignedLabels;

        Debug.Log(
            $"{nameof(AddressablesLabelTableImporter)}: Imported {addressablesReport.Imported} rows, " +
            $"skipped {addressablesReport.Skipped}, created/moved {addressablesReport.CreatedOrMovedEntries} entries, " +
            $"assigned {addressablesReport.AssignedLabels} labels from {tablePath}"
        );

        return report;
    }

    private static void ImportRow(
        AddressableAssetSettings settings,
        ExcelRow row,
        AddressablesLabelImportReport report)
    {
        var assetPath = NormalizeAssetPath(GetCell(row, AssetPathColumns));
        if (string.IsNullOrEmpty(assetPath))
        {
            report.Skipped++;
            return;
        }

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            report.Skipped++;
            Debug.LogWarning($"{nameof(AddressablesLabelTableImporter)}: Row {row.RowNumber} asset not found: {assetPath}");
            return;
        }

        var group = ResolveGroup(settings, GetCell(row, GroupColumns), guid);
        if (group == null)
        {
            report.Skipped++;
            Debug.LogWarning($"{nameof(AddressablesLabelTableImporter)}: Row {row.RowNumber} has no usable Addressables group: {assetPath}");
            return;
        }

        var entry = settings.FindAssetEntry(guid);
        var shouldCreateOrMove = entry == null || entry.parentGroup != group;
        entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (shouldCreateOrMove) report.CreatedOrMovedEntries++;

        var address = GetCell(row, AddressColumns);
        if (!string.IsNullOrWhiteSpace(address))
        {
            entry.address = address.Trim();
        }

        if (ParseBool(GetCell(row, ClearLabelsColumns)))
        {
            foreach (var label in entry.labels.ToArray())
            {
                entry.SetLabel(label, false, false, false);
            }
        }

        foreach (var label in SplitLabels(GetCell(row, LabelColumns)))
        {
            settings.AddLabel(label, false);
            if (entry.SetLabel(label, true, false, false))
            {
                report.AssignedLabels++;
            }
        }

        report.Imported++;
    }

    private static AddressableAssetGroup ResolveGroup(AddressableAssetSettings settings, string groupName, string guid)
    {
        groupName = groupName?.Trim();
        if (!string.IsNullOrEmpty(groupName))
        {
            return settings.FindGroup(groupName);
        }

        var existingEntry = settings.FindAssetEntry(guid);
        if (existingEntry?.parentGroup != null)
        {
            return existingEntry.parentGroup;
        }

        return settings.DefaultGroup;
    }

    private static bool HasAnyColumn(ExcelTable table, IEnumerable<string> columnNames)
    {
        return columnNames.Any(columnName => table.TryGetColumnIndex(columnName, out _));
    }

    private static string GetCell(ExcelRow row, IEnumerable<string> columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (row.TryGet(columnName, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> SplitLabels(string labels)
    {
        if (string.IsNullOrWhiteSpace(labels)) yield break;

        var separators = new[] { ';', ',', '|', '\n', '\r', '，', '；' };
        foreach (var label in labels.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = label.Trim();
            if (!string.IsNullOrEmpty(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static bool ParseBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "y" || value == "on";
    }

    private sealed class AddressablesLabelImportReport
    {
        public int Imported;
        public int Skipped;
        public int CreatedOrMovedEntries;
        public int AssignedLabels;
    }
}
