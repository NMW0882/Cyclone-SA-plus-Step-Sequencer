using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MidiBleWpfSample.Controls
{
    public partial class SingleSliderNoFill : UserControl
    {
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(SingleSliderNoFill), new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(SingleSliderNoFill), new PropertyMetadata(100.0, OnPropertyChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(SingleSliderNoFill), new PropertyMetadata(50.0, OnPropertyChanged));

        public static readonly DependencyProperty StringFormatProperty =
            DependencyProperty.Register("StringFormat", typeof(string), typeof(SingleSliderNoFill), new PropertyMetadata("F1", OnPropertyChanged));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register("Suffix", typeof(string), typeof(SingleSliderNoFill), new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static readonly DependencyProperty SnapValueProperty =
            DependencyProperty.Register("SnapValue", typeof(double?), typeof(SingleSliderNoFill), new PropertyMetadata(null));

        public static readonly DependencyProperty SnapThresholdProperty =
            DependencyProperty.Register("SnapThreshold", typeof(double), typeof(SingleSliderNoFill), new PropertyMetadata(0.01));

        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public string StringFormat
        {
            get { return (string)GetValue(StringFormatProperty); }
            set { SetValue(StringFormatProperty, value); }
        }

        public string Suffix
        {
            get { return (string)GetValue(SuffixProperty); }
            set { SetValue(SuffixProperty, value); }
        }

        public double? SnapValue
        {
            get { return (double?)GetValue(SnapValueProperty); }
            set { SetValue(SnapValueProperty, value); }
        }

        public double SnapThreshold
        {
            get { return (double)GetValue(SnapThresholdProperty); }
            set { SetValue(SnapThresholdProperty, value); }
        }

        public SingleSliderNoFill()
        {
            InitializeComponent();
            this.SizeChanged += SingleSliderNoFill_SizeChanged;
            this.Loaded += SingleSliderNoFill_Loaded;
        }

        private void SingleSliderNoFill_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisuals();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = d as SingleSliderNoFill;
            slider?.UpdateVisuals();
        }

        private void SingleSliderNoFill_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (SliderCanvas.ActualWidth == 0) return;

            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (range <= 0) return;

            // Calculate position
            double valuePos = (Value - Minimum) / range * width;

            // Center the main thumb
            Canvas.SetLeft(MainThumb, valuePos - MainThumb.Width / 2);

            // Update Label
            MainLabel.Text = Value.ToString(StringFormat) + Suffix;

            // Position Main Label
            double mainLabelWidth = MainLabel.ActualWidth > 0 ? MainLabel.ActualWidth : 10;
            Canvas.SetLeft(MainLabel, valuePos - (mainLabelWidth / 2));
        }

        private void MainThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (width == 0 || range <= 0) return;

            double deltaVal = e.HorizontalChange / width * range;
            
            // Calculate new Value
            double tentativeValue = Value + deltaVal;

            // Snap logic
            if (SnapValue.HasValue)
            {
                double snapPoint = SnapValue.Value;
                double threshold = (Maximum - Minimum) * SnapThreshold;
                if (Math.Abs(tentativeValue - snapPoint) < threshold)
                {
                    tentativeValue = snapPoint;
                }
            }

            // Clamp the value to the min/max range
            if (tentativeValue < Minimum) tentativeValue = Minimum;
            if (tentativeValue > Maximum) tentativeValue = Maximum;

            Value = tentativeValue;
        }
    }
}
