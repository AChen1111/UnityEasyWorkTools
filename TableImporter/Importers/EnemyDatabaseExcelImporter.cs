using System.Linq;
using Game.Gameplay;
using UnityEasyWorkTools.Editor;

/// <summary>
/// 敌人数据库 Excel 导入器, 列名需要与 Configure 中声明保持一致.
/// </summary>
public sealed class EnemyDatabaseExcelImporter : Excel2SoListAssetImporter<EnemyDatabase>
{
    protected override string DefaultAssetPath => UnityEasyWorkToolsPathSettings.GetOrCreate().EnemyDatabaseAssetPath;

    protected override string ListPropertyPath => "enemies";

    protected override void Configure(Excel2SoMapping map)
    {
        map.Column("enemyId").To("enemyId").AsInt();
        map.Column("displayName").To("displayName").AsString();
        map.Column("prefab").To("prefab").AsAsset<EnemyBase>();
        map.Column("maxHp").To("maxHp").AsInt();
        map.Column("moveSpeed").To("moveSpeed").AsFloat();
        map.Column("damage").To("damage").AsInt();
        map.Column("itemDropChance").To("itemDropChance").AsFloat();
    }

    protected override void OnAfterImportAsset(EnemyDatabase asset, ExcelTable table, Excel2SoImportReport report)
    {
        var importedEnemies = asset.Enemies.ToArray();
        asset.ReplaceEnemies(importedEnemies);
    }
}
