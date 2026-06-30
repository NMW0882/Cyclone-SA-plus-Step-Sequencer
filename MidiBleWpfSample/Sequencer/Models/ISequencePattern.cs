using System;

namespace MidiBleWpfSample.Sequencer.Models
{
    /// <summary>
    /// Represents the contract for all sequence patterns (generators and players).
    /// </summary>
    public interface ISequencePattern
    {
        /// <summary>
        /// The name to be displayed in the UI.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a default configuration object for this pattern.
        /// </summary>
        /// <returns>A new instance of a PatternConfiguration subclass.</returns>
        PatternConfiguration CreateDefaultConfiguration();

        /// <param name="currentTick">The current tick within the step.</param>
        /// <param name="totalTicksInStep">The total number of ticks for the current step's duration.</param>
        /// <param name="step">The step model containing configuration.</param>
        /// <returns>The calculated MIDI value (0-127).</returns>
        int GetValue(int currentTick, int totalTicksInStep, StepModel step);

        /// <summary>
        /// Resets the internal state of the pattern.
        /// </summary>
        void Reset();
    }
}
