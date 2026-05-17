using System.Linq;
using UnityEngine;
using Game.Items;
using UnityEasyWorkTools.Editor;

public sealed class ItemDatabaseExcelImporter : Excel2SoListAssetImporter<ItemDatabase>
{
    protected override string DefaultAssetPath => UnityEasyWorkToolsPathSettings.GetOrCreate().ItemDatabaseAssetPath;

    protected override string ListPropertyPath => "items";

    protected override void Configure(Excel2SoMapping map)
    {
        map.Column("itemId").To("itemId").AsInt();
        map.Column("itemName").To("itemName").AsString();
        map.Column("description").To("description").AsString();
        map.Column("icon").To("icon").AsAsset<Sprite>();
    }

    protected override void OnAfterImportAsset(ItemDatabase asset, ExcelTable table, Excel2SoImportReport report)
    {
        var importedItems = asset.Items.ToArray();
        asset.ReplaceItems(importedItems);
    }
}
