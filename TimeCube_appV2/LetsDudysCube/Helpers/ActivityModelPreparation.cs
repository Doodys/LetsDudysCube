using LetsDudysCube.Models;
using System;
using System.Collections.ObjectModel;

namespace LetsDudysCube.Helpers
{
    public class ActivityModelPreparation : ObservableCollection<ActivityModel>
    {
        private const string TimerInitialValue = "00:00:00";

        public ActivityModelPreparation(CubeModel cubeModel)
        {
            var timespanInitialValue = new TimeSpan(0);

            Add(new(cubeModel.BackWall, timespanInitialValue, TimerInitialValue));
            Add(new(cubeModel.BelowWall, timespanInitialValue, TimerInitialValue));
            Add(new(cubeModel.FrontWall, timespanInitialValue, TimerInitialValue));
            Add(new(cubeModel.LeftWall, timespanInitialValue, TimerInitialValue));
            Add(new(cubeModel.RightWall, timespanInitialValue, TimerInitialValue));
            Add(new(cubeModel.UpperWall, timespanInitialValue, TimerInitialValue));
        }
    }
}