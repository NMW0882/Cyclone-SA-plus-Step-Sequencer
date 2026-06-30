using System;
using MidiBleWpfSample.Sequencer.Models;
using MidiBleWpfSample.Sequencer.Models.Configurations;

namespace MidiBleWpfSample.Sequencer.Patterns
{
    public class StrokePattern : ISequencePattern
    {
        public string Name => "Stroke";

        public PatternConfiguration CreateDefaultConfiguration()
        {
            return new StrokePatternConfig();
        }

        public void Reset()
        {
        }

        public int GetValue(int currentTick, int totalTicksInStep, StepModel step)
        {
            if (step.Config is not StrokePatternConfig config)
            {
                return 64; // Default neutral value
            }

            // --- Calculate stroke parameters ---
            double startValue = config.StartValue;
            double effectiveBaseline = config.Baseline;
            double scaledRange = effectiveBaseline - startValue;
            int endValue = (int)Math.Round(Math.Clamp(effectiveBaseline, 0, 127));

            // --- Calculate ticks for various durations ---
            const double tickDurationSeconds = 0.05; // Timer interval from SequencerEngine

            // User-defined hold at the end of the stroke
            int endHoldDurationTicks = (int)(config.HoldDuration / tickDurationSeconds);

            // Hardcoded 50ms hold at the start of the stroke
            const int startHoldDurationTicks = 1; // 1 tick * 0.05s/tick = 50ms

            // The stroke's moving part is the total duration minus start and end holds.
            int movingPartDurationTicks = Math.Max(1, totalTicksInStep - startHoldDurationTicks - endHoldDurationTicks);

            int movingPartStartTick = startHoldDurationTicks;
            int endHoldStartTick = startHoldDurationTicks + movingPartDurationTicks;

            // --- State Machine ---
            if (currentTick < movingPartStartTick)
            {
                // 1. We are in the initial 'hold' phase
                return (int)Math.Round(startValue);
            }
            else if (currentTick >= endHoldStartTick)
            {
                // 3. We are in the final 'hold' phase
                return endValue;
            }
            else
            {
                // 2. We are in the 'moving' phase
                int tickInMovingPart = currentTick - movingPartStartTick;
                double progress = movingPartDurationTicks > 0 ? (double)tickInMovingPart / movingPartDurationTicks : 1.0;
                double finalValue;

                if (config.WaveformType == WaveformType.Square)
                {
                    // Simple square wave: start value for most of the duration, then jump to end value
                    finalValue = (progress < 1.0) ? startValue : effectiveBaseline;
                }
                else // Curve
                {
                    double curvedProgress;
                    if (progress >= 1.0)
                    {
                        curvedProgress = 1.0;
                    }
                    else
                    {
                        // CurveShape calculation. config.CurveShape is 0.0-1.0.
                        // 0.0 = concave (slow start), 0.5 = linear, 1.0 = convex (fast start)
                        double power = Math.Pow(4, 0.5 - config.CurveShape);
                        curvedProgress = Math.Pow(progress, power);
                    }
                    finalValue = startValue + (scaledRange * curvedProgress);
                }

                // Clamp the final value to the valid MIDI range
                double calculatedValue = Math.Clamp(finalValue, 0, 127);
                return (int)Math.Round(calculatedValue);
            }
        }
    }
}