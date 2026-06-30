using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MidiBleWpfSample.Sequencer.Models
{
    public abstract class PatternConfiguration : INotifyPropertyChanged
    {
        public abstract SequencePatternType PatternType { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}