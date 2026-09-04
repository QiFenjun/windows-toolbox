using WindowsToolbox.Core.Models;

namespace WindowsToolbox.App.Utilities;

/// <summary>Pure layout/timing values used by the sidebar motion hotfix.</summary>
public static class SidebarMotionMetrics
{
    public const double ExpandedWidth = 232;
    public const double CollapsedWidth = 72;
    public const double WidthDelta = ExpandedWidth - CollapsedWidth;

    public static readonly TimeSpan GeometryDuration = TimeSpan.FromMilliseconds(175);
    public static readonly TimeSpan TextFadeInDuration = TimeSpan.FromMilliseconds(110);
    public static readonly TimeSpan TextFadeOutDuration = TimeSpan.FromMilliseconds(90);
    public static readonly TimeSpan RecentExpandDelay = TimeSpan.FromMilliseconds(90);
    public static readonly TimeSpan RecentFadeInDuration = TimeSpan.FromMilliseconds(110);
    public static readonly TimeSpan RecentFadeOutDuration = TimeSpan.FromMilliseconds(90);

    public static double CalculateContentStartOffset(double currentSidebarWidth, double currentContentOffset, double targetSidebarWidth) =>
        currentSidebarWidth + currentContentOffset - targetSidebarWidth;

    public static TimeSpan Scale(TimeSpan duration, ReducedMotionMode mode) => mode switch
    {
        ReducedMotionMode.Off => TimeSpan.Zero,
        ReducedMotionMode.Reduced => TimeSpan.FromTicks(duration.Ticks / 2),
        _ => duration
    };

    public static double RecentTranslateOffset(ReducedMotionMode mode) =>
        mode == ReducedMotionMode.Reduced ? 2 : 4;
}
