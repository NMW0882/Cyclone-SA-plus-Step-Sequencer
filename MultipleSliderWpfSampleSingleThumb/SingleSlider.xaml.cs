using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MultipleSliderWpfSample
{
    public partial class SingleSlider : UserControl
    {
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(SingleSlider), new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(SingleSlider), new PropertyMetadata(100.0, OnPropertyChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(SingleSlider), new PropertyMetadata(50.0, OnPropertyChanged));

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

        public SingleSlider()
        {
            InitializeComponent();
            this.SizeChanged += SingleSlider_SizeChanged;
            this.Loaded += SingleSlider_Loaded;
        }

        private void SingleSlider_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisuals();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = d as SingleSlider;
            slider?.UpdateVisuals();
        }

        private void SingleSlider_SizeChanged(object sender, SizeChangedEventArgs e)
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

            // Update Range Highlight (Progress from 0 to Value)
            Canvas.SetLeft(RangeHighlight, 0);
            RangeHighlight.Width = Math.Max(0, valuePos);
            
            // Update Label
            MainLabel.Text = Value.ToString("F1");

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
            if (tentativeValue < Minimum) tentativeValue = Minimum;
            if (tentativeValue > Maximum) tentativeValue = Maximum;

            Value = tentativeValue;
        }
    }
}
