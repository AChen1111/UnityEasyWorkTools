using System;
using Game.Animation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEasyWorkTools.Editor;

namespace Game.Editor.Animation
{
    /// <summary>
    /// 动画序列编辑窗口, 使用 UI Toolkit 编辑 ScriptableObject 数据, 不写具体播放逻辑.
    /// </summary>
    public class AnimationSequenceEditorWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/UnityEasyWorkTools/AnimationSequence/Editor/UI/AnimationSequenceEditorWindow.uxml";
        private const string StylePath = "Assets/UnityEasyWorkTools/AnimationSequence/Editor/UI/AnimationSequenceEditorWindow.uss";
        private const string StepsPropertyName = "steps";

        private AnimationSequenceAsset currentAsset;
        private SerializedObject serializedAsset;
        private ObjectField currentAssetField;
        private TextField newAssetNameField;
        private ObjectField targetPathRootField;
        private EnumField addEffectTypeField;
        private VisualElement editorContent;
        private Label emptyAssetHelp;
        private Label stepsCountLabel;
        private ScrollView stepsScroll;
        private Transform targetPathRoot;
        private AnimationEffectType addEffectType = AnimationEffectType.FadeIn;

        [MenuItem("Tools/UnityEasyWorkTools/Animation Sequence Editor")]
        public static void Open()
        {
            GetWindow<AnimationSequenceEditorWindow>("Animation Sequence");
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            LoadLayout();
            BindToolbar();
            RefreshAssetState();
        }

