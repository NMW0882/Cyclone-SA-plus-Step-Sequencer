using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MidiBleWpfSample.Controls
{
    public partial class MultiRangeSlider : UserControl
    {
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(100.0, OnPropertyChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(50.0, OnPropertyChanged));

        public static readonly DependencyProperty RangeStartProperty =
            DependencyProperty.Register("RangeStart", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(25.0, OnPropertyChanged));

        public static readonly DependencyProperty RangeEndProperty =
            DependencyProperty.Register("RangeEnd", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(75.0, OnPropertyChanged));

        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register("TickFrequency", typeof(double), typeof(MultiRangeSlider), new PropertyMetadata(0.0, OnPropertyChanged));

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

        public double RangeStart
        {
            get { return (double)GetValue(RangeStartProperty); }
            set { SetValue(RangeStartProperty, value); }
        }

        public double RangeEnd
        {
            get { return (double)GetValue(RangeEndProperty); }
            set { SetValue(RangeEndProperty, value); }
        }

        public double TickFrequency
        {
            get { return (double)GetValue(TickFrequencyProperty); }
            set { SetValue(TickFrequencyProperty, value); }
        }

        public static readonly DependencyProperty StringFormatProperty =
            DependencyProperty.Register("StringFormat", typeof(string), typeof(MultiRangeSlider), new PropertyMetadata("F1", OnPropertyChanged));

        public string StringFormat
        {
            get { return (string)GetValue(StringFormatProperty); }
            set { SetValue(StringFormatProperty, value); }
        }

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register("Suffix", typeof(string), typeof(MultiRangeSlider), new PropertyMetadata(string.Empty, OnPropertyChanged));

        public string Suffix
        {
            get { return (string)GetValue(SuffixProperty); }
            set { SetValue(SuffixProperty, value); }
        }

        public MultiRangeSlider()
        {
            InitializeComponent();
            this.SizeChanged += MultiRangeSlider_SizeChanged;
            this.Loaded += MultiRangeSlider_Loaded;
        }

        private void MultiRangeSlider_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisuals();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = d as MultiRangeSlider;
            slider?.UpdateVisuals();
        }

        private void MultiRangeSlider_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private static readonly System.Windows.Media.Brush BlueBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007ACC"));
        private static readonly System.Windows.Media.Brush GrayBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555"));

        private void UpdateVisuals()
        {
            if (SliderCanvas.ActualWidth == 0) return;

            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (range <= 0) return;

            // Calculate positions
            double valuePos = (Value - Minimum) / range * width;
            double startPos = (RangeStart - Minimum) / range * width;
            double endPos = (RangeEnd - Minimum) / range * width;

            // Center the main thumb
            Canvas.SetLeft(MainThumb, valuePos - MainThumb.Width / 2);

            // Position side thumbs relative to main thumb or absolute?
            // Let's position them absolute for now to match the values
            Canvas.SetLeft(LeftThumb, startPos - LeftThumb.Width); // Align right edge of ear to startPos
            Canvas.SetLeft(RightThumb, endPos); // Align left edge of ear to endPos

            // Update Range Highlight
            double highlightStart = startPos;
            double highlightWidth = Math.Max(0, endPos - startPos);
            Canvas.SetLeft(RangeHighlight, highlightStart);
            RangeHighlight.Width = highlightWidth;
            
            // Update Colors
            // If either thumb is active (pulled out), both turn Blue.
            // Use epsilon to handle floating point precision issues
            double epsilon = 1e-9;
            bool isAnyActive = (Value - RangeStart > epsilon) || (RangeEnd - Value > epsilon);
            var thumbBrush = isAnyActive ? BlueBrush : GrayBrush;

            LeftThumb.Background = thumbBrush;
            RightThumb.Background = thumbBrush;

            // Update Labels
            MainLabel.Text = Value.ToString(StringFormat) + Suffix;
            LeftLabel.Text = RangeStart.ToString(StringFormat) + Suffix;
            RightLabel.Text = RangeEnd.ToString(StringFormat) + Suffix;

            // Center labels (approximate centering based on assumed text width or measure)
            // Ideally we should measure text, but for simple digits, centering relative to thumb is okay.
            // We'll use ActualWidth if available, otherwise estimate.
            // Note: TextBlock ActualWidth might not be updated immediately without layout update.
            // For simplicity in this sample, we'll just set Left and let it align left, or try to center.
            // Better approach: Use a binding or a converter, but here we do it in code.
            
            // Force layout update for accurate width? No, too expensive.
            // Let's just center based on the thumb center.
            
            if (isAnyActive)
            {
                MainLabel.Visibility = Visibility.Collapsed;
                LeftLabel.Visibility = Visibility.Visible;
                RightLabel.Visibility = Visibility.Visible;

                // Position Left Label
                double leftLabelWidth = LeftLabel.ActualWidth > 0 ? LeftLabel.ActualWidth : 10;
                // Move outward by 12 pixels to prevent overlap
                Canvas.SetLeft(LeftLabel, (startPos - 5) - (leftLabelWidth / 2) - 6);

                // Position Right Label
                double rightLabelWidth = RightLabel.ActualWidth > 0 ? RightLabel.ActualWidth : 10;
                // Move outward by 12 pixels to prevent overlap
                Canvas.SetLeft(RightLabel, (endPos + 5) - (rightLabelWidth / 2) + 6);
            }
            else
            {
                MainLabel.Visibility = Visibility.Visible;
                LeftLabel.Visibility = Visibility.Collapsed;
                RightLabel.Visibility = Visibility.Collapsed;

                // Position Main Label
                double mainLabelWidth = MainLabel.ActualWidth > 0 ? MainLabel.ActualWidth : 10;
                // MainThumb center is valuePos.
                Canvas.SetLeft(MainLabel, valuePos - (mainLabelWidth / 2));
            }
        }

        private void MainThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (width == 0 || range <= 0) return;

            double deltaVal = e.HorizontalChange / width * range;
            
            // Calculate new Value first and clamp it
            double tentativeValue = Value + deltaVal;
            if (tentativeValue < Minimum) tentativeValue = Minimum;
            if (tentativeValue > Maximum) tentativeValue = Maximum;

            // Calculate effective delta based on the clamped value
            double effectiveDelta = tentativeValue - Value;

            double newStart = RangeStart + effectiveDelta;
            double newEnd = RangeEnd + effectiveDelta;

            // Clamp Start/End to global bounds and Value
            if (newStart < Minimum) newStart = Minimum;
            if (newStart > tentativeValue) newStart = tentativeValue;

            if (newEnd > Maximum) newEnd = Maximum;
            if (newEnd < tentativeValue) newEnd = tentativeValue;

            if (TickFrequency > 0)
            {
                Value = Math.Round(tentativeValue / TickFrequency) * TickFrequency;
                RangeStart = Math.Round(newStart / TickFrequency) * TickFrequency;
                RangeEnd = Math.Round(newEnd / TickFrequency) * TickFrequency;
            }
            else
            {
                Value = tentativeValue;
                RangeStart = newStart;
                RangeEnd = newEnd;
            }
        }

        private const double SnapThreshold = 4.0;

        private void LeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (width == 0 || range <= 0) return;

            // Calculate current pixel position of LeftThumb (relative to track start)
            double currentPixelPos = (RangeStart - Minimum) / range * width;
            
            // Calculate proposed new pixel position
            double proposedPixelPos = currentPixelPos + e.HorizontalChange;
            
            // Calculate pixel position of MainThumb (Value)
            double valuePixelPos = (Value - Minimum) / range * width;

            double newStart;

            // Snap logic
            // If we are close to the value, snap to it
            if (Math.Abs(valuePixelPos - proposedPixelPos) < SnapThreshold)
            {
                proposedPixelPos = valuePixelPos;
                newStart = Value; // Explicitly snap to Value
            }
            else
            {
                // Convert back to value
                newStart = Minimum + (proposedPixelPos / width * range);
            }

            // Constraints
            if (newStart < Minimum) newStart = Minimum;
            if (newStart > Value) newStart = Value;

            if (TickFrequency > 0)
            {
                RangeStart = Math.Round(newStart / TickFrequency) * TickFrequency;
            }
            else
            {
                RangeStart = newStart;
            }
        }

        private void RightThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double width = SliderCanvas.ActualWidth;
            double range = Maximum - Minimum;
            if (width == 0 || range <= 0) return;

            // Calculate current pixel position of RightThumb
            double currentPixelPos = (RangeEnd - Minimum) / range * width;
            
            // Calculate proposed new pixel position
            double proposedPixelPos = currentPixelPos + e.HorizontalChange;
            
            // Calculate pixel position of MainThumb (Value)
            double valuePixelPos = (Value - Minimum) / range * width;

            double newEnd;

            // Snap logic
            if (Math.Abs(proposedPixelPos - valuePixelPos) < SnapThreshold)
            {
                proposedPixelPos = valuePixelPos;
                newEnd = Value; // Explicitly snap to Value
            }
            else
            {
                // Convert back to value
                newEnd = Minimum + (proposedPixelPos / width * range);
            }

            // Constraints
            if (newEnd > Maximum) newEnd = Maximum;
            if (newEnd < Value) newEnd = Value;

            if (TickFrequency > 0)
            {
                RangeEnd = Math.Round(newEnd / TickFrequency) * TickFrequency;
            }
            else
            {
                RangeEnd = newEnd;
            }
        }
    }
}