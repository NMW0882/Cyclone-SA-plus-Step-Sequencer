using System;
using System.Collections.Generic;
using System.Linq;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Models.Configurations;
using MidiBleWpfSample.Sequencer.ViewModels;
using MidiBleWpfSample.Timers;

namespace MidiBleWpfSample.Sequencer.Engine
{
    public class SequencerEngine : IDisposable
    {
        private const int TimerIntervalMilliseconds = 50;

        private readonly Dictionary<string, ISequencePattern> _patternRegistry;
        private readonly HighPrecisionTimer _timer;
        private readonly Random _random = new Random();

        private IReadOnlyList<StepViewModel> _currentSteps = new List<StepViewModel>();
        private int _currentStepIndex = -1;
        private int _previousStepIndex = -1;
        private int _currentTickInStep = 0;
        private bool _loopSequence = false;
        private int? _lastMidiValue = null;

        private bool _isRandomized = false;
        private List<int> _shuffledIndices = new List<int>();
        private int _shuffledIndexPosition = -1;

        private int _currentRepetitionCount = 0;
        private int _targetRepetitionCount = 1;

        private StepRotationDirection _lastActualDirection = StepRotationDirection.Clockwise;
        private StepRotationDirection _currentStepResolvedDirection = StepRotationDirection.Clockwise;

        public bool IsRunning { get; private set; }
        public event Action<int>? MidiValueGenerated;
        public event Action<int>? StepIndexChanged;
        public event Action<int>? StepEvaluated;
        public event Action? SequenceLooped;

        public SequencerEngine(Dictionary<string, ISequencePattern> patternRegistry)
        {
            _patternRegistry = patternRegistry ?? new Dictionary<string, ISequencePattern>();
            _timer = new HighPrecisionTimer(TimerIntervalMilliseconds, OnTimerTick);
        }

        public void Play(IReadOnlyList<StepViewModel> steps, bool loop, bool isRandomized)
        {
            if (steps == null || steps.Count == 0)
            {
                Stop();
                return;
            }

            _currentSteps = steps;
            _currentStepIndex = -1;
            _currentTickInStep = 0;
            _loopSequence = loop;
            _isRandomized = isRandomized;
            _lastMidiValue = null;
            IsRunning = true;

            if (_isRandomized)
            {
                BuildAndShuffleIndices();
                _shuffledIndexPosition = -1;
            }

            // First, set up the sequence and resolve the first step.
            MoveToNextStep();
            // Then, start the timer. This avoids a race condition on the first tick.
            _timer.Start();
        }

        public void Stop()
        {
            if (!IsRunning) return; // Prevent re-entry

            // Send the stop value directly from the engine, BEFORE setting IsRunning to false.
            MidiValueGenerated?.Invoke(64);

            IsRunning = false;
            _timer.Stop();

            // Reset state
            _previousStepIndex = -1;
            _currentStepIndex = -1;
            _currentTickInStep = 0;
            _lastMidiValue = null;
            _lastActualDirection = StepRotationDirection.Clockwise;
            StepIndexChanged?.Invoke(-1);
        }

        public void UpdateSequence(IReadOnlyList<StepViewModel> newSteps, bool isRandomized)
        {
            if (!IsRunning) return;

            if (newSteps == null || newSteps.Count == 0)
            {
                Stop();
                return;
            }

            _currentSteps = newSteps;
            _isRandomized = isRandomized;

            if (_isRandomized)
            {
                BuildAndShuffleIndices();
                if (!_shuffledIndices.Contains(_currentStepIndex))
                {
                    _currentTickInStep = int.MaxValue;
                }
            }
            else
            {
                if (_currentStepIndex >= _currentSteps.Count)
                {
                    _currentTickInStep = int.MaxValue;
                }
            }
        }