        private void LoadLayout()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (visualTree == null || styleSheet == null)
            {
                rootVisualElement.Add(new Label($"AnimationSequence UI 资源缺失: {LayoutPath}."));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void BindToolbar()
        {
            currentAssetField = rootVisualElement.Q<ObjectField>("current-asset-field");
            newAssetNameField = rootVisualElement.Q<TextField>("new-asset-name-field");
            targetPathRootField = rootVisualElement.Q<ObjectField>("target-path-root-field");
            editorContent = rootVisualElement.Q<VisualElement>("editor-content");
            emptyAssetHelp = rootVisualElement.Q<Label>("empty-asset-help");
            stepsCountLabel = rootVisualElement.Q<Label>("steps-count-label");
            stepsScroll = rootVisualElement.Q<ScrollView>("steps-scroll");

            currentAssetField.objectType = typeof(AnimationSequenceAsset);
            currentAssetField.allowSceneObjects = false;
            currentAssetField.RegisterValueChangedCallback(evt =>
            {
                currentAsset = evt.newValue as AnimationSequenceAsset;
                serializedAsset = currentAsset != null ? new SerializedObject(currentAsset) : null;
                RefreshAssetState();
            });

            newAssetNameField.value = "NewAnimationSequence";

            targetPathRootField.objectType = typeof(Transform);
            targetPathRootField.allowSceneObjects = true;
            targetPathRootField.RegisterValueChangedCallback(evt =>
            {
                targetPathRoot = evt.newValue as Transform;
            });

            var effectContainer = rootVisualElement.Q<VisualElement>("effect-type-container");
            addEffectTypeField = new EnumField("添加效果", addEffectType);
            addEffectTypeField.RegisterValueChangedCallback(evt => addEffectType = (AnimationEffectType)evt.newValue);
            effectContainer.Add(addEffectTypeField);

            rootVisualElement.Q<Button>("create-asset-button").clicked += CreateNewAsset;
            rootVisualElement.Q<Button>("save-asset-button").clicked += SaveCurrentAsset;
            rootVisualElement.Q<Button>("ping-asset-button").clicked += PingCurrentAsset;
            rootVisualElement.Q<Button>("add-empty-step-button").clicked += () => AddStep(null, addEffectType);
            rootVisualElement.Q<Button>("add-selection-button").clicked += AddStepsFromSelection;
        }

        private void RefreshAssetState()
        {
            var hasAsset = currentAsset != null;
            editorContent.style.display = hasAsset ? DisplayStyle.Flex : DisplayStyle.None;
            emptyAssetHelp.style.display = hasAsset ? DisplayStyle.None : DisplayStyle.Flex;

            if (!hasAsset)
            {
                if (stepsScroll != null)
                {
                    stepsScroll.Clear();
                }

                return;
            }

            EnsureSerializedAsset();
            RefreshSteps();
        }

        private void RefreshSteps()
        {
            if (serializedAsset == null || stepsScroll == null)
            {
                return;
            }

            serializedAsset.Update();
            stepsScroll.Clear();
            var steps = serializedAsset.FindProperty(StepsPropertyName);
            stepsCountLabel.text = $"动画步骤: {steps.arraySize}";

            for (var i = 0; i < steps.arraySize; i++)
            {
                stepsScroll.Add(CreateStepCard(steps, i));
            }
        }

        private VisualElement CreateStepCard(SerializedProperty steps, int index)
        {
            var step = steps.GetArrayElementAtIndex(index);
            var card = new VisualElement();
            card.AddToClassList("uewt-step-card");

            var header = new VisualElement();
            header.AddToClassList("uewt-step-header");
            var title = new Label($"Step {index + 1}");
            title.AddToClassList("uewt-step-title");
            header.Add(title);

            header.Add(CreateStepButton("上移", index > 0, () => MoveStep(index, index - 1)));
            header.Add(CreateStepButton("下移", index < steps.arraySize - 1, () => MoveStep(index, index + 1)));
            header.Add(CreateStepButton("删除", true, () => DeleteStep(index)));
            card.Add(header);

            AddTargetField(card, step);
            AddPropertyField(card, step.FindPropertyRelative("targetPath"), "目标路径");
            AddPropertyField(card, step.FindPropertyRelative("startupActiveState"), "开始激活状态");
            AddEffectTypeField(card, step);
            AddPropertyField(card, step.FindPropertyRelative("duration"), "持续时间");
            AddPropertyField(card, step.FindPropertyRelative("delay"), "延迟");
            AddPropertyField(card, step.FindPropertyRelative("ease"), "Ease");

            var paramContainer = new VisualElement();
            paramContainer.AddToClassList("uewt-param-container");
            DrawEffectParams(paramContainer, step, ReadEffectType(step));
            card.Add(paramContainer);

            card.Bind(serializedAsset);
            return card;
        }

        private Button CreateStepButton(string text, bool enabled, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("uewt-button");
            button.SetEnabled(enabled);
            return button;
        }

        private void AddTargetField(VisualElement parent, SerializedProperty step)
        {
            var targetProperty = step.FindPropertyRelative("target");
            var targetPathProperty = step.FindPropertyRelative("targetPath");
            var targetField = new ObjectField("目标物体")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                value = targetProperty.objectReferenceValue
            };

            targetField.RegisterValueChangedCallback(evt =>
            {
                serializedAsset.Update();
                var target = evt.newValue as GameObject;
                targetProperty.objectReferenceValue = target;
                targetPathProperty.stringValue = target != null
                    ? AnimationStepData.BuildTargetPath(target.transform, targetPathRoot)
                    : string.Empty;
                serializedAsset.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentAsset);
                RefreshSteps();
            });
            parent.Add(targetField);
        }

        private void AddEffectTypeField(VisualElement parent, SerializedProperty step)
        {
            var effectTypeProperty = step.FindPropertyRelative("effectType");
            var effectTypeField = new EnumField("效果类型", ReadEffectType(step));
            effectTypeField.RegisterValueChangedCallback(evt =>
            {
                serializedAsset.Update();
                effectTypeProperty.enumValueIndex = EnumIndexOf(typeof(AnimationEffectType), evt.newValue);
                serializedAsset.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentAsset);
                RefreshSteps();
            });
            parent.Add(effectTypeField);
        }

        private static void AddPropertyField(VisualElement parent, SerializedProperty property, string label)
        {
            parent.Add(new PropertyField(property, label));
        }

        private void DrawEffectParams(VisualElement parent, SerializedProperty step, AnimationEffectType effectType)
        {
            switch (effectType)
            {
                case AnimationEffectType.FadeIn:
                case AnimationEffectType.FadeOut:
                    AddPropertyField(parent, step.FindPropertyRelative("autoAddCanvasGroup"), "自动添加 CanvasGroup");
                    break;
                case AnimationEffectType.SlideUp:
                    AddPropertyField(parent, step.FindPropertyRelative("slideOffset"), "起始偏移");
                    break;
                case AnimationEffectType.Shake:
                    AddPropertyField(parent, step.FindPropertyRelative("shakeStrength"), "抖动强度");
                    AddPropertyField(parent, step.FindPropertyRelative("shakeVibrato"), "震动次数");
                    AddPropertyField(parent, step.FindPropertyRelative("shakeRandomness"), "随机角度");
                    break;
                case AnimationEffectType.ScaleIn:
                    AddPropertyField(parent, step.FindPropertyRelative("scaleFromMultiplier"), "起始缩放倍率");
                    break;
                case AnimationEffectType.ScaleOut:
                    AddPropertyField(parent, step.FindPropertyRelative("scaleToMultiplier"), "目标缩放倍率");
                    break;
                case AnimationEffectType.MoveTo:
                    AddPropertyField(parent, step.FindPropertyRelative("moveOffset"), "相对位移");
                    break;
                case AnimationEffectType.Rotate:
                    AddPropertyField(parent, step.FindPropertyRelative("rotationEuler"), "旋转角度");
                    break;
            }
        }

        private void MoveStep(int from, int to)
        {
            serializedAsset.Update();
            var steps = serializedAsset.FindProperty(StepsPropertyName);
            steps.MoveArrayElement(from, to);
            serializedAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAsset);
            RefreshSteps();
        }

        private void DeleteStep(int index)
        {
            serializedAsset.Update();
            var steps = serializedAsset.FindProperty(StepsPropertyName);
            steps.DeleteArrayElementAtIndex(index);
            serializedAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAsset);
            RefreshSteps();
        }

        private void AddStep(GameObject target, AnimationEffectType effectType)
        {
            if (currentAsset == null)
            {
                return;
            }

            Undo.RecordObject(currentAsset, "Add Animation Step");
            currentAsset.AddStep(new AnimationStepData(target, effectType, targetPathRoot));
            EditorUtility.SetDirty(currentAsset);
            EnsureSerializedAsset();
            RefreshSteps();
        }

        private void AddStepsFromSelection()
        {
            if (currentAsset == null)
            {
                return;
            }

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Animation Sequence", "当前没有选中的 GameObject.", "OK");
                return;
            }

            Array.Sort(selectedObjects, CompareSelectedObjects);
            Undo.RecordObject(currentAsset, "Batch Add Animation Steps");
            foreach (var selected in selectedObjects)
            {
                currentAsset.AddStep(new AnimationStepData(selected, addEffectType, targetPathRoot));
            }

            EditorUtility.SetDirty(currentAsset);
            EnsureSerializedAsset();
            RefreshSteps();
        }

        private int CompareSelectedObjects(GameObject left, GameObject right)
        {
            var leftPath = left != null ? AnimationStepData.BuildTargetPath(left.transform, targetPathRoot) : string.Empty;
            var rightPath = right != null ? AnimationStepData.BuildTargetPath(right.transform, targetPathRoot) : string.Empty;
            return string.Compare(leftPath, rightPath, StringComparison.Ordinal);
        }

        private void CreateNewAsset()
        {
            var pathSettings = UnityEasyWorkToolsPathSettings.GetOrCreate();
            UnityEasyWorkToolsPathSettings.EnsureAssetFolder(pathSettings.AnimationSequenceAssetFolder);

            var asset = CreateInstance<AnimationSequenceAsset>();
            var assetName = string.IsNullOrWhiteSpace(newAssetNameField.value) ? "NewAnimationSequence" : newAssetNameField.value.Trim();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{pathSettings.AnimationSequenceAssetFolder}/{assetName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            currentAsset = asset;
            serializedAsset = new SerializedObject(currentAsset);
            currentAssetField.SetValueWithoutNotify(currentAsset);
            Selection.activeObject = currentAsset;
            EditorGUIUtility.PingObject(currentAsset);
            RefreshAssetState();
        }

        private void SaveCurrentAsset()
        {
            if (currentAsset == null)
            {
                return;
            }

            serializedAsset?.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void PingCurrentAsset()
        {
            if (currentAsset == null)
            {
                return;
            }

            Selection.activeObject = currentAsset;
            EditorGUIUtility.PingObject(currentAsset);
        }

        private void EnsureSerializedAsset()
        {
            if (currentAsset != null && (serializedAsset == null || serializedAsset.targetObject != currentAsset))
            {
                serializedAsset = new SerializedObject(currentAsset);
            }
        }

        private static AnimationEffectType ReadEffectType(SerializedProperty step)
        {
            return (AnimationEffectType)step.FindPropertyRelative("effectType").enumValueIndex;
        }

        private static int EnumIndexOf(Type enumType, Enum value)
        {
            var values = Enum.GetValues(enumType);
            for (var i = 0; i < values.Length; i++)
            {
                if (Equals(values.GetValue(i), value))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
