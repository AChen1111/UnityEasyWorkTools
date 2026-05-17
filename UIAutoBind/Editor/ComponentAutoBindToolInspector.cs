using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using BindData = ComponentAutoBindTool.BindData;

[InitializeOnLoad]
[CustomEditor(typeof(ComponentAutoBindTool))]
public class ComponentAutoBindToolInspector : Editor
{
    private const string LayoutPath = "Assets/UnityEasyWorkTools/UIAutoBind/Editor/UI/ComponentAutoBindToolInspector.uxml";
    private const string StylePath = "Assets/UnityEasyWorkTools/UIAutoBind/Editor/UI/UIAutoBindInspectors.uss";
    private const string AutoAttachObjectIdKey = "ComponentAutoBindTool.AutoAttachObjectId";
    private const string AutoAttachTypeNameKey = "ComponentAutoBindTool.AutoAttachTypeName";
    private const string AutoAttachWaitingKey = "ComponentAutoBindTool.AutoAttachWaiting";
    private const string AutoAttachRetryCountKey = "ComponentAutoBindTool.AutoAttachRetryCount";
    private const int AutoAttachMaxRetryCount = 120;

    private readonly List<BindData> m_TempList = new List<BindData>();
    private readonly List<string> m_TempFiledNames = new List<string>();
    private readonly List<string> m_TempComponentTypeNames = new List<string>();
    private readonly string[] s_AssemblyNames = { "ComponentAutoBindTool.Runtime", "Assembly-CSharp", "Assembly-CSharp-firstpass" };

    private ComponentAutoBindTool m_Target;
    private SerializedProperty m_BindDatas;
    private SerializedProperty m_BindComs;
    private SerializedProperty m_Namespace;
    private SerializedProperty m_ClassName;
    private SerializedProperty m_CodePath;
    private SerializedProperty m_BaseClassName;
    private string[] m_HelperTypeNames;
    private string m_HelperTypeName;
    private int m_HelperTypeNameIndex;
    private AutoBindGlobalSetting m_Setting;
    private ScrollView bindListScroll;
    private Label bindListTitle;

    static ComponentAutoBindToolInspector()
    {
        EditorApplication.delayCall += TryAttachPendingGeneratedScript;
    }

    private void OnEnable()
    {
        m_Target = (ComponentAutoBindTool)target;
        m_BindDatas = serializedObject.FindProperty("BindDatas");
        m_BindComs = serializedObject.FindProperty("m_BindComs");
        m_Namespace = serializedObject.FindProperty("m_Namespace");
        m_ClassName = serializedObject.FindProperty("m_ClassName");
        m_CodePath = serializedObject.FindProperty("m_CodePath");
        m_BaseClassName = serializedObject.FindProperty("m_BaseClassName");

        m_HelperTypeNames = GetTypeNames(typeof(IAutoBindRuleHelper), s_AssemblyNames);
        LoadGlobalSetting();
        ApplyDefaultSettings();
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = LoadRoot();
        bindListScroll = root.Q<ScrollView>("bind-list-scroll");
        bindListTitle = root.Q<Label>("bind-list-title");

        BuildToolbar(root.Q<VisualElement>("toolbar-container"));
        BuildHelperSelect(root.Q<VisualElement>("helper-container"));
        BuildSettings(root.Q<VisualElement>("settings-container"));
        RefreshBindList();

        return root;
    }

    private static VisualElement LoadRoot()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        var root = new VisualElement();
        if (visualTree == null || styleSheet == null)
        {
            root.Add(new Label($"ComponentAutoBindTool UI 资源缺失: {LayoutPath}."));
            return root;
        }

