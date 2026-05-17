using System;
using UnityEditor;
using UnityEngine;

namespace UnityEasyWorkTools.Editor
{
    /// <summary>
    /// UnityEasyWorkTools 路径配置, 统一管理插件内可调整的默认目录和资产路径.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityEasyWorkTools/Path Settings", fileName = "UnityEasyWorkToolsPathSettings")]
    public sealed class UnityEasyWorkToolsPathSettings : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/UnityEasyWorkTools/UnityEasyWorkToolsPathSettings.asset";

        [Header("Plugin Root / 插件根目录")]
        [SerializeField] private string toolsRootFolder = "Assets/UnityEasyWorkTools";

        [Header("Animation Sequence / 动画序列")]
        [SerializeField] private string animationSequenceAssetFolder = "Assets/GameDataSO/DOTweenAnimationSequence";

        [Header("UI Auto Bind / UI 自动绑定")]
        [SerializeField] private string uiAutoBindGlobalSettingPath = "Assets/UnityEasyWorkTools/UIAutoBind/AutoBindGlobalSetting.asset";

        [Header("Table Importer / 导表工具")]
        [SerializeField] private string tableImporterRootFolder = "Assets/UnityEasyWorkTools/TableImporter";
        [SerializeField] private string tableImporterUiFolder = "Assets/UnityEasyWorkTools/TableImporter/Editor/UI";
        [SerializeField] private string generatedImporterFolder = "Assets/UnityEasyWorkTools/TableImporter/Importers/Generated";
        [SerializeField] private string generatedRuntimeScriptFolder = "Assets/Scripts/Generated/Excel2SO";
        [SerializeField] private string generatedDefaultAssetFolder = "Assets/GameDataSO/Generated";
        [SerializeField] private string generatedCsvFolder = "Assets/csv";

        [Header("Project Table Assets / 项目表资产")]
        [SerializeField] private string itemDatabaseAssetPath = "Assets/Resources/ItemDatabase.asset";
        [SerializeField] private string weaponDatabaseAssetPath = "Assets/Resources/WeaponDatabase.asset";
        [SerializeField] private string buffDatabaseAssetPath = "Assets/GameDataSO/DataBase/BuffDataBase.asset";
        [SerializeField] private string enemyDatabaseAssetPath = "Assets/GameDataSO/DataBase/EnemyDatabase.asset";
        [SerializeField] private string itemSpawnTableAssetPath = "Assets/GameDataSO/ItemSpawnerTable/ImportedItemSpawnTable.asset";

        public string ToolsRootFolder => NormalizeRequiredFolder(toolsRootFolder, nameof(toolsRootFolder));
        public string AnimationSequenceAssetFolder => NormalizeRequiredFolder(animationSequenceAssetFolder, nameof(animationSequenceAssetFolder));
        public string UiAutoBindGlobalSettingPath => NormalizeRequiredAssetPath(uiAutoBindGlobalSettingPath, nameof(uiAutoBindGlobalSettingPath));
        public string TableImporterRootFolder => NormalizeRequiredFolder(tableImporterRootFolder, nameof(tableImporterRootFolder));
        public string TableImporterUiFolder => NormalizeRequiredFolder(tableImporterUiFolder, nameof(tableImporterUiFolder));
        public string GeneratedImporterFolder => NormalizeRequiredFolder(generatedImporterFolder, nameof(generatedImporterFolder));
        public string GeneratedRuntimeScriptFolder => NormalizeRequiredFolder(generatedRuntimeScriptFolder, nameof(generatedRuntimeScriptFolder));
        public string GeneratedDefaultAssetFolder => NormalizeRequiredFolder(generatedDefaultAssetFolder, nameof(generatedDefaultAssetFolder));
        public string GeneratedCsvFolder => NormalizeRequiredFolder(generatedCsvFolder, nameof(generatedCsvFolder));
        public string ItemDatabaseAssetPath => NormalizeRequiredAssetPath(itemDatabaseAssetPath, nameof(itemDatabaseAssetPath));
        public string WeaponDatabaseAssetPath => NormalizeRequiredAssetPath(weaponDatabaseAssetPath, nameof(weaponDatabaseAssetPath));
        public string BuffDatabaseAssetPath => NormalizeRequiredAssetPath(buffDatabaseAssetPath, nameof(buffDatabaseAssetPath));
        public string EnemyDatabaseAssetPath => NormalizeRequiredAssetPath(enemyDatabaseAssetPath, nameof(enemyDatabaseAssetPath));
        public string ItemSpawnTableAssetPath => NormalizeRequiredAssetPath(itemSpawnTableAssetPath, nameof(itemSpawnTableAssetPath));

        /// <summary>
        /// 读取路径配置, 不存在时创建默认配置资产, 方便插件首次导入后直接编辑.
        /// </summary>
        public static UnityEasyWorkToolsPathSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEasyWorkToolsPathSettings>(DefaultAssetPath);
            if (settings != null)
            {
                return settings;
            }

            EnsureAssetFolder("Assets/UnityEasyWorkTools");
            settings = CreateInstance<UnityEasyWorkToolsPathSettings>();
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }

        [MenuItem("Tools/UnityEasyWorkTools/Settings/Open Path Settings")]
        private static void OpenPathSettings()
        {
            var settings = GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public string BuildGeneratedAssetPath(string namePrefix)
        {
            namePrefix = NormalizeNamePrefix(namePrefix);
            return CombineAssetPath(GeneratedDefaultAssetFolder, $"{namePrefix}Database.asset");
        }

        public string BuildGeneratedCsvPath(string namePrefix)
        {
            namePrefix = NormalizeNamePrefix(namePrefix);
            return CombineAssetPath(GeneratedCsvFolder, $"{namePrefix}Database.csv");
        }

        public static void EnsureAssetFolder(string folderPath)
        {
            folderPath = NormalizeRequiredFolder(folderPath, nameof(folderPath));
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
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

        private static string NormalizeNamePrefix(string namePrefix)
        {
            namePrefix = string.IsNullOrWhiteSpace(namePrefix) ? "NewTable" : namePrefix.Trim();
            return namePrefix;
        }

        private static string CombineAssetPath(string folder, string fileName)
        {
            return $"{NormalizeRequiredFolder(folder, nameof(folder))}/{fileName}";
        }

        private static string NormalizeRequiredFolder(string path, string fieldName)
        {
            path = NormalizeRequiredPath(path, fieldName).TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"UnityEasyWorkToolsPathSettings.{fieldName} 必须是 Assets 下的目录: {path}.");
            }

            return path;
        }

        private static string NormalizeRequiredAssetPath(string path, string fieldName)
        {
            path = NormalizeRequiredPath(path, fieldName);
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"UnityEasyWorkToolsPathSettings.{fieldName} 必须是 Assets 下的 .asset 路径: {path}.");
            }

            return path;
        }

        private static string NormalizeRequiredPath(string path, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException($"UnityEasyWorkToolsPathSettings.{fieldName} 不能为空.");
            }

            return path.Replace('\\', '/').Trim();
        }
    }
}