        private void BuildAndShuffleIndices()
        {
            _shuffledIndices = Enumerable.Range(0, _currentSteps.Count)
                                         .Where(i => _currentSteps[i].IsEnabled)
                                         .ToList();
            
            int n = _shuffledIndices.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (_shuffledIndices[k], _shuffledIndices[n]) = (_shuffledIndices[n], _shuffledIndices[k]);
            }
        }

        private void OnTimerTick()
        {
            if (!IsRunning || _currentSteps == null || _currentSteps.Count == 0 || _currentStepIndex < 0 || _currentStepIndex >= _currentSteps.Count)
            {
                MoveToNextStep();
                if (!IsRunning) return;
            }
            
            var currentStep = _currentSteps[_currentStepIndex];
            int totalTicksInStep = CalculateTotalTicksForStep(currentStep);

            if (_currentTickInStep >= totalTicksInStep)
            {
                _currentRepetitionCount++;
                if (_currentRepetitionCount >= _targetRepetitionCount)
                {
                    MoveToNextStep();
                }
                else
                {
                    _currentTickInStep = 0;
                    ResolveDirectionForCurrentStep();
                    if (currentStep.Model?.Config is StrokePatternConfig strokeConfigForRepetition)
                    {
                        ResolveStrokePatternRandomValues(strokeConfigForRepetition);
                        StepEvaluated?.Invoke(_currentStepIndex);
                    }
                    else if (currentStep.Model?.Config is not MidiFilePatternConfig)
                    {
                        ResolveCommonRandomValues(currentStep);
                        StepEvaluated?.Invoke(_currentStepIndex);
                    }
                    if (_patternRegistry.TryGetValue(currentStep.PatternName, out var patternToReset))
                    {
                        patternToReset.Reset();
                    }
                }

                if (!IsRunning) return;
                currentStep = _currentSteps[_currentStepIndex];
            }

            if (_currentStepIndex != _previousStepIndex)
            {
                _previousStepIndex = _currentStepIndex;
            }

            if (!_patternRegistry.TryGetValue(currentStep.PatternName, out var pattern))
            {
                return; 
            }

            totalTicksInStep = CalculateTotalTicksForStep(currentStep);
            int rawValue = pattern.GetValue(_currentTickInStep, totalTicksInStep, currentStep.Model);
            int finalValue = rawValue;

            if (_currentStepResolvedDirection == StepRotationDirection.Counterclockwise)
            {
                finalValue = 128 - rawValue;
            }

            if (_lastMidiValue.HasValue && _lastMidiValue.Value == finalValue)
            {
                _currentTickInStep++;
                return;
            }

            if (!IsRunning) return;

            _lastMidiValue = finalValue;
            MidiValueGenerated?.Invoke(finalValue);
            _currentTickInStep++;
        }

        private int CalculateTotalTicksForStep(StepViewModel step)
        {
            double stepDurationMs;
            if (step.Model.Config is StrokePatternConfig strokeConfig)
            {
                stepDurationMs = (strokeConfig.Duration + strokeConfig.HoldDuration) * 1000;
            }
            else
            {
                stepDurationMs = step.Duration.TotalMilliseconds;
            }
            return (int)Math.Max(1, stepDurationMs / TimerIntervalMilliseconds);
        }

        private void ResolveDirectionForCurrentStep()
        {
            var currentStep = _currentSteps[_currentStepIndex];
            var requestedDirection = StepRotationDirection.Clockwise; // Default

            if (currentStep.Model.Config is StrokePatternConfig strokeConfig)
            {
                requestedDirection = strokeConfig.RotationDirection;
            }
            else if (currentStep.Model.Config is ConstantPatternConfig constantConfig)
            {
                requestedDirection = constantConfig.RotationDirection;
            }

            switch (requestedDirection)
            {
                case StepRotationDirection.Clockwise:
                    _currentStepResolvedDirection = StepRotationDirection.Clockwise;
                    break;
                case StepRotationDirection.Counterclockwise:
                    _currentStepResolvedDirection = StepRotationDirection.Counterclockwise;
                    break;
                case StepRotationDirection.Alternate:
                    _currentStepResolvedDirection = (_lastActualDirection == StepRotationDirection.Clockwise)
                        ? StepRotationDirection.Counterclockwise
                        : StepRotationDirection.Clockwise;
                    break;
                case StepRotationDirection.Random:
                    _currentStepResolvedDirection = (_random.Next(0, 2) == 0)
                        ? StepRotationDirection.Clockwise
                        : StepRotationDirection.Counterclockwise;
                    break;
            }
            _lastActualDirection = _currentStepResolvedDirection;
        }

