using System.Collections.Generic;

namespace MidiBleWpfSample.Sequencer.Models
{
    /// <summary>
    /// Represents a full sequence, which is a list of steps.
    /// </summary>
    public class SequenceModel
    {
        /// <summary>
        /// The name of the sequence.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The list of steps that make up this sequence.
        /// </summary>
        public List<StepModel> Steps { get; set; } = new List<StepModel>();
    }
}