        visualTree.CloneTree(root);
        root.styleSheets.Add(styleSheet);
        return root;
    }

    private void LoadGlobalSetting()
    {
        string[] paths = AssetDatabase.FindAssets("t:AutoBindGlobalSetting");
        if (paths.Length == 0)
        {
            Debug.LogError("不存在AutoBindGlobalSetting");
            return;
        }

        if (paths.Length > 1)
        {
            Debug.LogError("AutoBindGlobalSetting数量大于1");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(paths[0]);
        m_Setting = AssetDatabase.LoadAssetAtPath<AutoBindGlobalSetting>(path);
    }

    private void ApplyDefaultSettings()
    {
        if (m_Setting == null)
        {
            return;
        }

        serializedObject.Update();
        m_Namespace.stringValue = string.IsNullOrEmpty(m_Namespace.stringValue) ? m_Setting.Namespace : m_Namespace.stringValue;
        m_ClassName.stringValue = string.IsNullOrEmpty(m_ClassName.stringValue) ? m_Target.gameObject.name : m_ClassName.stringValue;
        m_CodePath.stringValue = string.IsNullOrEmpty(m_CodePath.stringValue) ? m_Setting.CodePath : m_CodePath.stringValue;
        m_BaseClassName.stringValue = string.IsNullOrEmpty(m_BaseClassName.stringValue) ? m_Setting.BaseClassName : m_BaseClassName.stringValue;
        serializedObject.ApplyModifiedProperties();
    }

    private void BuildToolbar(VisualElement toolbar)
    {
        toolbar.Add(CreateToolbarButton("排序", () => ExecuteAndRefresh(Sort)));
        toolbar.Add(CreateToolbarButton("全部删除", () => ExecuteAndRefresh(RemoveAll)));
        toolbar.Add(CreateToolbarButton("删除空引用", () => ExecuteAndRefresh(RemoveNull)));
        toolbar.Add(CreateToolbarButton("自动绑定组件", () => ExecuteAndRefresh(AutoBindComponent)));
        toolbar.Add(CreateToolbarButton("生成绑定代码", () =>
        {
            serializedObject.ApplyModifiedProperties();
            GenAutoBindCode();
        }));
    }

    private static Button CreateToolbarButton(string text, Action action)
    {
        var button = new Button(action) { text = text };
        button.AddToClassList("uewt-toolbar-button");
        return button;
    }

    private void BuildHelperSelect(VisualElement parent)
    {
        parent.Clear();
        parent.Add(new Label("自动绑定规则") { name = "helper-title" });

        if (m_HelperTypeNames == null || m_HelperTypeNames.Length == 0)
        {
            var help = new Label("未找到 IAutoBindRuleHelper 实现, 请确认 DefaultAutoBindRuleHelper 已正常编译.");
            help.AddToClassList("uewt-help");
            parent.Add(help);
            return;
        }

        ResolveHelperSelection();
        var popup = new PopupField<string>("AutoBindRuleHelper", new List<string>(m_HelperTypeNames), m_HelperTypeNameIndex);
        popup.RegisterValueChangedCallback(evt =>
        {
            m_HelperTypeName = evt.newValue;
            m_HelperTypeNameIndex = Array.IndexOf(m_HelperTypeNames, m_HelperTypeName);
            m_Target.RuleHelper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, s_AssemblyNames);
            EditorUtility.SetDirty(m_Target);
        });
        parent.Add(popup);
    }

    private void ResolveHelperSelection()
    {
        m_HelperTypeName = m_HelperTypeNames[0];
        m_HelperTypeNameIndex = 0;

        if (m_Target.RuleHelper != null)
        {
            var currentTypeName = m_Target.RuleHelper.GetType().FullName;
            var currentIndex = Array.IndexOf(m_HelperTypeNames, currentTypeName);
            if (currentIndex >= 0)
            {
                m_HelperTypeName = currentTypeName;
                m_HelperTypeNameIndex = currentIndex;
            }
        }

        if (m_Target.RuleHelper == null)
        {
            m_Target.RuleHelper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, s_AssemblyNames);
            EditorUtility.SetDirty(m_Target);
        }

        foreach (GameObject go in Selection.gameObjects)
        {
            var autoBindTool = go.GetComponent<ComponentAutoBindTool>();
            if (autoBindTool != null && autoBindTool.RuleHelper == null)
            {
                autoBindTool.RuleHelper = (IAutoBindRuleHelper)CreateHelperInstance(m_HelperTypeName, s_AssemblyNames);
                EditorUtility.SetDirty(autoBindTool);
            }
        }
    }

    private void BuildSettings(VisualElement parent)
    {
        parent.Clear();
        parent.Add(new Label("代码生成设置") { name = "settings-title" });
        AddTextRow(parent, m_Namespace, "命名空间", "默认设置", () => m_Setting?.Namespace);
        AddTextRow(parent, m_ClassName, "类名", "物体名", () => m_Target.gameObject.name);
        AddTextRow(parent, m_BaseClassName, "继承类", "默认设置", () => m_Setting?.BaseClassName);
        AddCodePathRow(parent);
    }

    private void AddTextRow(VisualElement parent, SerializedProperty property, string label, string buttonText, Func<string> buttonValue)
    {
        var row = new VisualElement();
        row.AddToClassList("uewt-row");

        var field = new TextField(label) { value = property.stringValue };
        field.AddToClassList("uewt-grow");
        field.RegisterValueChangedCallback(evt => SetStringProperty(property, evt.newValue));
        row.Add(field);

        var button = new Button(() =>
        {
            var value = buttonValue.Invoke();
            if (value == null)
            {
                return;
            }

            SetStringProperty(property, value);
            field.SetValueWithoutNotify(value);
        })
        {
            text = buttonText
        };
        button.AddToClassList("uewt-small-button");
        row.Add(button);
        parent.Add(row);
    }

    private void AddCodePathRow(VisualElement parent)
    {
        var row = new VisualElement();
        row.AddToClassList("uewt-row");

        var field = new TextField("代码保存路径") { value = m_CodePath.stringValue };
        field.AddToClassList("uewt-grow");
        field.RegisterValueChangedCallback(evt => SetStringProperty(m_CodePath, evt.newValue));
        row.Add(field);

        var selectButton = new Button(() =>
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "选择代码保存路径",
                string.IsNullOrEmpty(m_CodePath.stringValue) ? Application.dataPath : m_CodePath.stringValue,
                string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            SetStringProperty(m_CodePath, selectedPath);
            field.SetValueWithoutNotify(selectedPath);
        })
        {
            text = "选择"
        };
        selectButton.AddToClassList("uewt-small-button");
        row.Add(selectButton);

        var defaultButton = new Button(() =>
        {
            if (m_Setting == null)
            {
                return;
            }

            SetStringProperty(m_CodePath, m_Setting.CodePath);
            field.SetValueWithoutNotify(m_Setting.CodePath);
        })
        {
            text = "默认设置"
        };
        defaultButton.AddToClassList("uewt-small-button");
        row.Add(defaultButton);
        parent.Add(row);
    }

    private void RefreshBindList()
    {
        if (bindListScroll == null)
        {
            return;
        }

        serializedObject.Update();
        bindListScroll.Clear();
        bindListTitle.text = $"绑定数据: {m_BindDatas.arraySize}";

        for (int i = 0; i < m_BindDatas.arraySize; i++)
        {
            bindListScroll.Add(CreateBindRow(i));
        }
    }

    private VisualElement CreateBindRow(int index)
    {
        var row = new VisualElement();
        row.AddToClassList("uewt-bind-row");
        var indexLabel = new Label($"[{index}]") { name = $"bind-index-{index}" };
        indexLabel.AddToClassList("uewt-index-label");
        row.Add(indexLabel);

        var bindData = m_BindDatas.GetArrayElementAtIndex(index);
        var nameProperty = bindData.FindPropertyRelative("Name");
        var componentProperty = bindData.FindPropertyRelative("BindCom");

        var nameField = new TextField { value = nameProperty.stringValue };
        nameField.AddToClassList("uewt-name-field");
        nameField.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            nameProperty.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
        });
        row.Add(nameField);

        var componentField = new ObjectField
        {
            objectType = typeof(Component),
            allowSceneObjects = true,
            value = componentProperty.objectReferenceValue
        };
        componentField.AddToClassList("uewt-component-field");
        componentField.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            componentProperty.objectReferenceValue = evt.newValue;
            SyncBindComs();
            serializedObject.ApplyModifiedProperties();
        });
        row.Add(componentField);

        var deleteButton = new Button(() =>
        {
            serializedObject.Update();
            m_BindDatas.DeleteArrayElementAtIndex(index);
            SyncBindComs();
            serializedObject.ApplyModifiedProperties();
            RefreshBindList();
        })
        {
            text = "X"
        };
        deleteButton.AddToClassList("uewt-danger-button");
        row.Add(deleteButton);

        return row;
    }

    private void ExecuteAndRefresh(Action action)
    {
        serializedObject.Update();
        action.Invoke();
        serializedObject.ApplyModifiedProperties();
        RefreshBindList();
    }

    private void SetStringProperty(SerializedProperty property, string value)
    {
        serializedObject.Update();
        property.stringValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(m_Target);
    }

    /// <summary>
    /// 排序.
    /// </summary>
    private void Sort()
    {
        m_TempList.Clear();
        foreach (BindData data in m_Target.BindDatas)
        {
            m_TempList.Add(new BindData(data.Name, data.BindCom));
        }

        m_TempList.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));
        m_BindDatas.ClearArray();
        foreach (BindData data in m_TempList)
        {
            AddBindData(data.Name, data.BindCom);
        }

        SyncBindComs();
    }

    /// <summary>
    /// 删除全部绑定数据.
    /// </summary>
    private void RemoveAll()
    {
        m_BindDatas.ClearArray();
        SyncBindComs();
    }

    /// <summary>
    /// 删除空组件引用.
    /// </summary>
    private void RemoveNull()
    {
        for (int i = m_BindDatas.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("BindCom");
            if (element.objectReferenceValue == null)
            {
                m_BindDatas.DeleteArrayElementAtIndex(i);
            }
        }

        SyncBindComs();
    }

    /// <summary>
    /// 按规则扫描子节点并自动绑定组件.
    /// </summary>
    private void AutoBindComponent()
    {
        if (m_Target.RuleHelper == null)
        {
            Debug.LogError("自动绑定失败, 未找到可用的绑定规则辅助器.");
            return;
        }

        m_BindDatas.ClearArray();
        Transform[] childs = m_Target.gameObject.GetComponentsInChildren<Transform>();
        foreach (Transform child in childs)
        {
            m_TempFiledNames.Clear();
            m_TempComponentTypeNames.Clear();

            if (!m_Target.RuleHelper.IsValidBind(child, m_TempFiledNames, m_TempComponentTypeNames))
            {
                continue;
            }

            for (int i = 0; i < m_TempFiledNames.Count; i++)
            {
                Component com = child.GetComponent(m_TempComponentTypeNames[i]);
                if (com == null)
                {
                    Debug.LogError($"{child.name}上不存在{m_TempComponentTypeNames[i]}的组件");
                    continue;
                }

                AddBindData(m_TempFiledNames[i], com);
            }
        }

        SyncBindComs();
    }

    /// <summary>
    /// 添加绑定数据.
    /// </summary>
    private void AddBindData(string name, Component bindCom)
    {
        int index = m_BindDatas.arraySize;
        m_BindDatas.InsertArrayElementAtIndex(index);
        SerializedProperty element = m_BindDatas.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("Name").stringValue = name;
        element.FindPropertyRelative("BindCom").objectReferenceValue = bindCom;
    }

    /// <summary>
    /// 同步运行时组件列表, 保持生成代码索引与组件索引一致.
    /// </summary>
    private void SyncBindComs()
    {
        m_BindComs.ClearArray();
        for (int i = 0; i < m_BindDatas.arraySize; i++)
        {
            SerializedProperty property = m_BindDatas.GetArrayElementAtIndex(i).FindPropertyRelative("BindCom");
            m_BindComs.InsertArrayElementAtIndex(i);
            m_BindComs.GetArrayElementAtIndex(i).objectReferenceValue = property.objectReferenceValue;
        }
    }

    /// <summary>
    /// 获取指定基类在指定程序集中的所有子类名称.
    /// </summary>
    private string[] GetTypeNames(Type typeBase, string[] assemblyNames)
    {
        List<string> typeNames = new List<string>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == null || Array.IndexOf(assemblyNames, assembly.GetName().Name) < 0)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // 忽略加载失败的类型, 保留已经成功加载的辅助器类型.
                types = Array.FindAll(e.Types, type => type != null);
            }

            foreach (Type type in types)
            {
                if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                {
                    typeNames.Add(type.FullName);
                }
            }
        }

        typeNames.Sort();
        return typeNames.ToArray();
    }

    /// <summary>
    /// 创建辅助器实例.
    /// </summary>
    private object CreateHelperInstance(string helperTypeName, string[] assemblyNames)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == null || Array.IndexOf(assemblyNames, assembly.GetName().Name) < 0)
            {
                continue;
            }

            object instance = assembly.CreateInstance(helperTypeName);
            if (instance != null)
            {
                return instance;
            }
        }

        return null;
    }

    /// <summary>
    /// 生成自动绑定代码.
    /// </summary>
    private void GenAutoBindCode()
    {
        GameObject go = m_Target.gameObject;
        if (m_Setting == null)
        {
            Debug.LogError("生成自动绑定代码失败, 缺少 AutoBindGlobalSetting.");
            return;
        }

        string className = !string.IsNullOrEmpty(m_Target.ClassName) ? m_Target.ClassName : go.name;
        string codePath = !string.IsNullOrEmpty(m_Target.CodePath) ? m_Target.CodePath : m_Setting.CodePath;

        if (!Directory.Exists(codePath))
        {
            Debug.LogError($"{go.name}的代码保存路径{codePath}无效");
            return;
        }

        GenMainCode(codePath, className);
        GenBindComponentCode(codePath, className);
        RegisterPendingGeneratedScriptAttach(go, className);
        AssetDatabase.Refresh();

        if (!EditorApplication.isCompiling)
        {
            TryAttachPendingGeneratedScript();
        }

        EditorUtility.DisplayDialog("提示", "代码生成完毕, 编译完成后会自动挂载主类脚本.", "OK");
    }

    /// <summary>
    /// 记录待挂载脚本信息, 用于编译完成后继续挂载.
    /// </summary>
    private void RegisterPendingGeneratedScriptAttach(GameObject go, string className)
    {
        string namespaceName = m_Target.Namespace;
        string fullTypeName = string.IsNullOrEmpty(namespaceName) ? className : $"{namespaceName}.{className}";
        GlobalObjectId objectId = GlobalObjectId.GetGlobalObjectIdSlow(go);

        EditorPrefs.SetString(AutoAttachObjectIdKey, objectId.ToString());
        EditorPrefs.SetString(AutoAttachTypeNameKey, fullTypeName);
        EditorPrefs.SetBool(AutoAttachWaitingKey, true);
        EditorPrefs.SetInt(AutoAttachRetryCountKey, AutoAttachMaxRetryCount);
    }

    /// <summary>
    /// 尝试挂载生成的主类脚本, 编译未完成时延后执行.
    /// </summary>
    private static void TryAttachPendingGeneratedScript()
    {
        if (!EditorPrefs.GetBool(AutoAttachWaitingKey, false))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAttachPendingGeneratedScript;
            return;
        }

        string objectIdText = EditorPrefs.GetString(AutoAttachObjectIdKey, string.Empty);
        string typeName = EditorPrefs.GetString(AutoAttachTypeNameKey, string.Empty);
        if (string.IsNullOrEmpty(objectIdText) || string.IsNullOrEmpty(typeName))
        {
            ClearPendingGeneratedScriptAttach();
            return;
        }

        if (!GlobalObjectId.TryParse(objectIdText, out GlobalObjectId objectId))
        {
            Debug.LogError($"自动挂载失败, 无法解析目标对象ID: {objectIdText}.");
            ClearPendingGeneratedScriptAttach();
            return;
        }

        GameObject go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId) as GameObject;
        if (go == null)
        {
            Debug.LogError("自动挂载失败, 目标对象不存在或已被删除.");
            ClearPendingGeneratedScriptAttach();
            return;
        }

        Type componentType = FindType(typeName);
        if (componentType == null)
        {
            int retryCount = EditorPrefs.GetInt(AutoAttachRetryCountKey, 0);
            if (retryCount > 0)
            {
                // 脚本类型可能仍在编译或载入, 下一帧继续尝试.
                EditorPrefs.SetInt(AutoAttachRetryCountKey, retryCount - 1);
                EditorApplication.delayCall += TryAttachPendingGeneratedScript;
                return;
            }

            Debug.LogError($"自动挂载失败, 未找到生成的脚本类型: {typeName}.");
            ClearPendingGeneratedScriptAttach();
            return;
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
        {
            Debug.LogError($"自动挂载失败, 类型不是MonoBehaviour: {typeName}.");
            ClearPendingGeneratedScriptAttach();
            return;
        }

        if (go.GetComponent(componentType) == null)
        {
            Undo.AddComponent(go, componentType);
            EditorUtility.SetDirty(go);
            if (go.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }

            Debug.Log($"自动挂载成功: {typeName} -> {go.name}.");
        }

        ClearPendingGeneratedScriptAttach();
    }

    /// <summary>
    /// 清理待挂载脚本信息, 避免重复执行.
    /// </summary>
    private static void ClearPendingGeneratedScriptAttach()
    {
        EditorPrefs.DeleteKey(AutoAttachObjectIdKey);
        EditorPrefs.DeleteKey(AutoAttachTypeNameKey);
        EditorPrefs.DeleteKey(AutoAttachWaitingKey);
        EditorPrefs.DeleteKey(AutoAttachRetryCountKey);
    }

    /// <summary>
    /// 在所有已加载程序集中查找类型, 支持 asmdef 程序集.
    /// </summary>
    private static Type FindType(string fullTypeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullTypeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// 生成主逻辑代码文件.
    /// </summary>
    private void GenMainCode(string codePath, string className)
    {
        string mainCodeFilePath = $"{codePath}/{className}.cs";
        if (File.Exists(mainCodeFilePath))
        {
            return;
        }

        using (StreamWriter sw = new StreamWriter(mainCodeFilePath))
        {
            sw.WriteLine("using UnityEngine;");
            sw.WriteLine("");

            if (!string.IsNullOrEmpty(m_Target.Namespace))
            {
                // 命名空间.
                sw.WriteLine("namespace " + m_Target.Namespace);
                sw.WriteLine("{");
                sw.WriteLine("");
            }

            // 主逻辑类.
            sw.WriteLine($"\tpublic partial class {className} : {GetMainBaseClassName()}");
            sw.WriteLine("\t{");
            sw.WriteLine("\t\tprivate void Awake()");
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\tGetBindComponents(gameObject);");
            sw.WriteLine("\t\t}");
            sw.WriteLine("\t}");

            if (!string.IsNullOrEmpty(m_Target.Namespace))
            {
                sw.WriteLine("}");
            }
        }
    }

    /// <summary>
    /// 获取主类继承类型名称.
    /// </summary>
    private string GetMainBaseClassName()
    {
        if (!string.IsNullOrEmpty(m_Target.BaseClassName))
        {
            return m_Target.BaseClassName;
        }

        return m_Setting.BaseClassName;
    }

    /// <summary>
    /// 生成组件绑定代码文件.
    /// </summary>
    private void GenBindComponentCode(string codePath, string className)
    {
        using (StreamWriter sw = new StreamWriter($"{codePath}/{className}.BindComponent.cs"))
        {
            sw.WriteLine("using UnityEngine;");
            sw.WriteLine("using UnityEngine.UI;");
            sw.WriteLine("");
            sw.WriteLine("// 自动生成于: " + DateTime.Now);

            if (!string.IsNullOrEmpty(m_Target.Namespace))
            {
                // 命名空间.
                sw.WriteLine("namespace " + m_Target.Namespace);
                sw.WriteLine("{");
                sw.WriteLine("");
            }

            // 绑定代码类.
            sw.WriteLine($"\tpublic partial class {className}");
            sw.WriteLine("\t{");
            sw.WriteLine("");

            foreach (BindData data in m_Target.BindDatas)
            {
                sw.WriteLine($"\t\tprivate {data.BindCom.GetType().Name} m_{data.Name};");
            }

            sw.WriteLine("");
            sw.WriteLine("\t\tprivate void GetBindComponents(GameObject go)");
            sw.WriteLine("\t\t{");
            sw.WriteLine("\t\t\tComponentAutoBindTool autoBindTool = go.GetComponent<ComponentAutoBindTool>();");
            sw.WriteLine("");

            for (int i = 0; i < m_Target.BindDatas.Count; i++)
            {
                BindData data = m_Target.BindDatas[i];
                string filedName = $"m_{data.Name}";
                sw.WriteLine($"\t\t\t{filedName} = autoBindTool.GetBindComponent<{data.BindCom.GetType().Name}>({i});");
            }

            sw.WriteLine("\t\t}");
            sw.WriteLine("\t}");

            if (!string.IsNullOrEmpty(m_Target.Namespace))
            {
                sw.WriteLine("}");
            }
        }
    }
}
