using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Models.Configurations;

namespace MidiBleWpfSample.Sequencer.Patterns
{
    public class MidiFilePattern : ISequencePattern
    {
        public string Name => "Midi File";

        private List<(long time, byte value)> _midiEvents = new();
        private long _totalTicks = 0;
        private string? _loadedFilePath;
        private TempoMap? _tempoMap;
        private byte _minOriginalValue = 0;
        private byte _maxOriginalValue = 127;

        public PatternConfiguration CreateDefaultConfiguration()
        {
            return new MidiFilePatternConfig();
        }

        private void LoadMidiFile(string filePath)
        {
            _loadedFilePath = filePath;
            _midiEvents.Clear();
            _totalTicks = 0;
            _tempoMap = null;
            _minOriginalValue = 127;
            _maxOriginalValue = 0;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                var midiFile = MidiFile.Read(filePath);
                _tempoMap = midiFile.GetTempoMap();

                var timedEvents = midiFile.GetTimedEvents()
                    .Where(e => e.Event.EventType == MidiEventType.ControlChange)
                    .Select(e => new { e.Time, Event = (ControlChangeEvent)e.Event })
                    .Where(e => e.Event.ControlNumber == 10)
                    .ToList();

                if (!timedEvents.Any())
                {
                    return; // No CC10 events found
                }

                foreach (var timedEvent in timedEvents)
                {
                    _midiEvents.Add((timedEvent.Time, timedEvent.Event.ControlValue));
                    if (timedEvent.Event.ControlValue < _minOriginalValue) _minOriginalValue = timedEvent.Event.ControlValue;
                    if (timedEvent.Event.ControlValue > _maxOriginalValue) _maxOriginalValue = timedEvent.Event.ControlValue;
                }
                
                if (_minOriginalValue > _maxOriginalValue)
                {
                    _minOriginalValue = 0;
                    _maxOriginalValue = 127;
                }

                _totalTicks = midiFile.GetDuration<MidiTimeSpan>().TimeSpan;

                if (_midiEvents.Any() && _midiEvents.Last().time < _totalTicks)
                {
                    _midiEvents.Add((_totalTicks, _midiEvents.Last().value));
                }
            }
            catch (Exception)
            {
                // Ignore errors for now
                _midiEvents.Clear();
            }
        }

        public int GetValue(int currentTick, int totalTicksInStep, StepModel step)
        {
            if (step.Config is not MidiFilePatternConfig config)
            {
                return 64;
            }

            if (config.FilePath != _loadedFilePath)
            {
                LoadMidiFile(config.FilePath);
            }

            if (!_midiEvents.Any() || _totalTicks == 0 || _tempoMap == null)
            {
                return 64; // No data to play
            }

            // The user wants Speed=100 to be 1.0x playback speed.
            double speedMultiplier = config.Speed / 100.0;

            // Prevent division by zero or negative speed.
            if (speedMultiplier <= 0)
            {
                speedMultiplier = 0.001; // A very small number to prevent freezing but effectively pause.
            }

            // The timer interval is 50ms (0.05s) in SequencerEngine
            const double tickDurationSeconds = 0.05;
            double elapsedSeconds = currentTick * tickDurationSeconds;
            var timeAsTimeSpan = TimeSpan.FromSeconds(elapsedSeconds * speedMultiplier);

            long currentMidiTick = TimeConverter.ConvertFrom(new MetricTimeSpan(timeAsTimeSpan), _tempoMap);
            long loopedTick = currentMidiTick % _totalTicks;

            var nextEventIndex = _midiEvents.FindIndex(e => e.time > loopedTick);
            if (nextEventIndex == -1)
            {
                // If loopedTick is beyond the last event, return the last event's value
                return (int)Math.Clamp(Math.Round(HandleValueScaling(_midiEvents.Last().value, config)), 0, 127);
            }

            var prevEventIndex = nextEventIndex > 0 ? nextEventIndex - 1 : 0;
            var prevEvent = _midiEvents[prevEventIndex];
            var nextEvent = _midiEvents[nextEventIndex];

            double interpolatedValue;
            if (prevEvent.time >= nextEvent.time)
            {
                interpolatedValue = prevEvent.value;
            }
            else
            {
                double ratio = (double)(loopedTick - prevEvent.time) / (nextEvent.time - prevEvent.time);
                interpolatedValue = prevEvent.value + ratio * (nextEvent.value - prevEvent.value);
            }

            return (int)Math.Clamp(Math.Round(HandleValueScaling(interpolatedValue, config)), 0, 127);
        }

        private double HandleValueScaling(double value, MidiFilePatternConfig config)
        {
            // --- Amplitude Scaling Logic ---
            // Scale the amplitude of the MIDI's CC values around a fixed center (63.5).
            double fixedCenter = 63.5;

            // How far the current value is from the fixed center
            double deviation = value - fixedCenter;

            // Scale the deviation by the amplitude slider
            double scaledDeviation = deviation * config.Amplitude;

            // Add the scaled deviation back to the center
            double finalValue = fixedCenter + scaledDeviation;

            if (config.Invert)
            {
                finalValue = 127 - finalValue;
            }

            return finalValue;
        }

        public void Reset()
        {
            // This pattern is stateless regarding playback progress.
            // The state it holds (the loaded file data) is managed internally
            // when the file path changes.
        }

        public static TimeSpan GetMidiFileDuration(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return TimeSpan.Zero;
            }

            try
            {
                var midiFile = MidiFile.Read(filePath);
                return midiFile.GetDuration<MetricTimeSpan>();
            }
            catch (Exception)
            {
                return TimeSpan.Zero;
            }
        }
    }
}