        private void ResolveStrokePatternRandomValues(StrokePatternConfig config)
        {
            if (config == null) return;

            // StartValue
            if (config.MinStartValue != config.MaxStartValue)
            {
                int min = Math.Min(config.MinStartValue, config.MaxStartValue);
                int max = Math.Max(config.MinStartValue, config.MaxStartValue);
                config.StartValue = _random.Next(min, max + 1);
            }

            // Baseline
            if (config.MinBaseline != config.MaxBaseline)
            {
                int min = Math.Min(config.MinBaseline, config.MaxBaseline);
                int max = Math.Max(config.MinBaseline, config.MaxBaseline);
                config.Baseline = _random.Next(min, max + 1);
            }

            // Duration
            if (config.MinDuration != config.MaxDuration)
            {
                double min = Math.Min(config.MinDuration, config.MaxDuration);
                double max = Math.Max(config.MinDuration, config.MaxDuration);
                config.Duration = min + (_random.NextDouble() * (max - min));
            }

            // HoldDuration
            if (config.MinHoldDuration != config.MaxHoldDuration)
            {
                double min = Math.Min(config.MinHoldDuration, config.MaxHoldDuration);
                double max = Math.Max(config.MinHoldDuration, config.MaxHoldDuration);
                config.HoldDuration = min + (_random.NextDouble() * (max - min));
            }

            // CurveShape
            if (config.MinCurveShape != config.MaxCurveShape)
            {
                double min = Math.Min(config.MinCurveShape, config.MaxCurveShape);
                double max = Math.Max(config.MinCurveShape, config.MaxCurveShape);
                config.CurveShape = min + (_random.NextDouble() * (max - min));
            }
        }

        private void ResolveCommonRandomValues(StepViewModel step)
        {
            if (step == null) return;

            // Common Duration
            if (step.Model.MinDuration != step.Model.MaxDuration)
            {
                double min = step.Model.MinDuration.TotalSeconds;
                double max = step.Model.MaxDuration.TotalSeconds;
                if (min > max) (min, max) = (max, min);
                double randomSeconds = min + (_random.NextDouble() * (max - min));
                step.DurationInSeconds = randomSeconds;
            }

            // Common Intensity
            if (step.Model.MinIntensity != step.Model.MaxIntensity)
            {
                double min = step.Model.MinIntensity;
                double max = step.Model.MaxIntensity;
                if (min > max) (min, max) = (max, min);
                step.Model.Intensity = min + (_random.NextDouble() * (max - min));
            }
        }

