using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEasyWorkTools.Editor;

public sealed class Excel2SoImporterWindow : EditorWindow
{
    private const string MenuPath = "Tools/UnityEasyWorkTools/Table Importer/Importer Window";

    private static readonly string[] LayoutFileNames =
    {
        "Excel2SoImporterWindow.uxml",
        "Excel2SOImporterWindow.uxml"
    };

    private static readonly string[] StyleFileNames =
    {
        "Excel2SoImporterWindow.uss",
        "Excel2SOImporterWindow.uss"
    };

    private readonly List<ImporterOption> importerOptions = new List<ImporterOption>();
    private readonly List<OperationMode> modeOptions = new List<OperationMode>
    {
        OperationMode.Excel2SO,
        OperationMode.SO2Table,
        OperationMode.CodeGen
    };

    private readonly List<Excel2SoCodeGenFieldKind> codeGenFieldKindOptions =
        Excel2SoCodeGenTypeRegistry.Definitions.Select(definition => definition.Kind).ToList();

    private readonly List<CodeGenFieldRow> codeGenFieldRows = new List<CodeGenFieldRow>();

    private PopupField<ImporterOption> importerField;
    private VisualElement formRoot;
    private VisualElement modeSidebar;
    private VisualElement importerRow;
    private VisualElement tablePathRow;
    private VisualElement targetPathRow;
    private VisualElement codeGenRoot;
    private VisualElement codeGenFieldsContainer;
    private Label tablePathLabel;
    private Label targetPathLabel;
    private TextField tablePathField;
    private TextField targetPathField;
    private Button tableBrowseButton;
    private Button targetBrowseButton;
    private Button targetDefaultButton;
    private Button importButton;
    private Label statusLabel;
    private TextField codeGenNamePrefixField;
    private TextField codeGenListPropertyField;
    private TextField codeGenRuntimeFolderField;
    private TextField codeGenEditorFolderField;
    private TextField codeGenAssetPathField;
    private TextField codeGenCsvPathField;
    private ImporterOption selectedImporter;
    private OperationMode selectedMode = OperationMode.Excel2SO;
    private string lastAutoCodeGenAssetPath;
    private string lastAutoCodeGenCsvPath;
    private readonly Dictionary<OperationMode, Button> modeButtons = new Dictionary<OperationMode, Button>();

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var window = GetWindow<Excel2SoImporterWindow>();
        window.titleContent = new GUIContent("UnityEasyWorkTools Table Importer / 表格工具");
        window.minSize = new Vector2(860f, 520f);
        window.Show();
    }

    public void CreateGUI()
    {
        importerOptions.Clear();
        importerOptions.AddRange(DiscoverImporters());

        var root = rootVisualElement;
        root.Clear();
        if (!LoadWindowLayout(root))
        {
            return;
        }

        if (!TryBindLayout(root, out var importerContainer))
        {
            return;
        }

        selectedImporter = importerOptions.Count > 0 ? importerOptions[0] : null;
        selectedMode = importerOptions.Count > 0 ? OperationMode.Excel2SO : OperationMode.CodeGen;

        BuildModeSidebar();

        if (importerOptions.Count > 0)
        {
            importerField = new PopupField<ImporterOption>(string.Empty, importerOptions, selectedImporter);
            importerField.AddToClassList("importer-popup");
            importerField.formatListItemCallback = FormatImporterOption;
            importerField.formatSelectedValueCallback = FormatImporterOption;
            importerField.RegisterValueChangedCallback(evt =>
            {
                selectedImporter = evt.newValue;
                ResetPathsForMode();
                UpdateActionState();
            });
            importerContainer.Add(importerField);
        }
        else
        {
            importerContainer.Add(new Label("No list asset importers were found. / 未找到列表型导入器。"));
        }

        tablePathField.RegisterValueChangedCallback(_ => UpdateActionState());
        targetPathField.RegisterValueChangedCallback(_ => UpdateActionState());

        tableBrowseButton.clicked += SelectTablePath;
        targetBrowseButton.clicked += SelectTargetPath;
        targetDefaultButton.clicked += ResetTargetPath;
        importButton.clicked += ExecuteSelected;

        BuildCodeGenUi();
        ApplyModeUi(resetPaths: true);
    }

    private static bool LoadWindowLayout(VisualElement root)
    {
        var layoutPaths = BuildToolAssetPaths(LayoutFileNames).ToArray();
        var visualTree = LoadFirstAsset<VisualTreeAsset>(layoutPaths)
                         ?? FindFirstNamedAsset<VisualTreeAsset>(LayoutFileNames);
        if (visualTree == null)
        {
            root.Add(new Label($"Missing Excel2SO importer layout / 缺少 Excel2SO 窗口布局: {string.Join(", ", layoutPaths)}"));
            return false;
        }

        visualTree.CloneTree(root);

        var styleSheet = LoadFirstAsset<StyleSheet>(BuildToolAssetPaths(StyleFileNames))
                         ?? FindFirstNamedAsset<StyleSheet>(StyleFileNames);
        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
        }

        return true;
    }

    private static IEnumerable<string> BuildToolAssetPaths(IEnumerable<string> fileNames)
    {
        var pathSettings = UnityEasyWorkToolsPathSettings.GetOrCreate();
        // UXML/USS 通过共享路径配置加载, 方便插件目录后续调整.
        foreach (var fileName in fileNames)
        {
            yield return $"{pathSettings.TableImporterUiFolder}/{fileName}";
        }
    }

    private static TAsset LoadFirstAsset<TAsset>(IEnumerable<string> assetPaths)
        where TAsset : UnityEngine.Object
    {
        foreach (var assetPath in assetPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
            if (asset != null)
            {
                return asset;
            }
        }

        return null;
    }

    private static TAsset FindFirstNamedAsset<TAsset>(IEnumerable<string> fileNames)
        where TAsset : UnityEngine.Object
    {
        foreach (var fileName in fileNames)
        {
            var assetName = Path.GetFileNameWithoutExtension(fileName);
            var pathSettings = UnityEasyWorkToolsPathSettings.GetOrCreate();
            var guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(TAsset).Name}", new[] { pathSettings.TableImporterRootFolder });

            // 文件名大小写不一致时, 按资源文件名兜底查找.
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileName(assetPath), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
                if (asset != null)
                {
                    return asset;
                }
            }
        }

        return null;
    }

    private bool TryBindLayout(
        VisualElement root,
        out VisualElement importerContainer)
    {
        formRoot = root.Q<VisualElement>("form-root");
        modeSidebar = root.Q<VisualElement>("mode-sidebar");
        importerRow = root.Q<VisualElement>("importer-row");
        tablePathRow = root.Q<VisualElement>("table-path-row");
        targetPathRow = root.Q<VisualElement>("target-path-row");
        codeGenRoot = root.Q<VisualElement>("codegen-root");
        importerContainer = root.Q<VisualElement>("importer-container");
        tablePathLabel = root.Q<Label>("table-path-label");
        targetPathLabel = root.Q<Label>("target-path-label");
        tablePathField = root.Q<TextField>("table-path-field");
        targetPathField = root.Q<TextField>("target-path-field");
        tableBrowseButton = root.Q<Button>("table-browse-button");
        targetBrowseButton = root.Q<Button>("target-browse-button");
        targetDefaultButton = root.Q<Button>("target-default-button");
        importButton = root.Q<Button>("import-button");
        statusLabel = root.Q<Label>("status-label");

        if (formRoot != null
            && modeSidebar != null
            && importerRow != null
            && tablePathRow != null
            && targetPathRow != null
            && codeGenRoot != null
            && importerContainer != null
            && tablePathLabel != null
            && targetPathLabel != null
            && tablePathField != null
            && targetPathField != null
            && tableBrowseButton != null
            && targetBrowseButton != null
            && targetDefaultButton != null
            && importButton != null
            && statusLabel != null)
        {
            return true;
        }

        root.Clear();
        statusLabel = new Label("Excel2SO importer layout is missing required controls. / Excel2SO 窗口布局缺少必要控件。");
        root.Add(statusLabel);
        return false;
    }

    private static IEnumerable<ImporterOption> DiscoverImporters()
    {
        return TypeCache.GetTypesDerivedFrom<Excel2SoImporterBase>()
            .Where(IsSupportedImporterType)
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .Select(type => new ImporterOption(type));
    }

    private static bool IsSupportedImporterType(Type type)
    {
        return type != null
               && !type.IsAbstract
               && !type.ContainsGenericParameters
               && typeof(IExcel2SoListAssetImporter).IsAssignableFrom(type)
               && type.GetConstructor(Type.EmptyTypes) != null;
    }

    private static string FormatImporterOption(ImporterOption option)
    {
        return option == null ? string.Empty : option.DisplayName;
    }

    private static string FormatModeOption(OperationMode mode)
    {
        switch (mode)
        {
            case OperationMode.Excel2SO:
                return "Excel2SO / 表转SO";
            case OperationMode.SO2Table:
                return "SO2Table / SO转表";
            case OperationMode.CodeGen:
                return "CodeGen / 生成代码";
            default:
                return mode.ToString();
        }
    }

    private void BuildModeSidebar()
    {
        modeSidebar.Clear();
        modeButtons.Clear();

        foreach (var mode in modeOptions)
        {
            var button = new Button(() =>
            {
                if (selectedMode == mode)
                {
                    return;
                }

                selectedMode = mode;
                ApplyModeUi(resetPaths: true);
            })
            {
                text = FormatModeOption(mode)
            };

            button.AddToClassList("mode-sidebar-button");
            modeSidebar.Add(button);
            modeButtons.Add(mode, button);
        }
    }

    private void RefreshModeSidebar()
    {
        foreach (var pair in modeButtons)
        {
            pair.Value.EnableInClassList("mode-sidebar-button-selected", pair.Key == selectedMode);
        }
    }

    private void BuildCodeGenUi()
    {
        codeGenRoot.Clear();
        codeGenFieldRows.Clear();
        var pathSettings = UnityEasyWorkToolsPathSettings.GetOrCreate();

        AddCodeGenSectionTitle("Scripts / 脚本");
        codeGenNamePrefixField = AddCodeGenTextRow("Name Prefix / 名称前缀", "NewTable");
        codeGenListPropertyField = AddCodeGenTextRow("List Property / 列表字段", "entries");
        codeGenRuntimeFolderField = AddCodeGenPathRow(
            "Runtime Folder / 运行时目录",
            pathSettings.GeneratedRuntimeScriptFolder,
            "Browse / 浏览",
            SelectCodeGenFolder);
        codeGenEditorFolderField = AddCodeGenPathRow(
            "Importer Folder / 导入器目录",
            pathSettings.GeneratedImporterFolder,
            "Browse / 浏览",
            SelectCodeGenFolder);
        codeGenAssetPathField = AddCodeGenPathRow(
            "Default SO / 默认SO",
            Excel2SoCodeGenerator.BuildDefaultAssetPath("NewTable"),
            "Save As / 另存",
            SelectCodeGenAssetPath);
        codeGenCsvPathField = AddCodeGenPathRow(
            "CSV Output / CSV输出",
            Excel2SoCodeGenerator.BuildDefaultCsvPath("NewTable"),
            "Save CSV / 保存CSV",
            SelectCodeGenCsvPath);

        lastAutoCodeGenAssetPath = codeGenAssetPathField.value;
        lastAutoCodeGenCsvPath = codeGenCsvPathField.value;

        codeGenNamePrefixField.RegisterValueChangedCallback(_ =>
        {
            RefreshCodeGenDerivedPaths(force: false);
            UpdateActionState();
        });
        codeGenListPropertyField.RegisterValueChangedCallback(_ => UpdateActionState());
        codeGenRuntimeFolderField.RegisterValueChangedCallback(_ => UpdateActionState());
        codeGenEditorFolderField.RegisterValueChangedCallback(_ => UpdateActionState());
        codeGenAssetPathField.RegisterValueChangedCallback(_ => UpdateActionState());
        codeGenCsvPathField.RegisterValueChangedCallback(_ => UpdateActionState());

        AddCodeGenSectionTitle("Fields / 字段");
        var fieldHeader = new VisualElement();
        fieldHeader.AddToClassList("codegen-field-header");
        fieldHeader.Add(CreateCodeGenHeaderLabel("Column / 列名", "codegen-column-header"));
        fieldHeader.Add(CreateCodeGenHeaderLabel("Property / 属性名", "codegen-property-header"));
        fieldHeader.Add(CreateCodeGenHeaderLabel("Type / 类型", "codegen-type-header"));
        fieldHeader.Add(CreateCodeGenHeaderLabel("Enum Type / 枚举类型", "codegen-enum-header"));
        fieldHeader.Add(CreateCodeGenHeaderLabel(string.Empty, "codegen-remove-header"));
        codeGenRoot.Add(fieldHeader);

        codeGenFieldsContainer = new VisualElement();
        codeGenFieldsContainer.AddToClassList("codegen-fields");
        codeGenRoot.Add(codeGenFieldsContainer);

        AddCodeGenFieldRow("id", "id", Excel2SoCodeGenFieldKind.String);

        var addFieldButton = new Button(() =>
        {
            var index = codeGenFieldRows.Count + 1;
            AddCodeGenFieldRow($"field{index}", $"field{index}", Excel2SoCodeGenFieldKind.String);
            UpdateActionState();
        })
        {
            text = "Add Field / 添加字段"
        };
        addFieldButton.AddToClassList("codegen-add-button");
        codeGenRoot.Add(addFieldButton);

        codeGenRoot.style.display = DisplayStyle.None;
    }

    private static Label CreateCodeGenHeaderLabel(string text, string className)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private void AddCodeGenSectionTitle(string title)
    {
        var label = new Label(title);
        label.AddToClassList("codegen-section-title");
        codeGenRoot.Add(label);
    }

    private TextField AddCodeGenTextRow(string labelText, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("codegen-row");

        var label = new Label(labelText);
        label.AddToClassList("codegen-label");
        row.Add(label);

        var field = new TextField();
        field.value = value;
        field.AddToClassList("codegen-text-field");
        row.Add(field);

        codeGenRoot.Add(row);
        return field;
    }

    private TextField AddCodeGenPathRow(
        string labelText,
        string value,
        string buttonText,
        Action<TextField> selectPath)
    {
        var row = new VisualElement();
        row.AddToClassList("codegen-row");

        var label = new Label(labelText);
        label.AddToClassList("codegen-label");
        row.Add(label);

        var field = new TextField();
        field.value = value;
        field.AddToClassList("codegen-text-field");
        row.Add(field);

        var button = new Button(() => selectPath(field))
        {
            text = buttonText
        };
        button.AddToClassList("codegen-path-button");
        row.Add(button);

        codeGenRoot.Add(row);
        return field;
    }

    private void AddCodeGenFieldRow(
        string columnName,
        string propertyName,
        Excel2SoCodeGenFieldKind fieldKind)
    {
        var row = new CodeGenFieldRow();
        row.Root = new VisualElement();
        row.Root.AddToClassList("codegen-field-row");

        row.ColumnField = new TextField();
        row.ColumnField.value = columnName;
        row.ColumnField.AddToClassList("codegen-column-field");
        row.Root.Add(row.ColumnField);

        row.PropertyField = new TextField();
        row.PropertyField.value = propertyName;
        row.PropertyField.AddToClassList("codegen-property-field");
        row.Root.Add(row.PropertyField);

        row.TypeField = new PopupField<Excel2SoCodeGenFieldKind>(string.Empty, codeGenFieldKindOptions, fieldKind);
        row.TypeField.formatListItemCallback = Excel2SoCodeGenTypeRegistry.Format;
        row.TypeField.formatSelectedValueCallback = Excel2SoCodeGenTypeRegistry.Format;
        row.TypeField.AddToClassList("codegen-type-field");
        row.Root.Add(row.TypeField);

        row.EnumTypeField = new TextField();
        row.EnumTypeField.AddToClassList("codegen-enum-field");
        row.EnumTypeField.value = "MyEnum";
        row.Root.Add(row.EnumTypeField);

        row.RemoveButton = new Button(() =>
        {
            codeGenFieldRows.Remove(row);
            row.Root.RemoveFromHierarchy();
            RefreshCodeGenRemoveButtons();
            UpdateActionState();
        })
        {
            text = "-"
        };
        row.RemoveButton.AddToClassList("codegen-remove-button");
        row.Root.Add(row.RemoveButton);

        row.ColumnField.RegisterValueChangedCallback(_ => UpdateActionState());
        row.PropertyField.RegisterValueChangedCallback(_ => UpdateActionState());
        row.EnumTypeField.RegisterValueChangedCallback(_ => UpdateActionState());
        row.TypeField.RegisterValueChangedCallback(_ =>
        {
            row.RefreshOptionalFields();
            UpdateActionState();
        });

        codeGenFieldRows.Add(row);
        codeGenFieldsContainer.Add(row.Root);
        row.RefreshOptionalFields();
        RefreshCodeGenRemoveButtons();
    }

    private void RefreshCodeGenRemoveButtons()
    {
        var canRemove = codeGenFieldRows.Count > 1;
        foreach (var row in codeGenFieldRows)
        {
            row.RemoveButton.SetEnabled(canRemove);
        }
    }

    private void RefreshCodeGenDerivedPaths(bool force)
    {
        if (codeGenNamePrefixField == null || codeGenAssetPathField == null || codeGenCsvPathField == null)
        {
            return;
        }

        var namePrefix = string.IsNullOrWhiteSpace(codeGenNamePrefixField.value)
            ? "NewTable"
            : codeGenNamePrefixField.value.Trim();
        var assetPath = Excel2SoCodeGenerator.BuildDefaultAssetPath(namePrefix);
        var csvPath = Excel2SoCodeGenerator.BuildDefaultCsvPath(namePrefix);

        if (force || string.IsNullOrWhiteSpace(codeGenAssetPathField.value)
            || string.Equals(codeGenAssetPathField.value, lastAutoCodeGenAssetPath, StringComparison.OrdinalIgnoreCase))
        {
            codeGenAssetPathField.value = assetPath;
        }

        if (force || string.IsNullOrWhiteSpace(codeGenCsvPathField.value)
            || string.Equals(codeGenCsvPathField.value, lastAutoCodeGenCsvPath, StringComparison.OrdinalIgnoreCase))
        {
            codeGenCsvPathField.value = csvPath;
        }

        lastAutoCodeGenAssetPath = assetPath;
        lastAutoCodeGenCsvPath = csvPath;
    }

    private void SelectCodeGenFolder(TextField field)
    {
        var directory = GetExistingDirectory(field.value, Application.dataPath);
        var path = EditorUtility.OpenFolderPanel("Select Folder / 选择目录", directory, string.Empty);
        if (!string.IsNullOrEmpty(path))
        {
            field.value = NormalizeAssetPath(path);
        }
    }

    private void SelectCodeGenAssetPath(TextField field)
    {
        var defaultPath = NormalizeAssetPath(field.value);
        var defaultName = Path.GetFileNameWithoutExtension(defaultPath);
        if (string.IsNullOrWhiteSpace(defaultName))
        {
            defaultName = $"{codeGenNamePrefixField.value.Trim()}Database";
        }

        var path = EditorUtility.SaveFilePanelInProject(
            "Select Default SO Asset / 选择默认 SO 资产",
            defaultName,
            "asset",
            "Choose the default ScriptableObject asset path used by the generated importer. / 选择生成导入器使用的默认 ScriptableObject 资产路径。"
        );

        if (!string.IsNullOrEmpty(path))
        {
            field.value = path;
        }
    }

    private void SelectCodeGenCsvPath(TextField field)
    {
        var directory = GetExistingDirectory(field.value, Application.dataPath);
        var defaultName = Path.GetFileNameWithoutExtension(field.value);
        if (string.IsNullOrWhiteSpace(defaultName))
        {
            defaultName = $"{codeGenNamePrefixField.value.Trim()}Database";
        }

        var path = EditorUtility.SaveFilePanel(
            "Select Empty CSV Output / 选择空 CSV 输出",
            directory,
            defaultName,
            "csv"
        );

        if (!string.IsNullOrEmpty(path))
        {
            field.value = NormalizeAssetPath(path);
        }
    }

    private void SelectTablePath()
    {
        var directory = GetExistingDirectory(tablePathField.value, Application.dataPath);
        var title = selectedMode == OperationMode.Excel2SO
            ? "Select Excel2SO Table / 选择 Excel2SO 表格"
            : "Select Source ScriptableObject / 选择源 ScriptableObject";
        var extensions = selectedMode == OperationMode.Excel2SO ? "xlsx,csv" : "asset";
        var path = EditorUtility.OpenFilePanel(title, directory, extensions);
        if (!string.IsNullOrEmpty(path))
        {
            tablePathField.value = selectedMode == OperationMode.Excel2SO ? path : NormalizeAssetPath(path);
        }
    }

    private void SelectTargetPath()
    {
        if (selectedMode == OperationMode.SO2Table)
        {
            SelectCsvOutputPath();
            return;
        }

        var targetPath = NormalizeAssetPath(targetPathField.value);
        var defaultName = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(defaultName))
        {
            defaultName = selectedImporter == null ? "ImportedData" : selectedImporter.Type.Name;
        }

        var path = EditorUtility.SaveFilePanelInProject(
            "Select Target Asset / 选择目标资产",
            defaultName,
            "asset",
            "Choose where to save the imported ScriptableObject asset. / 选择导入后的 ScriptableObject 资产保存位置。"
        );

        if (!string.IsNullOrEmpty(path))
        {
            targetPathField.value = path;
        }
    }

    private void SelectCsvOutputPath()
    {
        var directory = GetExistingDirectory(targetPathField.value, Application.dataPath);
        var defaultName = Path.GetFileNameWithoutExtension(targetPathField.value);
        if (string.IsNullOrWhiteSpace(defaultName))
        {
            defaultName = selectedImporter == null ? "ExportedTable" : selectedImporter.Type.Name;
        }

        var path = EditorUtility.SaveFilePanel(
            "Select CSV Output / 选择 CSV 输出",
            directory,
            defaultName,
            "csv"
        );

        if (!string.IsNullOrEmpty(path))
        {
            targetPathField.value = NormalizeAssetPath(path);
        }
    }

    private void ResetTargetPath()
    {
        if (selectedImporter == null)
        {
            return;
        }

        if (selectedMode == OperationMode.Excel2SO)
        {
            targetPathField.value = selectedImporter.CreateImporter().DefaultTargetAssetPath;
            return;
        }

        tablePathField.value = selectedImporter.CreateImporter().DefaultTargetAssetPath;
    }

    private void ResetPathsForMode()
    {
        if (selectedMode == OperationMode.CodeGen)
        {
            RefreshCodeGenDerivedPaths(force: false);
            return;
        }

        if (selectedImporter == null || tablePathField == null || targetPathField == null)
        {
            return;
        }

        if (selectedMode == OperationMode.Excel2SO)
        {
            targetPathField.value = selectedImporter.CreateImporter().DefaultTargetAssetPath;
            return;
        }

        tablePathField.value = selectedImporter.CreateImporter().DefaultTargetAssetPath;
        targetPathField.value = string.Empty;
    }

    private void ApplyModeUi(bool resetPaths)
    {
        if (tablePathLabel == null || targetPathLabel == null || importButton == null)
        {
            return;
        }

        var isImportMode = selectedMode == OperationMode.Excel2SO;
        var isCodeGenMode = selectedMode == OperationMode.CodeGen;
        importerRow.style.display = isCodeGenMode ? DisplayStyle.None : DisplayStyle.Flex;
        tablePathRow.style.display = isCodeGenMode ? DisplayStyle.None : DisplayStyle.Flex;
        targetPathRow.style.display = isCodeGenMode ? DisplayStyle.None : DisplayStyle.Flex;
        codeGenRoot.style.display = isCodeGenMode ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshModeSidebar();

        tablePathLabel.text = isImportMode ? "Table / 表格" : "Source SO / 源SO";
        targetPathLabel.text = isImportMode ? "Target / 目标" : "CSV Output / CSV输出";
        targetBrowseButton.text = isImportMode ? "Save As / 另存" : "Save CSV / 保存CSV";
        targetDefaultButton.style.display = isImportMode ? DisplayStyle.Flex : DisplayStyle.None;
        importButton.text = isCodeGenMode ? "Generate / 生成" : isImportMode ? "Import / 导入" : "Export CSV / 导出CSV";

        if (resetPaths)
        {
            ResetPathsForMode();
        }

        UpdateActionState();
    }

    private void ExecuteSelected()
    {
        if (selectedMode == OperationMode.CodeGen)
        {
            GenerateSelected();
            return;
        }

        if (selectedMode == OperationMode.SO2Table)
        {
            ExportSelected();
            return;
        }

        ImportSelected();
    }

    private void GenerateSelected()
    {
        if (!TryGetValidatedCodeGenOptions(out var options, out var validationMessage))
        {
            ShowStatus(validationMessage);
            return;
        }

        try
        {
            var report = Excel2SoCodeGenerator.Generate(options);
            ShowReport(report);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowStatus($"Code generation failed / 代码生成失败: {ex.Message}");
        }
    }

    private void ImportSelected()
    {
        if (!TryGetValidatedImportInput(out var tablePath, out var targetPath, out var validationMessage))
        {
            ShowStatus(validationMessage);
            return;
        }

        try
        {
            var importer = selectedImporter.CreateImporter();
            var report = importer.Import(tablePath, targetPath);
            ShowReport(report);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowStatus($"Import failed / 导入失败: {ex.Message}");
        }
    }

    private void ExportSelected()
    {
        if (!TryGetValidatedExportInput(out var sourcePath, out var csvPath, out var validationMessage))
        {
            ShowStatus(validationMessage);
            return;
        }

        try
        {
            var importer = selectedImporter.CreateImporter();
            var report = importer.Export(sourcePath, csvPath);
            ShowReport(report);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowStatus($"Export failed / 导出失败: {ex.Message}");
        }
    }

    private void UpdateActionState()
    {
        if (importButton == null)
        {
            return;
        }

        bool isValid;
        string message;
        if (selectedMode == OperationMode.Excel2SO)
        {
            isValid = TryGetValidatedImportInput(out _, out _, out message);
        }
        else if (selectedMode == OperationMode.SO2Table)
        {
            isValid = TryGetValidatedExportInput(out _, out _, out message);
        }
        else
        {
            isValid = TryGetValidatedCodeGenOptions(out _, out message);
        }

        importButton.SetEnabled(isValid);
        ShowStatus(isValid
            ? selectedMode == OperationMode.Excel2SO
                ? "Ready to import. / 准备导入。"
                : selectedMode == OperationMode.SO2Table
                    ? "Ready to export. / 准备导出。"
                    : "Ready to generate. / 准备生成。"
            : message);
    }

    private bool TryGetValidatedImportInput(out string tablePath, out string targetPath, out string message)
    {
        tablePath = string.Empty;
        targetPath = string.Empty;
        message = string.Empty;

        if (selectedImporter == null)
        {
            message = "Select an importer. / 请选择导入器。";
            return false;
        }

        var rawTablePath = NormalizePath(tablePathField == null ? string.Empty : tablePathField.value);
        if (string.IsNullOrWhiteSpace(rawTablePath))
        {
            message = "Choose a .xlsx or .csv table file. / 请选择 .xlsx 或 .csv 表格文件。";
            return false;
        }

        var resolvedTablePath = ResolveProjectPath(rawTablePath);
        if (!File.Exists(resolvedTablePath))
        {
            message = $"Table file does not exist / 表格文件不存在: {rawTablePath}";
            return false;
        }

        var extension = Path.GetExtension(resolvedTablePath);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            message = "Table file must be .xlsx or .csv. / 表格文件必须是 .xlsx 或 .csv。";
            return false;
        }

        var rawTargetPath = NormalizeAssetPath(targetPathField == null ? string.Empty : targetPathField.value);
        if (string.IsNullOrWhiteSpace(rawTargetPath))
        {
            message = "Choose a target .asset path. / 请选择目标 .asset 路径。";
            return false;
        }

        if (!rawTargetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            message = $"Target asset path must be under Assets/ / 目标资产路径必须在 Assets/ 下: {rawTargetPath}";
            return false;
        }

        if (!rawTargetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            message = "Target asset path must end with .asset. / 目标资产路径必须以 .asset 结尾。";
            return false;
        }

        tablePath = resolvedTablePath;
        targetPath = rawTargetPath;
        return true;
    }

    private bool TryGetValidatedExportInput(out string sourcePath, out string csvPath, out string message)
    {
        sourcePath = string.Empty;
        csvPath = string.Empty;
        message = string.Empty;

        if (selectedImporter == null)
        {
            message = "Select an importer. / 请选择导入器。";
            return false;
        }

        var rawSourcePath = NormalizeAssetPath(tablePathField == null ? string.Empty : tablePathField.value);
        if (string.IsNullOrWhiteSpace(rawSourcePath))
        {
            message = "Choose a source .asset file. / 请选择源 .asset 文件。";
            return false;
        }

        if (!rawSourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            message = $"Source SO path must be under Assets/ / 源 SO 路径必须在 Assets/ 下: {rawSourcePath}";
            return false;
        }

        if (!rawSourcePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            message = "Source SO path must end with .asset. / 源 SO 路径必须以 .asset 结尾。";
            return false;
        }

        var resolvedSourcePath = ResolveProjectPath(rawSourcePath);
        if (!File.Exists(resolvedSourcePath))
        {
            message = $"Source SO does not exist / 源 SO 不存在: {rawSourcePath}";
            return false;
        }

        var rawCsvPath = NormalizeAssetPath(targetPathField == null ? string.Empty : targetPathField.value);
        if (string.IsNullOrWhiteSpace(rawCsvPath))
        {
            message = "Choose a CSV output path. / 请选择 CSV 输出路径。";
            return false;
        }

        var resolvedCsvPath = ResolveProjectPath(rawCsvPath);
        if (!Path.IsPathRooted(resolvedCsvPath)
            && !rawCsvPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            message = "CSV output path must be under Assets/ or an absolute path. / CSV 输出路径必须在 Assets/ 下或使用绝对路径。";
            return false;
        }

        if (!string.Equals(Path.GetExtension(resolvedCsvPath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            message = "CSV output path must end with .csv. / CSV 输出路径必须以 .csv 结尾。";
            return false;
        }

        sourcePath = rawSourcePath;
        csvPath = rawCsvPath;
        return true;
    }

    private bool TryGetValidatedCodeGenOptions(out Excel2SoCodeGenOptions options, out string message)
    {
        options = null;
        message = string.Empty;

        if (codeGenNamePrefixField == null
            || codeGenListPropertyField == null
            || codeGenRuntimeFolderField == null
            || codeGenEditorFolderField == null
            || codeGenAssetPathField == null
            || codeGenCsvPathField == null)
        {
            message = "CodeGen UI is not ready. / 代码生成界面尚未准备好。";
            return false;
        }

        return Excel2SoCodeGenerator.TryCreateOptions(
            codeGenNamePrefixField.value,
            codeGenListPropertyField.value,
            codeGenRuntimeFolderField.value,
            codeGenEditorFolderField.value,
            codeGenAssetPathField.value,
            codeGenCsvPathField.value,
            codeGenFieldRows.Select(row => row.ToField()),
            out options,
            out message);
    }

    private void ShowReport(Excel2SoImportReport report)
    {
        if (report == null)
        {
            ShowStatus("Import finished without a report. / 导入完成但没有报告。");
            return;
        }

        ShowStatus(
            $"{report}\n" +
            $"Table / 表格: {report.TablePath}\n" +
            $"Target / 目标: {report.TargetPath}");
    }

    private void ShowReport(Excel2SoExportReport report)
    {
        if (report == null)
        {
            ShowStatus("Export finished without a report. / 导出完成但没有报告。");
            return;
        }

        ShowStatus(
            $"{report}\n" +
            $"Source / 源SO: {report.SourcePath}\n" +
            $"CSV / CSV文件: {report.CsvPath}");
    }

    private void ShowReport(Excel2SoCodeGenReport report)
    {
        if (report == null)
        {
            ShowStatus("Generation finished without a report. / 生成完成但没有报告。");
            return;
        }

        ShowStatus(report.WrittenPaths.Count == 0
            ? report.ToString()
            : $"{report}\n" +
              $"Files / 文件:\n{string.Join("\n", report.WrittenPaths)}");
    }

    private void ShowStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }

    private static string GetExistingDirectory(string path, string fallback)
    {
        var normalizedPath = ResolveProjectPath(NormalizePath(path));
        if (File.Exists(normalizedPath))
        {
            return Path.GetDirectoryName(normalizedPath);
        }

        if (Directory.Exists(normalizedPath))
        {
            return normalizedPath;
        }

        return fallback;
    }

    private static string ResolveProjectPath(string path)
    {
        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? path
                : NormalizePath(Path.Combine(projectRoot, path));
        }

        return path;
    }

    private static string NormalizeAssetPath(string path)
    {
        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
        {
            var dataPath = NormalizePath(Application.dataPath);
            if (path.Equals(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (path.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }
        }

        return path;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
    }

    private enum OperationMode
    {
        Excel2SO,
        SO2Table,
        CodeGen
    }

    private sealed class CodeGenFieldRow
    {
        public VisualElement Root { get; set; }

        public TextField ColumnField { get; set; }

        public TextField PropertyField { get; set; }

        public PopupField<Excel2SoCodeGenFieldKind> TypeField { get; set; }

        public TextField EnumTypeField { get; set; }

        public Button RemoveButton { get; set; }

        public void RefreshOptionalFields()
        {
            var definition = Excel2SoCodeGenTypeRegistry.Get(TypeField.value);
            EnumTypeField.style.display = definition.RequiresEnumType ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public Excel2SoCodeGenField ToField()
        {
            return new Excel2SoCodeGenField
            {
                ColumnName = ColumnField.value,
                PropertyName = PropertyField.value,
                Kind = TypeField.value,
                EnumTypeName = EnumTypeField.value
            };
        }
    }

    private sealed class ImporterOption
    {
        public ImporterOption(Type type)
        {
            Type = type;
            DisplayName = string.IsNullOrEmpty(type.Namespace) ? type.Name : type.FullName;
        }

        public Type Type { get; }

        public string DisplayName { get; }

        public IExcel2SoListAssetImporter CreateImporter()
        {
            return (IExcel2SoListAssetImporter)Activator.CreateInstance(Type);
        }
    }
}
