using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(AutoBindGlobalSetting))]
public class AutoBindGlobalSettingInspector : Editor
{
    private const string LayoutPath = "Assets/UnityEasyWorkTools/UIAutoBind/Editor/UI/AutoBindGlobalSettingInspector.uxml";
    private const string StylePath = "Assets/UnityEasyWorkTools/UIAutoBind/Editor/UI/UIAutoBindInspectors.uss";

    private SerializedProperty m_Namespace;
    private SerializedProperty m_CodePath;
    private SerializedProperty m_BaseClassName;

    private void OnEnable()
    {
        m_Namespace = serializedObject.FindProperty("m_Namespace");
        m_CodePath = serializedObject.FindProperty("m_CodePath");
        m_BaseClassName = serializedObject.FindProperty("m_BaseClassName");
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = LoadRoot();
        var container = root.Q<VisualElement>("setting-fields-container");
        AddTextRow(container, m_Namespace, "默认命名空间");
        AddTextRow(container, m_BaseClassName, "默认继承类");
        AddCodePathRow(container);
        return root;
    }

    private static VisualElement LoadRoot()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        var root = new VisualElement();
        if (visualTree == null || styleSheet == null)
        {
            root.Add(new Label($"AutoBindGlobalSetting UI 资源缺失: {LayoutPath}."));
            return root;
        }

        visualTree.CloneTree(root);
        root.styleSheets.Add(styleSheet);
        return root;
    }

    private void AddTextRow(VisualElement parent, SerializedProperty property, string label)
    {
        var field = new TextField(label) { value = property.stringValue };
        field.RegisterValueChangedCallback(evt => SetStringProperty(property, evt.newValue));
        parent.Add(field);
    }

    private void AddCodePathRow(VisualElement parent)
    {
        var row = new VisualElement();
        row.AddToClassList("uewt-row");

        var field = new TextField("默认代码保存路径") { value = m_CodePath.stringValue };
        field.AddToClassList("uewt-grow");
        field.RegisterValueChangedCallback(evt => SetStringProperty(m_CodePath, evt.newValue));
        row.Add(field);

        var selectButton = new Button(() =>
        {
            var path = EditorUtility.OpenFolderPanel("选择代码保存路径", string.IsNullOrEmpty(m_CodePath.stringValue) ? Application.dataPath : m_CodePath.stringValue, string.Empty);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            SetStringProperty(m_CodePath, path);
            field.SetValueWithoutNotify(path);
        })
        {
            text = "选择"
        };
        selectButton.AddToClassList("uewt-small-button");
        row.Add(selectButton);

        parent.Add(row);
    }

    private void SetStringProperty(SerializedProperty property, string value)
    {
        serializedObject.Update();
        property.stringValue = value;
        serializedObject.ApplyModifiedProperties();
    }
}
