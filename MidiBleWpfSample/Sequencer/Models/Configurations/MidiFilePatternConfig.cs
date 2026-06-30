using MidiBleWpfSample.Sequencer.Models.Configurations;

namespace MidiBleWpfSample.Sequencer.Models.Configurations
{
    public class MidiFilePatternConfig : PatternConfiguration
    {
        public override SequencePatternType PatternType => SequencePatternType.MidiFile;

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath == value) return;
                _filePath = value;
                OnPropertyChanged();
            }
        }

        private double _amplitude = 1.0;
        public double Amplitude
        {
            get => _amplitude;
            set
            {
                if (_amplitude == value) return;
                _amplitude = value;
                OnPropertyChanged();
            }
        }

        private bool _invert = false;
        public bool Invert
        {
            get => _invert;
            set
            {
                if (_invert == value) return;
                _invert = value;
                OnPropertyChanged();
            }
        }

        private double _speed = 100.0;
        public double Speed
        {
            get => _speed;
            set
            {
                if (_speed == value) return;
                _speed = value;
                OnPropertyChanged();
            }
        }
    }
}
