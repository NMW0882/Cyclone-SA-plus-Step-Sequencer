using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace MidiBleWpfSample.Sequencer.Models
{
    /// <summary>
    /// Holds the configuration for a single step within a sequence.
    /// </summary>
    public class StepModel : INotifyPropertyChanged
    {
        private string _patternName = string.Empty;
        /// <summary>
        /// The name of the pattern to use for this step.
        /// </summary>
        public string PatternName
        {
            get => _patternName;
            set
            {
                if (_patternName == value) return;
                _patternName = value;
                OnPropertyChanged();
            }
        }

        private bool _isEnabled = true;
        /// <summary>
        /// Gets or sets a value indicating whether this step is enabled.
        /// Disabled steps will be skipped by the sequencer.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _duration = TimeSpan.FromSeconds(3.0);
        /// <summary>
        /// The duration this step should run for.
        /// </summary>
        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration == value) return;
                _duration = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _minDuration = TimeSpan.FromSeconds(3.0);
        public TimeSpan MinDuration
        {
            get => _minDuration;
            set
            {
                if (_minDuration == value) return;
                _minDuration = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _maxDuration = TimeSpan.FromSeconds(3.0);
        public TimeSpan MaxDuration
        {
            get => _maxDuration;
            set
            {
                if (_maxDuration == value) return;
                _maxDuration = value;
                OnPropertyChanged();
            }
        }

        private double _intensity = 100.0;
        /// The intensity of the pattern, as a percentage (0-100).
        /// </summary>
        public double Intensity
        {
            get => _intensity;
            set
            {
                if (_intensity == value) return;
                _intensity = value;
                OnPropertyChanged();
            }
        }

        private double _minIntensity = 100.0;
        public double MinIntensity
        {
            get => _minIntensity;
            set
            {
                if (_minIntensity == value) return;
                _minIntensity = value;
                OnPropertyChanged();
            }
        }

        private double _maxIntensity = 100.0;
        public double MaxIntensity
        {
            get => _maxIntensity;
            set
            {
                if (_maxIntensity == value) return;
                _maxIntensity = value;
                OnPropertyChanged();
            }
        }

        private PatternConfiguration _config = null!;
        /// <summary>
        /// Holds the pattern-specific configuration object.
        /// </summary>
        public PatternConfiguration Config
        {
            get => _config;
            set
            {
                if (_config == value) return;

                if (_config != null)
                {
                    _config.PropertyChanged -= Config_PropertyChanged;
                }

                _config = value;

                if (_config != null)
                {
                    _config.PropertyChanged += Config_PropertyChanged;
                }

                OnPropertyChanged();
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (_config != null)
            {
                // Ensure the handler is attached after deserialization.
                // Unsubscribe first to prevent any potential duplicate subscriptions.
                _config.PropertyChanged -= Config_PropertyChanged;
                _config.PropertyChanged += Config_PropertyChanged;
            }
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Bubble up the change notification.
            // This will be caught by PresetModel's subscription to StepModel's PropertyChanged event.
            OnPropertyChanged(nameof(Config));
        }

        // public RandomizationOptions Randomization { get; set; } // To be added in a future phase

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}