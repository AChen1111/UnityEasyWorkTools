using System.Collections.Generic;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// 动画序列资产, 保存多个按顺序播放的动画步骤.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationSequence", menuName = "PG/Anim/Animation Sequence", order = 20)]
    public class AnimationSequenceAsset : ScriptableObject
    {
        [SerializeField] private List<AnimationStepData> steps = new List<AnimationStepData>();

        public IReadOnlyList<AnimationStepData> Steps => steps;

        public void AddStep(AnimationStepData step)
        {
            steps.Add(step);
        }

        public void RemoveStepAt(int index)
        {
            steps.RemoveAt(index);
        }
    }
}
