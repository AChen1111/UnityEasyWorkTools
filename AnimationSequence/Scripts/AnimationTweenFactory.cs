using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// 根据动画步骤创建 DOTween Tween, 运行时和编辑器都不在这里之外写具体动画逻辑.
    /// </summary>
    public static class AnimationTweenFactory
    {
        public static Tween CreateTween(AnimationStepData step, GameObject target, AnimationSequenceState state, TweenCallback beforeEffect = null)
        {
            if (step == null || target == null || state == null) return null;

            var effectTween = CreateEffectTween(step, target, state);
            if (effectTween == null) return null;
            effectTween.Pause();

            var sequence = DOTween.Sequence();
            sequence.SetTarget(target);
            sequence.Pause();

            if (step.Delay > 0f)
            {
                sequence.AppendInterval(step.Delay);
            }

            if (beforeEffect != null)
            {
                sequence.AppendCallback(beforeEffect);
            }

            sequence.Append(effectTween);
            return sequence;
        }

        private static Tween CreateEffectTween(AnimationStepData step, GameObject target, AnimationSequenceState state)
        {
            switch (step.EffectType)
            {
                case AnimationEffectType.FadeIn:
                    return CreateFadeTween(step, target, 0f, 1f);
                case AnimationEffectType.FadeOut:
                    return CreateFadeTween(step, target, 1f, 0f);
                case AnimationEffectType.SlideUp:
                    return CreateSlideTween(step, target, state);
                case AnimationEffectType.Shake:
                    return CreateShakeTween(step, target);
                case AnimationEffectType.ScaleIn:
                    return CreateScaleInTween(step, target, state);
                case AnimationEffectType.ScaleOut:
                    return CreateScaleOutTween(step, target, state);
                case AnimationEffectType.MoveTo:
                    return CreateMoveToTween(step, target);
                case AnimationEffectType.Rotate:
                    return CreateRotateTween(step, target);
                default:
                    return null;
            }
        }

        private static Tween CreateFadeTween(AnimationStepData step, GameObject target, float fromAlpha, float toAlpha)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null && step.AutoAddCanvasGroup)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                Debug.LogError($"[AnimationTweenFactory] {target.name} 缺少 CanvasGroup, 无法播放 Fade 动画.", target);
                return null;
            }

            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () => canvasGroup.alpha = fromAlpha,
                value => canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, value)
            );
        }

        private static Tween CreateSlideTween(AnimationStepData step, GameObject target, AnimationSequenceState state)
        {
            var snapshot = state.GetOrCapture(target);
            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var original = snapshot.AnchoredPosition3D;
                var start = original + step.SlideOffset;
                return CreateProgressTween(
                    target,
                    step.Duration,
                    step.Ease,
                    () => rectTransform.anchoredPosition3D = start,
                    value => rectTransform.anchoredPosition3D = Vector3.LerpUnclamped(start, original, value)
                );
            }

            var transform = target.transform;
            var localOriginal = snapshot.LocalPosition;
            var localStart = localOriginal + step.SlideOffset;
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () => transform.localPosition = localStart,
                value => transform.localPosition = Vector3.LerpUnclamped(localStart, localOriginal, value)
            );
        }

        private static Tween CreateShakeTween(AnimationStepData step, GameObject target)
        {
            if (step.Duration <= 0f)
            {
                return CreateCallbackTween(target, null);
            }

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                return CreateRectTransformShakeTween(step, target, rectTransform);
            }

            return target.transform
                .DOShakePosition(step.Duration, step.ShakeStrength, step.ShakeVibrato, step.ShakeRandomness, false, true)
                .SetEase(step.Ease)
                .SetTarget(target);
        }

        private static Tween CreateRectTransformShakeTween(AnimationStepData step, GameObject target, RectTransform rectTransform)
        {
            Vector3 origin = default;
            var vibrato = Mathf.Max(1, step.ShakeVibrato);
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () => origin = rectTransform.anchoredPosition3D,
                value =>
                {
                    if (value >= 1f)
                    {
                        rectTransform.anchoredPosition3D = origin;
                        return;
                    }

                    // UI 抖动使用 anchoredPosition3D, 避免 RectTransform 被世界坐标 Tween 改写.
                    var scaled = value * vibrato;
                    var index = Mathf.FloorToInt(scaled);
                    var lerp = scaled - index;
                    var current = GetShakeOffset(index, step.ShakeStrength, step.ShakeRandomness);
                    var next = GetShakeOffset(index + 1, step.ShakeStrength, step.ShakeRandomness);
                    rectTransform.anchoredPosition3D = origin + Vector3.LerpUnclamped(current, next, lerp) * (1f - value);
                }
            );
        }

        private static Tween CreateScaleInTween(AnimationStepData step, GameObject target, AnimationSequenceState state)
        {
            var transform = target.transform;
            var originalScale = state.GetOrCapture(target).LocalScale;
            var startScale = Vector3.Scale(originalScale, step.ScaleFromMultiplier);
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () => transform.localScale = startScale,
                value => transform.localScale = Vector3.LerpUnclamped(startScale, originalScale, value)
            );
        }

        private static Tween CreateScaleOutTween(AnimationStepData step, GameObject target, AnimationSequenceState state)
        {
            var transform = target.transform;
            var originalScale = state.GetOrCapture(target).LocalScale;
            var targetScale = Vector3.Scale(originalScale, step.ScaleToMultiplier);
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () => transform.localScale = originalScale,
                value => transform.localScale = Vector3.LerpUnclamped(originalScale, targetScale, value)
            );
        }

        private static Tween CreateMoveToTween(AnimationStepData step, GameObject target)
        {
            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 start = default;
                Vector3 end = default;
                return CreateProgressTween(
                    target,
                    step.Duration,
                    step.Ease,
                    () =>
                    {
                        start = rectTransform.anchoredPosition3D;
                        end = start + step.MoveOffset;
                    },
                    value => rectTransform.anchoredPosition3D = Vector3.LerpUnclamped(start, end, value)
                );
            }

            var transform = target.transform;
            Vector3 localStart = default;
            Vector3 localEnd = default;
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () =>
                {
                    localStart = transform.localPosition;
                    localEnd = localStart + step.MoveOffset;
                },
                value => transform.localPosition = Vector3.LerpUnclamped(localStart, localEnd, value)
            );
        }

        private static Tween CreateRotateTween(AnimationStepData step, GameObject target)
        {
            var transform = target.transform;
            Vector3 start = default;
            Vector3 end = default;
            return CreateProgressTween(
                target,
                step.Duration,
                step.Ease,
                () =>
                {
                    start = transform.localEulerAngles;
                    end = start + step.RotationEuler;
                },
                value => transform.localEulerAngles = Vector3.LerpUnclamped(start, end, value)
            );
        }

        private static Tween CreateProgressTween(GameObject target, float duration, Ease ease, TweenCallback onStart, Action<float> onValue)
        {
            if (duration <= 0f)
            {
                return CreateCallbackTween(target, () =>
                {
                    onStart?.Invoke();
                    onValue?.Invoke(1f);
                });
            }

            var tween = DOTween.To(() => 0f, value => onValue?.Invoke(value), 1f, duration);
            tween.SetEase(ease);
            tween.SetTarget(target);
            tween.Pause();
            tween.OnStart(() =>
            {
                onStart?.Invoke();
                onValue?.Invoke(0f);
            });
            return tween;
        }

        private static Tween CreateCallbackTween(GameObject target, TweenCallback callback)
        {
            var sequence = DOTween.Sequence();
            sequence.SetTarget(target);
            sequence.Pause();
            if (callback != null)
            {
                sequence.AppendCallback(callback);
            }
            else
            {
                sequence.AppendInterval(0f);
            }

            return sequence;
        }

        private static Vector3 GetShakeOffset(int index, Vector3 strength, float randomness)
        {
            var angleRange = Mathf.Max(0f, randomness);
            var angle = (PseudoRandom(index * 2) * 2f - 1f) * angleRange;
            var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            var sign = PseudoRandom(index * 2 + 1) > 0.5f ? 1f : -1f;
            return new Vector3(direction.x * strength.x, direction.y * strength.y, strength.z) * sign;
        }

        private static float PseudoRandom(int seed)
        {
            return Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f);
        }
    }

    /// <summary>
    /// 播放开始前记录目标原始状态, 用于 SlideUp 计算和完成后还原.
    /// </summary>
    public sealed class AnimationSequenceState
    {
        private readonly Dictionary<Transform, TargetSnapshot> snapshots = new Dictionary<Transform, TargetSnapshot>();

        public TargetSnapshot GetOrCapture(GameObject target)
        {
            var transform = target.transform;
            if (!snapshots.TryGetValue(transform, out var snapshot))
            {
                snapshot = new TargetSnapshot(target);
                snapshots.Add(transform, snapshot);
            }

            return snapshot;
        }

        public void Restore()
        {
            foreach (var snapshot in snapshots.Values)
            {
                snapshot.Restore();
            }
        }
    }

    /// <summary>
    /// 单个目标的原始状态快照.
    /// </summary>
    public sealed class TargetSnapshot
    {
        private readonly Transform transform;
        private readonly RectTransform rectTransform;
        private readonly CanvasGroup canvasGroup;
        private readonly bool hasCanvasGroup;
        private readonly float canvasAlpha;

        public Vector3 LocalPosition { get; }
        public Vector3 AnchoredPosition3D { get; }
        public Vector3 LocalScale { get; }
        public Vector3 LocalEulerAngles { get; }

        public TargetSnapshot(GameObject target)
        {
            transform = target.transform;
            rectTransform = target.GetComponent<RectTransform>();
            canvasGroup = target.GetComponent<CanvasGroup>();
            hasCanvasGroup = canvasGroup != null;

            LocalPosition = transform.localPosition;
            AnchoredPosition3D = rectTransform != null ? rectTransform.anchoredPosition3D : Vector3.zero;
            LocalScale = transform.localScale;
            LocalEulerAngles = transform.localEulerAngles;

            if (hasCanvasGroup)
            {
                canvasAlpha = canvasGroup.alpha;
            }
        }

        public void Restore()
        {
            if (transform == null) return;

            transform.localScale = LocalScale;
            transform.localEulerAngles = LocalEulerAngles;

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition3D = AnchoredPosition3D;
            }
            else
            {
                transform.localPosition = LocalPosition;
            }

            if (hasCanvasGroup && canvasGroup != null)
            {
                canvasGroup.alpha = canvasAlpha;
            }
        }
    }
}
