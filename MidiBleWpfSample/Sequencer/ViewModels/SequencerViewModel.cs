using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using MidiBleWpfSample.Sequencer.Common;
using MidiBleWpfSample.Sequencer.Engine;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Patterns;
using Newtonsoft.Json;

namespace MidiBleWpfSample.Sequencer.ViewModels
{
    public enum PresetPlaybackMode
    {
        Sequential,
        Repeat,
        Random
    }

    public class SequencerViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SequencerEngine _engine;
        private readonly Dictionary<string, ISequencePattern> _patternRegistry;
        private bool _isPlayingPresets = false;
        private List<PresetModel> _currentPresetPlaylist = new List<PresetModel>();
        private static readonly Random _random = new Random();
        private bool _isUpdatingSteps = false; // Flag to prevent recursion

        private PresetModel? _currentlyPlayingPreset;
        private StepViewModel? _currentlyPlayingStep;

        public ObservableCollection<StepViewModel> Steps { get; } = new();
        public ObservableCollection<PresetModel> UserPresets { get; } = new();

        private StepViewModel? _selectedStep;
        public StepViewModel? SelectedStep
        {
            get => _selectedStep;
            set { _selectedStep = value; OnPropertyChanged(); }
        }

        private PresetModel? _selectedPreset;
        public PresetModel? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset == value) return;

                // Before switching, save any pending changes to the old preset.
                SaveChangesToSelectedPreset();

                _selectedPreset = value;
                LoadStepsFromPreset(value);
                OnPropertyChanged();
            }
        }

        private bool _isRandomized;
        public bool IsRandomized
        {
            get => _isRandomized;
            set
            {
                if (_isRandomized != value)
                {
                    _isRandomized = value;
                    OnPropertyChanged();
                    UpdateLiveSequence(); // Update engine if running
                }
            }
        }

        private PresetPlaybackMode _currentPlaybackMode = PresetPlaybackMode.Sequential;
        public PresetPlaybackMode CurrentPlaybackMode
        {
            get => _currentPlaybackMode;
            set
            {
                if (_currentPlaybackMode == value) return;
                _currentPlaybackMode = value;
                OnPropertyChanged();
                UpdateLiveSequence();
            }
        }


        public List<string> AvailablePatternNames { get; } = new();

        public ICommand AddStepCommand { get; }
        public ICommand RemoveStepCommand { get; }
        public ICommand MoveStepUpCommand { get; }
        public ICommand MoveStepDownCommand { get; }
        public ICommand DuplicateStepCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AddPresetCommand { get; }
        public ICommand RemovePresetCommand { get; }
        public ICommand MovePresetUpCommand { get; }
        public ICommand MovePresetDownCommand { get; }
        public ICommand SavePresetCommand { get; }
        public ICommand LoadPresetCommand { get; }

        public event Action<int>? SequencerMidiValueGenerated;
        public event Action? SequencerStopped;
        public event Action<string>? PresetFileLoaded;

        public SequencerViewModel()
        {
            _patternRegistry = new Dictionary<string, ISequencePattern>();
            RegisterPatterns();

            _engine = new SequencerEngine(_patternRegistry);
            _engine.MidiValueGenerated += (ccValue) => SequencerMidiValueGenerated?.Invoke(ccValue);
            _engine.StepIndexChanged += OnEngineStepIndexChanged;
            _engine.StepEvaluated += OnStepEvaluated;
            _engine.SequenceLooped += OnSequenceLooped;

            AddStepCommand = new DelegateCommand(AddStep);
            RemoveStepCommand = new DelegateCommand(RemoveStep, CanRemoveStep);
            MoveStepUpCommand = new DelegateCommand(MoveStepUp, CanMoveStepUp);
            MoveStepDownCommand = new DelegateCommand(MoveStepDown, CanMoveStepDown);
            DuplicateStepCommand = new DelegateCommand(DuplicateStep, CanDuplicateStep);
            PlayCommand = new DelegateCommand(Play, CanPlay);
            StopCommand = new DelegateCommand(Stop, CanStop);

            AddPresetCommand = new DelegateCommand(_ => AddPreset(false));
            RemovePresetCommand = new DelegateCommand(_ => RemoveSelectedPreset(), _ => CanManipulateSelectedPreset());
            MovePresetUpCommand = new DelegateCommand(_ => MoveSelectedPresetUp(), _ => CanMoveSelectedPresetUp());
            MovePresetDownCommand = new DelegateCommand(_ => MoveSelectedPresetDown(), _ => CanMoveSelectedPresetDown());

            SavePresetCommand = new DelegateCommand(_ => SavePresetToFile(), _ => SelectedPreset != null);
            LoadPresetCommand = new DelegateCommand(_ => LoadPresetFromFile());

            Steps.CollectionChanged += OnStepsChanged;
            UserPresets.CollectionChanged += OnUserPresetsChanged;
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }

        private void OnEngineStepIndexChanged(int globalStepIndex)
        {
            // Clear previous highlights
            if (_currentlyPlayingPreset != null)
            {
                _currentlyPlayingPreset.IsPlaying = false;
                _currentlyPlayingPreset = null;
            }
            if (_currentlyPlayingStep != null)
            {
                _currentlyPlayingStep.IsPlaying = false;
                _currentlyPlayingStep = null;
            }

            if (globalStepIndex < 0) return; // Playback stopped

            if (_isPlayingPresets)
            {
                if (CurrentPlaybackMode == PresetPlaybackMode.Random)
                {
                    // In Random mode, the currently playing preset is determined by _currentPlaylistIndex.
                    // The globalStepIndex from the engine refers to the index within that single preset.
                    if (_currentPlaylistIndex < _currentPresetPlaylist.Count)
                    {
                        var currentPreset = _currentPresetPlaylist[_currentPlaylistIndex];
                        _currentlyPlayingPreset = currentPreset;
                        currentPreset.IsPlaying = true;

                        // Highlight the step only if the playing preset is the one selected in the UI
                        if (currentPreset == SelectedPreset)
                        {
                            if (globalStepIndex < Steps.Count)
                            {
                                _currentlyPlayingStep = Steps[globalStepIndex];
                                _currentlyPlayingStep.IsPlaying = true;
                            }
                        }
                    }
                }
                else // Sequential or Repeat mode
                {
                    // Original logic for combined playlist
                    var playingPresets = _currentPresetPlaylist;
                    if (!playingPresets.Any()) return; // Should not happen if _isPlayingPresets is true

                    int stepCounter = 0;
                    foreach (var preset in playingPresets)
                    {
                        int stepsInPreset = preset.Steps.Count;
                        if (globalStepIndex < stepCounter + stepsInPreset)
                        {
                            _currentlyPlayingPreset = preset;
                            preset.IsPlaying = true;

                            // If the playing preset is the currently selected one, highlight the step
                            if (preset == SelectedPreset)
                            {
                                int localStepIndex = globalStepIndex - stepCounter;
                                if (localStepIndex < Steps.Count)
                                {
                                    _currentlyPlayingStep = Steps[localStepIndex];
                                    _currentlyPlayingStep.IsPlaying = true;
                                }
                            }
                            break;
                        }
                        stepCounter += stepsInPreset;
                    }
                }
            }
            else // Not playing from presets, but from the live view
            {
                if (globalStepIndex < Steps.Count)
                {
                    _currentlyPlayingStep = Steps[globalStepIndex];
                    _currentlyPlayingStep.IsPlaying = true;
                }
            }
        }

        private void OnStepEvaluated(int stepIndex)
        {
            if (_isPlayingPresets)
            {
                var playingPresets = _currentPresetPlaylist;
                if (!playingPresets.Any()) return;

                int stepCounter = 0;
                foreach (var preset in playingPresets)
                {
                    int stepsInPreset = preset.Steps.Count;
                    if (stepIndex < stepCounter + stepsInPreset)
                    {
                        // If the preset being played is the one currently being edited/viewed.
                        if (preset == SelectedPreset)
                        {
                            int localStepIndex = stepIndex - stepCounter;
                            if (localStepIndex >= 0 && localStepIndex < Steps.Count)
                            {
                                Steps[localStepIndex].RefreshUI();
                            }
                        }
                        break;
                    }
                    stepCounter += stepsInPreset;
                }
            }
            else // Playing from the live view
            {
                if (stepIndex >= 0 && stepIndex < Steps.Count)
                {
                    Steps[stepIndex].RefreshUI();
                }
            }
        }


        private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // When steps are added or removed, subscribe/unsubscribe to their PropertyChanged event for auto-saving.
            if (e.NewItems != null)
            {
                foreach (StepViewModel item in e.NewItems)
                {
                    item.PropertyChanged += OnStepViewModelPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (StepViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnStepViewModelPropertyChanged;
                }
            }

            // Auto-save changes to the selected preset.
            SaveChangesToSelectedPreset();

            // If the collection of steps changes (add/remove), we must update the engine.
            UpdateLiveSequence();
        }

        private void OnStepViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // If any property of a step view model changes, trigger the auto-save.
            SaveChangesToSelectedPreset();

            // Also update the live sequence in the engine to reflect the change immediately.
            UpdateLiveSequence();
        }

        private void OnUserPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PresetModel item in e.NewItems)
                {
                    item.PropertyChanged += OnPresetPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (PresetModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnPresetPropertyChanged;
                }
            }

            // If the currently selected preset is removed, deselect it.
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems != null && e.OldItems.Contains(SelectedPreset))
                {
                    SelectedPreset = null;
                }
            }

            UpdateLiveSequence();
        }

        private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PresetModel.IsSelectedForLoop))
            {
                UpdateLiveSequence();
            }
        }

        private void RegisterPatterns()
        {
            var constantPattern = new ConstantPattern();
            _patternRegistry.Add(constantPattern.Name, constantPattern);
            AvailablePatternNames.Add(constantPattern.Name);

            var strokePattern = new StrokePattern();
            _patternRegistry.Add(strokePattern.Name, strokePattern);
            AvailablePatternNames.Add(strokePattern.Name);

            var midiFilePattern = new MidiFilePattern();
            _patternRegistry.Add(midiFilePattern.Name, midiFilePattern);
            AvailablePatternNames.Add(midiFilePattern.Name);
        }

        private void AddStep(object? parameter)
        {
            if (!UserPresets.Any())
            {
                AddPreset();
            }

            var defaultPatternName = AvailablePatternNames.FirstOrDefault();
            if (string.IsNullOrEmpty(defaultPatternName)) return;

            var defaultPattern = _patternRegistry[defaultPatternName];
            var newStepModel = new StepModel
            {
                PatternName = defaultPatternName,
                Config = defaultPattern.CreateDefaultConfiguration()
            };

            if (newStepModel.PatternName == "Constant")
            {
                newStepModel.Intensity = 60.0;
                newStepModel.MinIntensity = 60.0;
                newStepModel.MaxIntensity = 60.0;
            }

            var newStepViewModel = new StepViewModel(newStepModel, _patternRegistry, AvailablePatternNames);
            Steps.Add(newStepViewModel);
            SelectedStep = newStepViewModel;
        }

        private bool CanRemoveStep(object? parameter) => SelectedStep != null && Steps.Count > 0;
        private void RemoveStep(object? parameter)
        {
            if (SelectedStep != null)
            {
                var index = Steps.IndexOf(SelectedStep);
                Steps.Remove(SelectedStep);
                if (Steps.Count > 0)
                {
                    SelectedStep = Steps[Math.Min(index, Steps.Count - 1)];
                }
                else
                {
                    SelectedStep = null;
                }
            }
        }

        private void MoveStepUp(object? parameter)
        {
            if (SelectedStep != null)
            {
                int index = Steps.IndexOf(SelectedStep);
                if (index > 0)
                {
                    Steps.Move(index, index - 1);
                    UpdateLiveSequence();
                }
            }
        }

        private bool CanMoveStepUp(object? parameter)
        {
            if (SelectedStep == null) return false;
            return Steps.IndexOf(SelectedStep) > 0;
        }

        private void MoveStepDown(object? parameter)
        {
            if (SelectedStep != null)
            {
                int index = Steps.IndexOf(SelectedStep);
                if (index < Steps.Count - 1)
                {
                    Steps.Move(index, index + 1);
                    UpdateLiveSequence();
                }
            }
        }

        private bool CanMoveStepDown(object? parameter)
        {
            if (SelectedStep == null) return false;
            return Steps.IndexOf(SelectedStep) < Steps.Count - 1;
        }

        private T DeepClone<T>(T obj)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            string json = JsonConvert.SerializeObject(obj, settings);
            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        private void DuplicateStep(object? parameter)
        {
            if (SelectedStep == null) return;

            var index = Steps.IndexOf(SelectedStep);

            // Deep clone the model
            var clonedModel = DeepClone(SelectedStep.Model);

            var newStepViewModel = new StepViewModel(clonedModel, _patternRegistry, AvailablePatternNames);

            Steps.Insert(index + 1, newStepViewModel);
            SelectedStep = newStepViewModel;
        }

        private bool CanDuplicateStep(object? parameter)
        {
            return SelectedStep != null;
        }

        public void AddPreset(bool isSelectedForLoop = false)
        {
            var newPreset = new PresetModel
            {
                Name = $"Preset {UserPresets.Count + 1}",
                IsSelectedForLoop = isSelectedForLoop
            };
            UserPresets.Add(newPreset);
            SelectedPreset = newPreset; // Select the new preset
        }

        private void RemoveSelectedPreset()
        {
            if (SelectedPreset != null)
            {
                UserPresets.Remove(SelectedPreset);
            }
        }

        private bool CanManipulateSelectedPreset() => SelectedPreset != null;

        private void MoveSelectedPresetUp()
        {
            if (SelectedPreset != null)
            {
                int index = UserPresets.IndexOf(SelectedPreset);
                if (index > 0)
                {
                    UserPresets.Move(index, index - 1);
                    UpdateLiveSequence();
                }
            }
        }

        private bool CanMoveSelectedPresetUp()
        {
            if (SelectedPreset == null) return false;
            return UserPresets.IndexOf(SelectedPreset) > 0;
        }

        private void MoveSelectedPresetDown()
        {
            if (SelectedPreset != null)
            {
                int index = UserPresets.IndexOf(SelectedPreset);
                if (index < UserPresets.Count - 1)
                {
                    UserPresets.Move(index, index + 1);
                    UpdateLiveSequence();
                }
            }
        }

        private bool CanMoveSelectedPresetDown()
        {
            if (SelectedPreset == null) return false;
            return UserPresets.IndexOf(SelectedPreset) < UserPresets.Count - 1;
        }

        private void LoadStepsFromPreset(PresetModel? preset)
        {
            _isUpdatingSteps = true; // Set flag to prevent auto-saving during load

            // Unsubscribe from old step events
            foreach (var step in Steps)
            {
                step.PropertyChanged -= OnStepViewModelPropertyChanged;
            }
            Steps.Clear();

            if (preset != null)
            {
                foreach (var stepModel in preset.Steps)
                {
                    var stepViewModel = new StepViewModel(stepModel, _patternRegistry, AvailablePatternNames);
                    stepViewModel.PropertyChanged += OnStepViewModelPropertyChanged;
                    Steps.Add(stepViewModel);
                }
            }

            SelectedStep = Steps.FirstOrDefault();
            _isUpdatingSteps = false; // Unset flag
        }

        private void SaveChangesToSelectedPreset()
        {
            if (SelectedPreset == null || _isUpdatingSteps)
            {
                return; // Don't save if no preset is selected or if we are in the middle of loading
            }

            SelectedPreset.Steps.Clear();
            foreach (var step in Steps)
            {
                SelectedPreset.Steps.Add(step.Model);
            }
        }
        
        private List<PresetModel> GetPresetsForPlayback()
        {
            if (CurrentPlaybackMode == PresetPlaybackMode.Repeat)
            {
                return SelectedPreset != null ? new List<PresetModel> { SelectedPreset } : new List<PresetModel>();
            }

            var presets = UserPresets.Where(p => p.IsSelectedForLoop).ToList();

            if (CurrentPlaybackMode == PresetPlaybackMode.Random)
            {
                return presets.OrderBy(p => _random.Next()).ToList();
            }

            return presets;
        }

        private List<StepViewModel> BuildViewModelsFromPresets(List<PresetModel> presets)
        {
            var viewModels = new List<StepViewModel>();
            foreach (var preset in presets)
            {
                foreach (var stepModel in preset.Steps)
                {
                    viewModels.Add(new StepViewModel(stepModel, _patternRegistry, AvailablePatternNames));
                }
            }
            return viewModels;
        }

        private void UpdateLiveSequence()
        {
            if (!_engine.IsRunning) return;
 
            if (_isPlayingPresets)
            {
                // In preset playback mode, we need to update the playlist based on current selections
                // without disrupting the random order if in random mode.
                var selectedPresets = UserPresets.Where(p => p.IsSelectedForLoop).ToList();

                if (CurrentPlaybackMode == PresetPlaybackMode.Random)
                {
                    // Preserve the existing random order.
                    // Remove presets that are no longer selected.
                    var updatedPlaylist = _currentPresetPlaylist.Where(p => selectedPresets.Contains(p)).ToList();
                    // Find newly selected presets that are not yet in the playlist.
                    var newPresets = selectedPresets.Where(p => !updatedPlaylist.Contains(p));
                    // Add the new presets to the end of the current playlist.
                    updatedPlaylist.AddRange(newPresets);
                    _currentPresetPlaylist = updatedPlaylist;
                }
                else // Sequential or Repeat
                {
                    // For other modes, we can just rebuild the playlist from the current selection.
                    _currentPresetPlaylist = GetPresetsForPlayback();
                }

                if (_currentPresetPlaylist.Any())
                {
                    // In Random mode, OnSequenceLooped is responsible for updating the engine.
                    // This prevents this method from overwriting the single-preset loop with a giant list
                    // just because the user clicked on another preset in the UI.
                    if (CurrentPlaybackMode != PresetPlaybackMode.Random)
                    {
                        var sequenceToPlay = BuildViewModelsFromPresets(_currentPresetPlaylist);
                        _engine.UpdateSequence(sequenceToPlay, IsRandomized); 
                    }
                }
                else
                {
                    // We were playing presets, but now none are selected. Stop playback.
                    Stop(null);
                }
            }
            else
            {
                // Live editor playback is affected by the randomize toggle
                _engine.UpdateSequence(Steps.ToList(), IsRandomized);
            }
        }

        private bool CanPlay(object? parameter)
        {
            if (_engine.IsRunning) return false;

            if (CurrentPlaybackMode == PresetPlaybackMode.Repeat)
            {
                return SelectedPreset != null && SelectedPreset.Steps.Any();
            }
            return Steps.Any() || UserPresets.Any(p => p.IsSelectedForLoop);
        }

        private int _currentPlaylistIndex = 0;

        private void OnSequenceLooped()
        {
            System.Diagnostics.Debug.WriteLine("[OnSequenceLooped] Event received.");
            if (!_isPlayingPresets || CurrentPlaybackMode != PresetPlaybackMode.Random)
            {
                System.Diagnostics.Debug.WriteLine($"[OnSequenceLooped] Ignoring event. IsPlayingPresets: {_isPlayingPresets}, Mode: {CurrentPlaybackMode}");
                return;
            }

            if (_currentPresetPlaylist.Count <= 1)
            {
                System.Diagnostics.Debug.WriteLine("[OnSequenceLooped] Only one or zero presets in playlist, nothing to randomize.");
                // If there's one preset, the engine will loop it automatically.
                // If there are zero, this event shouldn't have been triggered.
                return;
            }

            // --- True Randomization Logic ---
            // Select a new random index that is different from the current one.
            int newIndex = _currentPlaylistIndex;
            while (newIndex == _currentPlaylistIndex)
            {
                newIndex = _random.Next(0, _currentPresetPlaylist.Count);
            }
            _currentPlaylistIndex = newIndex;
            // --- End of True Randomization Logic ---

            var nextPreset = _currentPresetPlaylist[_currentPlaylistIndex];
            System.Diagnostics.Debug.WriteLine($"[OnSequenceLooped] Randomly selected next preset at index {_currentPlaylistIndex}: {nextPreset.Name}");
            var sequenceToPlay = BuildViewModelsFromPresets(new List<PresetModel> { nextPreset });
            
            // Update the engine with the new sequence, but don't randomize the steps within it
            if (sequenceToPlay.Any())
            {
                 System.Diagnostics.Debug.WriteLine($"[OnSequenceLooped] Updating engine with {sequenceToPlay.Count} steps from {nextPreset.Name}.");
                 _engine.UpdateSequence(sequenceToPlay, IsRandomized);
            }
            else
            {
                // If the next preset has no steps, move to the one after it.
                // This prevents getting stuck on an empty preset.
                System.Diagnostics.Debug.WriteLine($"[OnSequenceLooped] Preset {nextPreset.Name} is empty, skipping to the next one.");
                OnSequenceLooped();
            }
        }

        private void Play(object? parameter)
        {
            System.Diagnostics.Debug.WriteLine($"[Play] Starting playback. Mode: {CurrentPlaybackMode}");
            List<StepViewModel> sequenceToPlay;
            bool loop = true;
            bool randomizeSteps = IsRandomized;

            _currentPresetPlaylist.Clear();
            _currentPlaylistIndex = 0;
            var selectedPresets = GetPresetsForPlayback();
            System.Diagnostics.Debug.WriteLine($"[Play] Got {selectedPresets.Count} presets for playback: {string.Join(", ", selectedPresets.Select(p => p.Name))}");


            if (!selectedPresets.Any())
            {
                _isPlayingPresets = false;
                sequenceToPlay = Steps.ToList();
                if (!sequenceToPlay.Any()) return;
            }
            else
            {
                _isPlayingPresets = true;
                _currentPresetPlaylist = selectedPresets;

                if (CurrentPlaybackMode == PresetPlaybackMode.Random)
                {
                    // For Random mode, play only the first preset in the shuffled list.
                    // The OnSequenceLooped event will handle playing the next ones.
                    var firstPreset = _currentPresetPlaylist.FirstOrDefault();
                    if (firstPreset == null) return;
                    System.Diagnostics.Debug.WriteLine($"[Play] Random mode detected. Starting with first preset: {firstPreset.Name}");
                    sequenceToPlay = BuildViewModelsFromPresets(new List<PresetModel> { firstPreset });
                }
                else
                {
                    // For Sequential and Repeat modes, play all selected presets as one continuous sequence.
                    System.Diagnostics.Debug.WriteLine($"[Play] {CurrentPlaybackMode} mode detected. Playing all presets sequentially.");
                    sequenceToPlay = BuildViewModelsFromPresets(_currentPresetPlaylist);
                }
            }

            if (!sequenceToPlay.Any())
            {
                System.Diagnostics.Debug.WriteLine("[Play] No sequence to play. Aborting.");
                return;
            }

            _engine.Play(sequenceToPlay, loop, randomizeSteps);

            OnPropertyChanged(nameof(IsPlaying));
            System.Diagnostics.Debug.WriteLine("[Play] Engine started.");
        }


        private void SavePresetToFile()
        {
            if (SelectedPreset == null) return;

            SaveChangesToSelectedPreset(); // Ensure current changes are in the model

            var sfd = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"{SelectedPreset.Name.TrimEnd('*')}.json"
            };

            if (sfd.ShowDialog() == true)
            {
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, Formatting = Formatting.Indented };
                string json = JsonConvert.SerializeObject(SelectedPreset, settings);
                File.WriteAllText(sfd.FileName, json);
                SelectedPreset.FilePath = sfd.FileName;
                SelectedPreset.ResetModified();
            }
        }

        public bool SavePreset(PresetModel preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.FilePath))
            {
                return false;
            }

            try
            {
                // Ensure the currently edited steps are part of the preset model before saving
                if (preset == SelectedPreset)
                {
                    SaveChangesToSelectedPreset();
                }

                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, Formatting = Formatting.Indented };
                string json = JsonConvert.SerializeObject(preset, settings);
                File.WriteAllText(preset.FilePath, json);
                preset.ResetModified();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving preset: {ex.Message}");
                return false;
            }
        }

        private void LoadPresetFromFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                if (LoadPresetFromPath(ofd.FileName))
                {
                    PresetFileLoaded?.Invoke(ofd.FileName);
                }
            }
        }

        public bool LoadPresetFromPath(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                var loadedPreset = JsonConvert.DeserializeObject<PresetModel>(json, settings);

                if (loadedPreset == null) return false;

                loadedPreset.Name = Path.GetFileNameWithoutExtension(filePath);
                loadedPreset.FilePath = filePath;

                UserPresets.Add(loadedPreset);
                SelectedPreset = loadedPreset; // This will trigger loading its steps into the view
                return true;
            }
            catch (Exception ex)
            {
                // Consider showing an error message to the user
                System.Diagnostics.Debug.WriteLine($"Error loading preset: {ex.Message}");
                return false;
            }
        }

        private bool CanStop(object? parameter) => _engine?.IsRunning ?? false;
        private void Stop(object? parameter)
        {
            _engine.Stop();
            _isPlayingPresets = false;
            _currentPresetPlaylist.Clear();
            OnPropertyChanged(nameof(IsPlaying));
            SequencerStopped?.Invoke();
        }

        public bool IsPlaying => _engine?.IsRunning ?? false;

        public int? GetCurrentSequencerValue()
        {
            return _engine.GetCurrentValue();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}