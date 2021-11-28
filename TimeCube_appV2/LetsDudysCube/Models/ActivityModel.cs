using LetsDudysCube.Annotations;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LetsDudysCube.Models
{
    public class ActivityModel : INotifyPropertyChanged
    {
        private string _activityName;
        private TimeSpan _timeSpent;
        private string _timeOutput;

        public string ActivityName
        {
            get => _activityName;
            set { _activityName = value; OnPropertyChanged(nameof(ActivityName)); }
        }

        public TimeSpan TimeSpent
        {
            get => _timeSpent;
            set { _timeSpent = value; OnPropertyChanged(nameof(TimeSpent)); }
        }

        public string TimeOutput
        {
            get => _timeOutput;
            set { _timeOutput = value; OnPropertyChanged(nameof(TimeOutput)); }
        }

        public ActivityModel(string activityName, TimeSpan timeSpent, string timeOutput)
        {
            this.ActivityName = activityName;
            this.TimeSpent = timeSpent;
            this.TimeOutput = timeOutput;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}