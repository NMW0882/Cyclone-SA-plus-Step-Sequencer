using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using MidiBleWpfSample.Sequencer.Common;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Models.Configurations;
using MidiBleWpfSample.Sequencer.Patterns;
using MidiBleWpfSample.Services;

namespace MidiBleWpfSample.Sequencer.ViewModels
{
    /// <summary>
    /// ViewModel for a single step in the sequencer. Wraps a StepModel and provides INotifyPropertyChanged.
    /// </summary>
    public class StepViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<string, ISequencePattern> _patternRegistry;
        private TimeSpan _midiBaseDuration;
        private const double DefaultDurationSeconds = 3.0;

        public StepModel Model { get; }

        /// <summary>
        /// List of available pattern names for the UI dropdown.
        /// </summary>
        public List<string> AvailablePatternNames { get; }

        public ICommand SelectMidiFileCommand { get; }

        public StepViewModel(StepModel model, Dictionary<string, ISequencePattern> patternRegistry, List<string> availablePatternNames)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _patternRegistry = patternRegistry ?? throw new ArgumentNullException(nameof(patternRegistry));
            AvailablePatternNames = availablePatternNames ?? throw new ArgumentNullException(nameof(availablePatternNames));

            SelectMidiFileCommand = new DelegateCommand(SelectMidiFile);

            // If the initial pattern is a MIDI file, calculate its duration properties
            if (IsMidiFilePattern)
            {
                _midiBaseDuration = MidiFilePattern.GetMidiFileDuration(MidiFilePath);
                UpdateMidiStepDuration();
            }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set
            {
                if (Model.IsEnabled != value)
                {
                    Model.IsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The name of the pattern to use for this step.
        /// When changed, this also resets the Config property to the default for the new pattern.
        /// </summary>
        public string PatternName
        {
            get => Model.PatternName;
            set
            {
                if (Model.PatternName != value)
                {
                    var oldPatternWasMidi = IsMidiFilePattern;

                    Model.PatternName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMidiFilePattern));
                    OnPropertyChanged(nameof(IsStrokePattern));

                    // When pattern name changes, create a new default config for it
                    if (_patternRegistry.TryGetValue(value, out var pattern))
                    {
                        Config = pattern.CreateDefaultConfiguration();
                    }

                    // If we changed away from a MIDI pattern, reset duration to a sensible default
                    if (oldPatternWasMidi && !IsMidiFilePattern)
                    {
                        Duration = TimeSpan.FromSeconds(DefaultDurationSeconds);
                    }
                }
            }
        }

        /// <summary>
        /// The duration this step should run for.
        /// </summary>
        public TimeSpan Duration
        {
            get => Model.Duration;
            set
            {
                if (Model.Duration != value)
                {
                    Model.Duration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DurationInSeconds));
                }
            }
        }

        /// <summary>
        /// Wrapper for Duration.TotalSeconds for easy binding to UI controls like sliders.
        /// </summary>
        public double DurationInSeconds
        {
            get => Model.Duration.TotalSeconds;
            set
            {
                // This setter is only used by the UI for non-MIDI patterns.
                if (IsMidiFilePattern) return;

                var newDuration = TimeSpan.FromSeconds(value);
                if (Model.Duration != newDuration)
                {
                    Model.Duration = newDuration;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        public double MinDurationInSeconds
        {
            get => Model.MinDuration.TotalSeconds;
            set
            {
                var newDuration = TimeSpan.FromSeconds(value);
                if (Model.MinDuration != newDuration)
                {
                    Model.MinDuration = newDuration;
                    OnPropertyChanged();
                }
            }
        }

        public double MaxDurationInSeconds
        {
            get => Model.MaxDuration.TotalSeconds;
            set
            {
                var newDuration = TimeSpan.FromSeconds(value);
                if (Model.MaxDuration != newDuration)
                {
                    Model.MaxDuration = newDuration;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The generic intensity of the pattern, as a percentage (0-100).
        /// </summary>
        public double Intensity
        {
            get => Model.Intensity;
            set
            {
                if (Model.Intensity != value)
                {
                    Model.Intensity = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MinIntensity
        {
            get => Model.MinIntensity;
            set
            {
                if (Model.MinIntensity != value)
                {
                    Model.MinIntensity = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MaxIntensity
        {
            get => Model.MaxIntensity;
            set
            {
                if (Model.MaxIntensity != value)
                {
                    Model.MaxIntensity = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Holds the pattern-specific configuration object.
        /// </summary>
        public PatternConfiguration Config
        {
            get => Model.Config;
            set
            {
                if (Model.Config != value)
                {
                    Model.Config = value;
                    OnPropertyChanged();
                    // Notify that all config-dependent properties have changed
                    OnPropertyChanged(nameof(IsMidiFilePattern));
                    OnPropertyChanged(nameof(IsStrokePattern));
                    OnPropertyChanged(nameof(MidiFilePath));
                    OnPropertyChanged(nameof(MidiAmplitude));
                    OnPropertyChanged(nameof(MidiInvert));
                    OnPropertyChanged(nameof(MidiSpeed));
                }
            }
        }

        #region MidiFilePattern Properties

        public bool IsMidiFilePattern => Model.Config is MidiFilePatternConfig;
        public bool IsStrokePattern => Model.Config is StrokePatternConfig;

        public string MidiFilePath
        {
            get => (Model.Config as MidiFilePatternConfig)?.FilePath ?? string.Empty;
            set
            {
                if (Model.Config is MidiFilePatternConfig config && config.FilePath != value)
                {
                    config.FilePath = value;
                    OnPropertyChanged();

                    _midiBaseDuration = MidiFilePattern.GetMidiFileDuration(value);
                    UpdateMidiStepDuration();
                }
            }
        }

        public double MidiAmplitude
        {
            get => (Model.Config as MidiFilePatternConfig)?.Amplitude ?? 1.0;
            set
            {
                if (Model.Config is MidiFilePatternConfig config && config.Amplitude != value)
                {
                    config.Amplitude = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MidiSpeed
        {
            get => (Model.Config as MidiFilePatternConfig)?.Speed ?? 100.0;
            set
            {
                if (Model.Config is MidiFilePatternConfig config && config.Speed != value)
                {
                    config.Speed = value;
                    OnPropertyChanged();
                    UpdateMidiStepDuration();
                }
            }
        }

        public bool MidiInvert
        {
            get => (Model.Config as MidiFilePatternConfig)?.Invert ?? false;
            set
            {
                if (Model.Config is MidiFilePatternConfig config && config.Invert != value)
                {
                    config.Invert = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateMidiStepDuration()
        {
            if (!IsMidiFilePattern || _midiBaseDuration <= TimeSpan.Zero)
            {
                return;
            }

            double speed = MidiSpeed;
            if (speed <= 0)
            {
                // Prevent division by zero and infinite duration.
                // Set a very long duration to effectively pause.
                Duration = TimeSpan.FromHours(1);
                return;
            }

            double speedMultiplier = speed / 100.0;
            Duration = _midiBaseDuration / speedMultiplier;
        }

        private void SelectMidiFile(object? _)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "MIDI Files (*.mid)|*.mid|All files (*.*)|*.*",
                Title = "Select a MIDI File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                MidiFilePath = openFileDialog.FileName;
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshUI()
        {
            // Passing null or an empty string to OnPropertyChanged will refresh all bindings.
            OnPropertyChanged(null);
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            // Ensure the event is raised on the UI thread.
            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                }));
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
