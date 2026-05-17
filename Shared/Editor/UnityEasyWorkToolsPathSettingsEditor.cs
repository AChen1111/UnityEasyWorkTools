using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEasyWorkTools.Editor
{
    /// <summary>
    /// 路径配置自定义 Inspector, 使用 UXML/USS 提供文件管理器选择和定位按钮.
    /// </summary>
    [CustomEditor(typeof(UnityEasyWorkToolsPathSettings))]
    public sealed class UnityEasyWorkToolsPathSettingsEditor : UnityEditor.Editor
    {
        private const string LayoutPath = "Assets/UnityEasyWorkTools/Shared/Editor/UI/UnityEasyWorkToolsPathSettingsEditor.uxml";
        private const string StylePath = "Assets/UnityEasyWorkTools/Shared/Editor/UI/UnityEasyWorkToolsPathSettingsEditor.uss";

        private SerializedProperty toolsRootFolder;
        private SerializedProperty animationSequenceAssetFolder;
        private SerializedProperty uiAutoBindGlobalSettingPath;
        private SerializedProperty tableImporterRootFolder;
        private SerializedProperty tableImporterUiFolder;
        private SerializedProperty generatedImporterFolder;
        private SerializedProperty generatedRuntimeScriptFolder;
        private SerializedProperty generatedDefaultAssetFolder;
        private SerializedProperty generatedCsvFolder;

        private void OnEnable()
        {
            toolsRootFolder = serializedObject.FindProperty("toolsRootFolder");
            animationSequenceAssetFolder = serializedObject.FindProperty("animationSequenceAssetFolder");
            uiAutoBindGlobalSettingPath = serializedObject.FindProperty("uiAutoBindGlobalSettingPath");
            tableImporterRootFolder = serializedObject.FindProperty("tableImporterRootFolder");
            tableImporterUiFolder = serializedObject.FindProperty("tableImporterUiFolder");
            generatedImporterFolder = serializedObject.FindProperty("generatedImporterFolder");
            generatedRuntimeScriptFolder = serializedObject.FindProperty("generatedRuntimeScriptFolder");
            generatedDefaultAssetFolder = serializedObject.FindProperty("generatedDefaultAssetFolder");
            generatedCsvFolder = serializedObject.FindProperty("generatedCsvFolder");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = LoadRoot();

            AddFolderRow(root.Q<VisualElement>("plugin-root-container"), toolsRootFolder, "Tools Root Folder");
            AddFolderRow(root.Q<VisualElement>("animation-container"), animationSequenceAssetFolder, "Sequence Asset Folder");
            AddAssetRow(root.Q<VisualElement>("ui-auto-bind-container"), uiAutoBindGlobalSettingPath, "Global Setting Asset");

            var tableContainer = root.Q<VisualElement>("table-importer-container");
            AddFolderRow(tableContainer, tableImporterRootFolder, "Table Importer Root");
            AddFolderRow(tableContainer, tableImporterUiFolder, "Table Importer UI");
            AddFolderRow(tableContainer, generatedImporterFolder, "Generated Importers");
            AddFolderRow(tableContainer, generatedRuntimeScriptFolder, "Generated Runtime Scripts");
            AddFolderRow(tableContainer, generatedDefaultAssetFolder, "Generated SO Assets");
            AddFolderRow(tableContainer, generatedCsvFolder, "Generated CSV Folder");

            return root;
        }

        private static VisualElement LoadRoot()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            var root = new VisualElement();
            if (visualTree == null || styleSheet == null)
            {
                root.Add(new Label($"UnityEasyWorkToolsPathSettings UI 资源缺失: {LayoutPath}."));
                return root;
            }

            visualTree.CloneTree(root);
            root.styleSheets.Add(styleSheet);
            return root;
        }

        private void AddFolderRow(VisualElement parent, SerializedProperty property, string label)
        {
            AddPathRow(parent, property, label, isAssetFile: false);
        }

        private void AddAssetRow(VisualElement parent, SerializedProperty property, string label)
        {
            AddPathRow(parent, property, label, isAssetFile: true);
        }

        private void AddPathRow(VisualElement parent, SerializedProperty property, string label, bool isAssetFile)
        {
            var row = new VisualElement();
            row.AddToClassList("uewt-path-row");

            var field = new TextField(label) { value = property.stringValue };
            field.AddToClassList("uewt-path-field");
            field.RegisterValueChangedCallback(evt => SetPropertyValue(property, evt.newValue));
            row.Add(field);

            var selectButton = new Button(() =>
            {
                var selected = isAssetFile
                    ? SelectAssetPath(property.stringValue, label)
                    : SelectFolderPath(property.stringValue, label);
                SetPropertyValue(property, selected);
                field.SetValueWithoutNotify(selected);
            })
            {
                text = "选择"
            };
            selectButton.AddToClassList("uewt-small-button");
            row.Add(selectButton);

            var openButton = new Button(() => RevealPath(property.stringValue, isAssetFile)) { text = "打开" };
            openButton.AddToClassList("uewt-small-button");
            row.Add(openButton);

            parent.Add(row);
        }

        private void SetPropertyValue(SerializedProperty property, string value)
        {
            serializedObject.Update();
            property.stringValue = NormalizePath(value);
            serializedObject.ApplyModifiedProperties();
        }

        private static string SelectFolderPath(string currentAssetPath, string label)
        {
            var currentAbsolutePath = ResolveExistingAbsolutePath(currentAssetPath, isAssetFile: false);
            var selectedAbsolutePath = EditorUtility.OpenFolderPanel($"选择 {label}", currentAbsolutePath, string.Empty);
            if (string.IsNullOrEmpty(selectedAbsolutePath))
            {
                return NormalizePath(currentAssetPath);
            }

            return ConvertAbsoluteToAssetPath(selectedAbsolutePath, currentAssetPath);
        }

        private static string SelectAssetPath(string currentAssetPath, string label)
        {
            currentAssetPath = NormalizePath(currentAssetPath);
            var defaultFolder = Path.GetDirectoryName(currentAssetPath)?.Replace('\\', '/') ?? "Assets";
            var defaultName = Path.GetFileNameWithoutExtension(currentAssetPath);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = label.Replace(" ", string.Empty);
            }

            var selectedAssetPath = EditorUtility.SaveFilePanelInProject(
                $"选择 {label}",
                defaultName,
                "asset",
                "选择或输入一个 ScriptableObject 资产路径.",
                defaultFolder);

            return string.IsNullOrEmpty(selectedAssetPath)
                ? currentAssetPath
                : NormalizePath(selectedAssetPath);
        }

        private static void RevealPath(string assetPath, bool isAssetFile)
        {
            assetPath = NormalizePath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                EditorUtility.DisplayDialog("UnityEasyWorkTools", "路径为空.", "确认");
                return;
            }

            var objectToPing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (objectToPing != null)
            {
                Selection.activeObject = objectToPing;
                EditorGUIUtility.PingObject(objectToPing);
            }

            var absolutePath = AssetPathToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
            {
                absolutePath = ResolveExistingAbsolutePath(assetPath, isAssetFile);
            }

            EditorUtility.RevealInFinder(absolutePath);
        }

        private static string ResolveExistingAbsolutePath(string assetPath, bool isAssetFile)
        {
            assetPath = NormalizePath(assetPath);
            var absolutePath = AssetPathToAbsolutePath(assetPath);
            if (isAssetFile)
            {
                absolutePath = Path.GetDirectoryName(absolutePath) ?? ProjectRoot;
            }

            while (!string.IsNullOrEmpty(absolutePath) && !Directory.Exists(absolutePath))
            {
                absolutePath = Path.GetDirectoryName(absolutePath);
            }

            return string.IsNullOrEmpty(absolutePath) ? Application.dataPath : absolutePath;
        }

        private static string ConvertAbsoluteToAssetPath(string absolutePath, string fallbackAssetPath)
        {
            absolutePath = NormalizePath(absolutePath);
            var projectRoot = NormalizePath(ProjectRoot);
            if (absolutePath.Equals(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (!absolutePath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("UnityEasyWorkTools", "请选择当前 Unity 项目 Assets 目录下的路径.", "确认");
                return NormalizePath(fallbackAssetPath);
            }

            var assetPath = absolutePath.Substring(projectRoot.Length + 1);
            if (!assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("UnityEasyWorkTools", "请选择当前 Unity 项目 Assets 目录下的路径.", "确认");
                return NormalizePath(fallbackAssetPath);
            }

            return assetPath;
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            assetPath = NormalizePath(assetPath);
            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            return NormalizePath(Path.Combine(ProjectRoot, assetPath));
        }

        private static string ProjectRoot
        {
            get
            {
                var dataPath = NormalizePath(Application.dataPath);
                return NormalizePath(Directory.GetParent(dataPath)?.FullName ?? dataPath);
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }
    }
}
