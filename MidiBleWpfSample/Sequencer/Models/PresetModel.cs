using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MidiBleWpfSample.Sequencer.Models;
using Newtonsoft.Json;

namespace MidiBleWpfSample.Sequencer.Models
{
    public class PresetModel : INotifyPropertyChanged
    {
        private string _name = "New Preset";
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelectedForLoop;
        public bool IsSelectedForLoop
        {
            get => _isSelectedForLoop;
            set
            {
                if (_isSelectedForLoop == value) return;
                _isSelectedForLoop = value;
                OnPropertyChanged();
                MarkAsModified();
            }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        private bool _isModified;
        [JsonIgnore]
        public bool IsModified
        {
            get => _isModified;
            private set
            {
                if (_isModified == value) return;
                _isModified = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public string FilePath { get; set; } = string.Empty;

        public ObservableCollection<StepModel> Steps { get; private set; }

        public PresetModel()
        {
            Steps = new ObservableCollection<StepModel>();
            Steps.CollectionChanged += Steps_CollectionChanged;
        }

        private void Steps_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (StepModel item in e.NewItems)
                {
                    item.PropertyChanged += Step_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (StepModel item in e.OldItems)
                {
                    item.PropertyChanged -= Step_PropertyChanged;
                }
            }
            MarkAsModified();
        }

        private void Step_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkAsModified();
        }

        public void MarkAsModified()
        {
            if (IsModified) return;

            IsModified = true;
            if (!Name.EndsWith("*"))
            {
                Name += "*";
            }
        }

        public void ResetModified()
        {
            IsModified = false;
            if (Name.EndsWith("*"))
            {
                Name = Name.TrimEnd('*');
            }
        }

        [JsonConstructor]
        public PresetModel(List<StepModel> steps)
        {
            Steps = new ObservableCollection<StepModel>(steps);
            Steps.CollectionChanged += Steps_CollectionChanged;
            foreach (var step in Steps)
            {
                step.PropertyChanged += Step_PropertyChanged;
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
