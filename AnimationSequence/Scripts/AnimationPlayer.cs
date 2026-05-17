using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Animation
{
    /// <summary>
    /// 场景动画播放器, 引用 AnimationSequenceAsset 后按步骤顺序播放 DOTween Sequence.
    /// </summary>
    public class AnimationPlayer : MonoBehaviour
    {
        [Header("动画资产")]
        [SerializeField] private AnimationSequenceAsset sequenceAsset;

        [Header("目标解析")]
        [SerializeField] private Transform bindingRoot;

        [Header("播放选项")]
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool restoreOnComplete;

        [Header("完成事件")]
        [SerializeField] private UnityEvent onComplete = new UnityEvent();

        private Sequence currentSequence;
        private AnimationSequenceState currentState;
        private bool hasAppliedStartupActiveStates;

        public AnimationSequenceAsset SequenceAsset
        {
            get => sequenceAsset;
            set => sequenceAsset = value;
        }

        public bool RestoreOnComplete
        {
            get => restoreOnComplete;
            set => restoreOnComplete = value;
        }

        public UnityEvent OnComplete => onComplete;

        private void Reset()
        {
            bindingRoot = transform;
        }

        private void Awake()
        {
            ApplyStartupActiveStatesOnce();
        }

        private void OnEnable()
        {
            ApplyStartupActiveStatesOnce();

            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        [ContextMenu("播放动画序列")]
        public void Play()
        {
            Stop();

            if (sequenceAsset == null)
            {
                Debug.LogError("[AnimationPlayer] AnimationSequenceAsset 未设置.", this);
                return;
            }

            currentState = new AnimationSequenceState();
            currentSequence = DOTween.Sequence();
            currentSequence.SetTarget(this);
            currentSequence.SetUpdate(useUnscaledTime);
            currentSequence.Pause();

            var appendedCount = 0;
            foreach (var step in sequenceAsset.Steps)
            {
                if (step == null) continue;

                var target = step.ResolveTarget(bindingRoot);
                if (target == null)
                {
                    Debug.LogError($"[AnimationPlayer] 动画步骤目标为空或路径无效: {step.TargetPath}.", this);
                    continue;
                }

                currentState.GetOrCapture(target);
                var tween = AnimationTweenFactory.CreateTween(step, target, currentState, () => EnsureTargetActiveForPlay(target));
                if (tween == null)
                {
                    Debug.LogError($"[AnimationPlayer] 无法创建动画步骤: {step.EffectType}, target={target.name}.", target);
                    continue;
                }

                currentSequence.Append(tween);
                appendedCount++;
            }

            if (appendedCount <= 0)
            {
                Debug.LogWarning("[AnimationPlayer] 没有可播放的动画步骤.", this);
                Stop();
                return;
            }

            currentSequence.OnComplete(() =>
            {
                if (restoreOnComplete)
                {
                    currentState?.Restore();
                }

                currentSequence = null;
                currentState = null;

                // 所有步骤播放完成后触发, 用于 Inspector 绑定后续表现或流程.
                onComplete?.Invoke();
            });
            currentSequence.Play();
        }

        [ContextMenu("停止动画序列")]
        public void Stop()
        {
            if (currentSequence != null && currentSequence.IsActive())
            {
                currentSequence.Kill(false);
            }

            currentSequence = null;
            currentState = null;
        }

        private void ApplyStartupActiveStatesOnce()
        {
            if (hasAppliedStartupActiveStates) return;

            hasAppliedStartupActiveStates = true;
            if (sequenceAsset == null) return;

            foreach (var step in sequenceAsset.Steps)
            {
                if (step == null || step.StartupActiveState == AnimationStartupActiveState.KeepCurrent) continue;

                var target = step.ResolveTarget(bindingRoot);
                if (target == null)
                {
                    Debug.LogError($"[AnimationPlayer] 开始激活状态目标为空或路径无效: {step.TargetPath}.", this);
                    continue;
                }

                // 开始状态只处理自身 activeSelf, 不隐式修改父节点激活状态.
                target.SetActive(step.StartupActiveState == AnimationStartupActiveState.Active);
            }
        }

        private void EnsureTargetActiveForPlay(GameObject target)
        {
            if (!target.activeSelf)
            {
                target.SetActive(true);
            }

            if (!target.activeInHierarchy)
            {
                Debug.LogError($"[AnimationPlayer] {target.name} 的父节点未激活, 播放时目标无法真正显示.", target);
            }
        }
    }
}
