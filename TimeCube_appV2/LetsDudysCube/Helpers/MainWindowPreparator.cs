using LetsDudysCube.Models;
using System;
using System.Collections.ObjectModel;

namespace LetsDudysCube.Helpers
{
    public static class MainWindowPreparator
    {
        public static ObservableCollection<ActivityModel> activityModels;

        public static ObservableCollection<ActivityModel> InitializeActivityModels(CubeModel cubeModel)
        {
            var timerInitialValue = "00:00:00";
            var timespanInitialValue = new TimeSpan(0);

            return activityModels = new ObservableCollection<ActivityModel>()
            {
                new ActivityModel()
                {
                    ActivityName = cubeModel.BackWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = cubeModel.BelowWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = cubeModel.FrontWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = cubeModel.LeftWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = cubeModel.RightWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = cubeModel.UpperWall,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
            };
        }
    }
}
