using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEasyWorkTools.Editor;

/// <summary>
/// 自动绑定全局设置, 负责保存代码生成路径和命名空间.
/// </summary>
public class AutoBindGlobalSetting : ScriptableObject
{
    [SerializeField]
    private string m_CodePath;

    [SerializeField]
    private string m_Namespace;

    [SerializeField]
    private string m_BaseClassName = "MonoBehaviour";

    public string CodePath
    {
        get
        {
            return m_CodePath;
        }
    }

    public string Namespace
    {
        get
        {
            return m_Namespace;
        }
    }

    public string BaseClassName
    {
        get
        {
            return string.IsNullOrEmpty(m_BaseClassName) ? "MonoBehaviour" : m_BaseClassName;
        }
    }

    [MenuItem("Tools/UnityEasyWorkTools/UI Auto Bind/Create Global Setting")]
    private static void CreateAutoBindGlobalSetting()
    {
        string[] paths = AssetDatabase.FindAssets("t:AutoBindGlobalSetting");
        if (paths.Length >= 1)
        {
            string path = AssetDatabase.GUIDToAssetPath(paths[0]);
            EditorUtility.DisplayDialog("警告", $"已存在AutoBindGlobalSetting，路径:{path}", "确认");
            return;
        }

        AutoBindGlobalSetting setting = CreateInstance<AutoBindGlobalSetting>();
        var settingPath = UnityEasyWorkToolsPathSettings.GetOrCreate().UiAutoBindGlobalSettingPath;
        // 创建配置指定的父目录, 让全局设置资产可以跟随插件路径配置移动.
        UnityEasyWorkToolsPathSettings.EnsureAssetFolder(Path.GetDirectoryName(settingPath)?.Replace('\\', '/'));
        AssetDatabase.CreateAsset(setting, settingPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
