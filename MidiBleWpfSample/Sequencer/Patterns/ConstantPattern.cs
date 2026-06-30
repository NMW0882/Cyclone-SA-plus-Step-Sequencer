using System;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Models.Configurations;

namespace MidiBleWpfSample.Sequencer.Patterns
{
    public class ConstantPattern : ISequencePattern
    {
        public string Name => "Constant";

        private readonly Random _random = new Random();
        private double _currentValue = 64.0;
        private static readonly TimeSpan ResetThreshold = TimeSpan.FromMilliseconds(50);

        public PatternConfiguration CreateDefaultConfiguration()
        {
            return new ConstantPatternConfig();
        }

        public int GetValue(int currentTick, int totalTicksInStep, StepModel step)
        {
            if (step.Config is not ConstantPatternConfig config)
            {
                return 64; // Return center value if config is invalid
            }

            // Calculate the non-random center value
            double scale = step.Intensity / 100.0;
            // The direction is now handled by the SequencerEngine.
            // This pattern always calculates as if it's clockwise.
            double deviation = 63.5 * scale;
            double centerValue = 64 + deviation;

            if (!config.IsRandomnessEnabled)
            {
                return (int)Math.Clamp(Math.Round(centerValue), 0, 127);
            }

            // --- Randomness Logic ---

            // Reset if it's the beginning of a new step
            if (currentTick == 0)
            {
                _currentValue = centerValue;
            }

            // 1. Brownian motion (random step)
            // The intensity is scaled by a factor to make the slider feel more responsive.
            // A larger multiplier means the "Intensity" slider has a more pronounced effect.
            double randomStep = (_random.NextDouble() * 2.0 - 1.0) * config.RandomnessIntensity * 5.0; // Max change of 5 units per tick

            // 2. Spring force
            double displacement = _currentValue - centerValue;
            // The stiffness is scaled to provide a gentle pull.
            double springForce = -displacement * config.SpringStiffness * 0.1;

            // 3. Update value
            _currentValue += randomStep + springForce;

            // 4. Clamp and return
            return (int)Math.Clamp(Math.Round(_currentValue), 0, 127);
        }

        public void Reset()
        {
            _currentValue = 64.0;
        }
    }
}
