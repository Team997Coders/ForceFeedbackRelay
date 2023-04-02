using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ForceFeedbackRelay;

public sealed partial class AxisBar
{
    public AxisBar()
    {
        InitializeComponent();
        // Update progress indicator on size changed
        SizeChanged += HandleSizeChanged;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ProgressBarBackground.Height = e.NewSize.Height;
        ProgressBarBackground.Width = e.NewSize.Width;
        SetProgressBarIndicatorSize();
    }

    private void SetProgressBarIndicatorSize()
    {
        // Need to calculate the width the progress bar should show
        // If the bar is vertical, this should be the width of the canvas
        var progress = Math.Clamp((Value - Minimum) / (Maximum - Minimum), -1, 1);
        if (Orientation == Orientation.Vertical)
        {
            var height = ActualHeight * progress;
            ProgressBarIndicator.Height = height;
            ProgressBarIndicator.Width = ActualWidth;
            // Make progress bar run bottom to top
            ProgressBarIndicator.SetValue(Canvas.TopProperty, ActualHeight-height);
        }
        // Otherwise, return the number of pixels that should be filled
        else
        {
            var width = ActualWidth * progress;
            ProgressBarIndicator.Width = width;
            ProgressBarIndicator.Height = ActualHeight;
        }
    }

    // private double ProgressWidth
    // {
    //     get
    //     {
    //         // return 100;
    //         // Need to calculate the width the progress bar should show
    //         // If the bar is vertical, this should be the width of the canvas
    //         if (Orientation == Orientation.Vertical) return Canvas.ActualWidth;
    //         // Otherwise, return the number of pixels that should be filled
    //         return Canvas.ActualWidth * ((Value + Minimum) / (Maximum + Minimum));
    //     }
    // }

    private CornerRadius GridRadius(double radius) => new(radius);

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set
        {
            SetValue(ValueProperty, value);
            SetProgressBarIndicatorSize();
        }
    }
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }
    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(AxisBar), new PropertyMetadata(Orientation.Horizontal));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(AxisBar), new PropertyMetadata(0.0));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(AxisBar), new PropertyMetadata(-1.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(AxisBar), new PropertyMetadata(1.0));
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(double), typeof(AxisBar), new PropertyMetadata(5.0));
}
