namespace Game.Animation
{
    /// <summary>
    /// 可视化动画序列支持的硬编码效果类型.
    /// </summary>
    public enum AnimationEffectType
    {
        FadeIn,
        FadeOut,
        SlideUp,
        Shake,
        ScaleIn,
        ScaleOut,
        MoveTo,
        Rotate
    }

    /// <summary>
    /// 游戏开始时的目标激活状态, 只由 AnimationPlayer 在启动阶段应用一次.
    /// </summary>
    public enum AnimationStartupActiveState
    {
        KeepCurrent,
        Active,
        Inactive
    }
}
