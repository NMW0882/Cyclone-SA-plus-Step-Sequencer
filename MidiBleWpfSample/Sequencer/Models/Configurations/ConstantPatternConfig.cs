using MidiBleWpfSample.Sequencer.Models;

namespace MidiBleWpfSample.Sequencer.Models.Configurations
{
    public class ConstantPatternConfig : PatternConfiguration
    {
        public override SequencePatternType PatternType => SequencePatternType.Constant;

        private StepRotationDirection _rotationDirection = StepRotationDirection.Clockwise;
        /// <summary>
        /// Gets or sets the direction of rotation.
        /// </summary>
        public StepRotationDirection RotationDirection
        {
            get => _rotationDirection;
            set
            {
                if (_rotationDirection == value) return;
                _rotationDirection = value;
                OnPropertyChanged();
            }
        }

        private bool _isRandomnessEnabled = false;
        /// <summary>
        /// Gets or sets a value indicating whether randomness is enabled.
        /// </summary>
        public bool IsRandomnessEnabled
        {
            get => _isRandomnessEnabled;
            set
            {
                if (_isRandomnessEnabled == value) return;
                _isRandomnessEnabled = value;
                OnPropertyChanged();
            }
        }

        private double _randomnessIntensity = 0.5;
        /// <summary>
        /// Gets or sets the intensity of the random fluctuations (0.0 to 1.0).
        /// Corresponds to "Velocity" or "Intensity".
        /// </summary>
        public double RandomnessIntensity
        {
            get => _randomnessIntensity;
            set
            {
                if (_randomnessIntensity == value) return;
                _randomnessIntensity = value;
                OnPropertyChanged();
            }
        }

        private double _springStiffness = 0.5;
        /// <summary>
        /// Gets or sets the strength of the spring force pulling the value back to the center (0.0 to 1.0).
        /// Corresponds to "Stability" or "Spring Force".
        /// </summary>
        public double SpringStiffness
        {
            get => _springStiffness;
            set
            {
                if (_springStiffness == value) return;
                _springStiffness = value;
                OnPropertyChanged();
            }
        }
    }
}

