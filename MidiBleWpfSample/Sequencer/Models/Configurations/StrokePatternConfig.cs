using MidiBleWpfSample.Sequencer.Models;
using System;

namespace MidiBleWpfSample.Sequencer.Models.Configurations
{
    public class StrokePatternConfig : PatternConfiguration
    {
        public override SequencePatternType PatternType => SequencePatternType.Stroke;

        private int _startValue = 127;
        public int StartValue
        {
            get => _startValue;
            set
            {
                if (_startValue == value) return;
                _startValue = value;
                OnPropertyChanged();
            }
        }

        private int _minStartValue = 127;
        public int MinStartValue
        {
            get => _minStartValue;
            set
            {
                if (_minStartValue == value) return;
                _minStartValue = value;
                OnPropertyChanged();
            }
        }

        private int _maxStartValue = 127;
        public int MaxStartValue
        {
            get => _maxStartValue;
            set
            {
                if (_maxStartValue == value) return;
                _maxStartValue = value;
                OnPropertyChanged();
            }
        }

        private int _baseline = 64;
        public int Baseline
        {
            get => _baseline;
            set
            {
                if (_baseline == value) return;
                _baseline = value;
                OnPropertyChanged();
            }
        }

        private int _minBaseline = 64;
        public int MinBaseline
        {
            get => _minBaseline;
            set
            {
                if (_minBaseline == value) return;
                _minBaseline = value;
                OnPropertyChanged();
            }
        }

        private int _maxBaseline = 64;
        public int MaxBaseline
        {
            get => _maxBaseline;
            set
            {
                if (_maxBaseline == value) return;
                _maxBaseline = value;
                OnPropertyChanged();
            }
        }

        private WaveformType _waveformType = WaveformType.Curve;
        public WaveformType WaveformType
        {
            get => _waveformType;
            set
            {
                if (_waveformType == value) return;
                _waveformType = value;
                OnPropertyChanged();
            }
        }

        private double _curveShape = 0.5;
        public double CurveShape
        {
            get => _curveShape;
            set
            {
                if (_curveShape == value) return;
                _curveShape = value;
                OnPropertyChanged();
            }
        }

        private double _minCurveShape = 0.5;
        public double MinCurveShape
        {
            get => _minCurveShape;
            set
            {
                if (_minCurveShape == value) return;
                _minCurveShape = value;
                OnPropertyChanged();
            }
        }

        private double _maxCurveShape = 0.5;
        public double MaxCurveShape
        {
            get => _maxCurveShape;
            set
            {
                if (_maxCurveShape == value) return;
                _maxCurveShape = value;
                OnPropertyChanged();
            }
        }

        private double _duration = 1.0;
        public double Duration
        {
            get => _duration;
            set
            {
                if (_duration == value) return;
                _duration = value;
                OnPropertyChanged();
            }
        }

        private double _minDuration = 1.0;
        public double MinDuration
        {
            get => _minDuration;
            set
            {
                if (_minDuration == value) return;
                _minDuration = value;
                OnPropertyChanged();
            }
        }

        private double _maxDuration = 1.0;
        public double MaxDuration
        {
            get => _maxDuration;
            set
            {
                if (_maxDuration == value) return;
                _maxDuration = value;
                OnPropertyChanged();
            }
        }

        private double _holdDuration = 0.1;
        public double HoldDuration
        {
            get => _holdDuration;
            set
            {
                if (_holdDuration == value) return;
                _holdDuration = value;
                OnPropertyChanged();
            }
        }

        private double _minHoldDuration = 0.1;
        public double MinHoldDuration
        {
            get => _minHoldDuration;
            set
            {
                if (_minHoldDuration == value) return;
                _minHoldDuration = value;
                OnPropertyChanged();
            }
        }

        private double _maxHoldDuration = 0.1;
        public double MaxHoldDuration
        {
            get => _maxHoldDuration;
            set
            {
                if (_maxHoldDuration == value) return;
                _maxHoldDuration = value;
                OnPropertyChanged();
            }
        }

        private int _repetitions = 1;
        public int Repetitions
        {
            get => _repetitions;
            set
            {
                if (_repetitions == value) return;
                _repetitions = value;
                OnPropertyChanged();
            }
        }

        private int _minRepetitions = 1;
        public int MinRepetitions
        {
            get => _minRepetitions;
            set
            {
                if (_minRepetitions == value) return;
                _minRepetitions = value;
                OnPropertyChanged();
            }
        }

        private int _maxRepetitions = 1;
        public int MaxRepetitions
        {
            get => _maxRepetitions;
            set
            {
                if (_maxRepetitions == value) return;
                _maxRepetitions = value;
                OnPropertyChanged();
            }
        }

        private StepRotationDirection _rotationDirection = StepRotationDirection.Clockwise;
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
    }
}
