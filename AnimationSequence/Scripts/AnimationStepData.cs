using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Animation
{
    /// <summary>
    /// 单个动画步骤的数据配置, 只保存播放需要的数据, 不包含具体 Tween 构建逻辑.
    /// </summary>
    [Serializable]
    public class AnimationStepData
    {
        private const string SelfTargetPath = ".";

        [Header("目标")]
        [SerializeField] private GameObject target;
        [SerializeField] private string targetPath;
        [SerializeField] private AnimationStartupActiveState startupActiveState = AnimationStartupActiveState.KeepCurrent;

        [Header("基础")]
        [SerializeField] private AnimationEffectType effectType = AnimationEffectType.FadeIn;
        [SerializeField, Min(0f)] private float duration = 0.3f;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField] private Ease ease = Ease.OutQuad;

        [Header("Fade")]
        [SerializeField] private bool autoAddCanvasGroup = true;

        [Header("Move")]
        [SerializeField] private Vector3 slideOffset = new Vector3(0f, -80f, 0f);
        [SerializeField] private Vector3 moveOffset = new Vector3(0f, 80f, 0f);

        [Header("Shake")]
        [SerializeField] private Vector3 shakeStrength = new Vector3(12f, 12f, 0f);
        [SerializeField, Min(0)] private int shakeVibrato = 20;
        [SerializeField, Range(0f, 180f)] private float shakeRandomness = 90f;

        [Header("Scale")]
        [SerializeField] private Vector3 scaleFromMultiplier = new Vector3(0.8f, 0.8f, 0.8f);
        [SerializeField] private Vector3 scaleToMultiplier = Vector3.zero;

        [Header("Rotate")]
        [SerializeField] private Vector3 rotationEuler = new Vector3(0f, 0f, 360f);

        public GameObject Target => target;
        public string TargetPath => targetPath;
        public AnimationStartupActiveState StartupActiveState => startupActiveState;
        public AnimationEffectType EffectType => effectType;
        public float Duration => duration;
        public float Delay => delay;
        public Ease Ease => ease;
        public bool AutoAddCanvasGroup => autoAddCanvasGroup;
        public Vector3 SlideOffset => slideOffset;
        public Vector3 MoveOffset => moveOffset;
        public Vector3 ShakeStrength => shakeStrength;
        public int ShakeVibrato => shakeVibrato;
        public float ShakeRandomness => shakeRandomness;
        public Vector3 ScaleFromMultiplier => scaleFromMultiplier;
        public Vector3 ScaleToMultiplier => scaleToMultiplier;
        public Vector3 RotationEuler => rotationEuler;

        public AnimationStepData()
        {
        }

        public AnimationStepData(GameObject target, AnimationEffectType effectType, Transform root = null)
        {
            this.effectType = effectType;
            SetTarget(target, root);
        }

        /// <summary>
        /// 设置目标时同步记录层级路径, 用于 SO 无法持久化场景对象引用时的运行时解析.
        /// </summary>
        public void SetTarget(GameObject newTarget, Transform root = null)
        {
            target = newTarget;
            targetPath = newTarget != null ? BuildTargetPath(newTarget.transform, root) : string.Empty;
        }

        /// <summary>
        /// 优先使用直接引用, 为空时使用绑定根节点或场景全路径查找.
        /// </summary>
        public GameObject ResolveTarget(Transform bindingRoot)
        {
            if (target != null) return target;
            if (string.IsNullOrEmpty(targetPath)) return null;

            if (targetPath == SelfTargetPath)
            {
                return bindingRoot != null ? bindingRoot.gameObject : null;
            }

            if (bindingRoot != null)
            {
                var child = bindingRoot.Find(targetPath);
                if (child != null) return child.gameObject;
            }

            return FindInLoadedScenes(targetPath);
        }

        /// <summary>
        /// 构建相对 root 的层级路径, root 为空时返回场景根开始的完整路径.
        /// </summary>
        public static string BuildTargetPath(Transform targetTransform, Transform root = null)
        {
            if (targetTransform == null) return string.Empty;
            if (root != null && targetTransform == root) return SelfTargetPath;

            var names = new List<string>();
            var current = targetTransform;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            if (root != null && current != root) return string.Empty;

            names.Reverse();
            return string.Join("/", names);
        }

        private static GameObject FindInLoadedScenes(string path)
        {
            var names = path.Split('/');
            if (names.Length == 0) return null;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name != names[0]) continue;
                    if (names.Length == 1) return root;

                    var child = FindChildByPath(root.transform, names, 1);
                    if (child != null) return child.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildByPath(Transform root, IReadOnlyList<string> names, int index)
        {
            var current = root;
            for (var i = index; i < names.Count; i++)
            {
                current = FindDirectChild(current, names[i]);
                if (current == null) return null;
            }

            return current;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName) return child;
            }

            return null;
        }
    }
}