        private void MoveToNextStep()
        {
            if (_currentSteps == null || _currentSteps.Count == 0) { Stop(); return; }

            for (int i = 0; i < _currentSteps.Count; i++)
            {
                int nextIndex = -1;
                if (_isRandomized)
                {
                    _shuffledIndexPosition++;
                    if (_shuffledIndexPosition >= _shuffledIndices.Count)
                    {
                                                                if (_loopSequence)
                                                                {
                                                                    System.Diagnostics.Debug.WriteLine("[Engine] Random sequence looped.");
                                                                    SequenceLooped?.Invoke();
                                                                    BuildAndShuffleIndices();
                                                                    if (_shuffledIndices.Count == 0) { Stop(); return; }
                                                                    _shuffledIndexPosition = 0;
                                                                }                        else
                        {
                            Stop(); return;
                        }
                    }
                    nextIndex = _shuffledIndices[_shuffledIndexPosition];
                }
                else // Sequential
                {
                    int searchIndex = _currentStepIndex;
                    if (searchIndex < 0) searchIndex = -1;

                    bool found = false;
                    for (int j = 0; j < _currentSteps.Count; j++)
                    {
                        searchIndex++;
                        if (searchIndex >= _currentSteps.Count)
                        {
                            if (_loopSequence) { System.Diagnostics.Debug.WriteLine("[Engine] Sequential sequence looped."); SequenceLooped?.Invoke(); searchIndex = 0; }
                            else { Stop(); return; }
                        }
                        if (_currentSteps[searchIndex].IsEnabled)
                        {
                            nextIndex = searchIndex;
                            found = true;
                            break;
                        }
                    }
                    if (!found) { Stop(); return; }
                }

                var candidateStep = _currentSteps[nextIndex];

                if (candidateStep.Model.Config is StrokePatternConfig strokeConfigForNewStep)
                {
                    ResolveStrokePatternRandomValues(strokeConfigForNewStep);
                    StepEvaluated?.Invoke(nextIndex);
                }
                else if (!(candidateStep.Model.Config is MidiFilePatternConfig))
                {
                    ResolveCommonRandomValues(candidateStep);
                    StepEvaluated?.Invoke(nextIndex);
                }
                
                int reps;
                if (candidateStep.Model.Config is StrokePatternConfig strokeConfig)
                {
                    // Use random repetitions if a range is defined
                    if (strokeConfig.MinRepetitions != strokeConfig.MaxRepetitions)
                    {
                        int min = strokeConfig.MinRepetitions;
                        int max = strokeConfig.MaxRepetitions;
                        if (min > max) (min, max) = (max, min); // Ensure min <= max
                        reps = (min == max) ? min : _random.Next(min, max + 1);
                    }
                    else 
                    { 
                        reps = strokeConfig.Repetitions; 
                    }

                    // If playing a single enabled stroke pattern and repetitions is 0, treat as 1.
                    if (_currentSteps.Count(s => s.IsEnabled) == 1 && reps == 0)
                    {
                        reps = 1;
                    }
                }
                else 
                { 
                    reps = 1; 
                }

                if (reps > 0)
                {
                    _currentStepIndex = nextIndex;
                    _targetRepetitionCount = reps;
                    _currentTickInStep = 0;
                    _currentRepetitionCount = 0;
                    ResolveDirectionForCurrentStep();
                    if (_patternRegistry.TryGetValue(candidateStep.PatternName, out var pattern))
                    {
                        pattern.Reset();
                    }
                    StepIndexChanged?.Invoke(_currentStepIndex);
                    return;
                }
                
                if (_isRandomized)
                {
                    continue;
                }
                else
                {
                    _currentStepIndex = nextIndex;
                }
            }
            
            Stop();
        }

        public int? GetCurrentValue()
        {
            if (!IsRunning || _currentSteps == null || _currentSteps.Count == 0 || _currentStepIndex < 0 || _currentStepIndex >= _currentSteps.Count)
            {
                return null;
            }

            var currentStep = _currentSteps[_currentStepIndex];
            if (!_patternRegistry.TryGetValue(currentStep.PatternName, out var pattern))
            {
                return null;
            }

            int totalTicksInStep = CalculateTotalTicksForStep(currentStep);

            // Use the current tick, but don't advance it here.
            // Clamp the tick to the last valid tick of the step to avoid out-of-bounds issues in patterns.
            int tick = Math.Min(_currentTickInStep, totalTicksInStep > 0 ? totalTicksInStep - 1 : 0);

            int rawValue = pattern.GetValue(tick, totalTicksInStep, currentStep.Model);
            int finalValue = rawValue;

            // Apply the already resolved direction for the current step
            if (_currentStepResolvedDirection == StepRotationDirection.Counterclockwise)
            {
                finalValue = 128 - rawValue;
            }

            return finalValue;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}