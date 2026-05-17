using System.Linq;
using UnityEngine;
using Game.Gameplay;
using UnityEasyWorkTools.Editor;

public sealed class WeaponDatabaseExcelImporter : Excel2SoListAssetImporter<WeaponDatabase>
{
    protected override string DefaultAssetPath => UnityEasyWorkToolsPathSettings.GetOrCreate().WeaponDatabaseAssetPath;

    protected override string ListPropertyPath => "weapons";

    protected override void Configure(Excel2SoMapping map)
    {
        map.Column("weaponId").To("weaponId").AsString();
        map.Column("displayName").To("displayName").AsString();
        map.Column("minDamage").To("minDamage").AsInt();
        map.Column("maxDamage").To("maxDamage").AsInt();
        map.Column("maxBulletBagNum").To("maxBulletBagNum").AsInt();
        map.Column("clipSize").To("clipSize").AsInt();
        map.Column("shootInterval").To("shootInterval").AsFloat();
        map.Column("bulletSpeed").To("bulletSpeed").AsInt();
        map.Column("reloadSound").To("reloadSound").AsAsset<AudioClip>();
        map.Column("shootSounds").To("shootSounds").AsAssetList<AudioClip>(";");
    }

    protected override void OnAfterImportAsset(WeaponDatabase asset, ExcelTable table, Excel2SoImportReport report)
    {
        asset.ReplaceWeapons(asset.Weapons.ToArray());
    }
}
