using System.Windows;
using System.Windows.Media.Animation;

namespace WindowsToolbox.App.Utilities;

/// <summary>Interpolates pixel GridLength values without scaling text or icons.</summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public IEasingFunction? EasingFunction { get; set; }
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(GridLength), typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(GridLength), typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(0)));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue,
        object defaultDestinationValue,
        AnimationClock animationClock)
    {
        GridLength from = From.IsAbsolute ? From : defaultOriginValue is GridLength origin ? origin : new GridLength(0);
        GridLength to = To.IsAbsolute ? To : defaultDestinationValue is GridLength destination ? destination : from;
        double progress = animationClock.CurrentProgress ?? 1;
        double easedProgress = progress;
        if (EasingFunction is not null)
            easedProgress = EasingFunction.Ease(progress);

        return new GridLength(from.Value + ((to.Value - from.Value) * easedProgress));
    }
}
