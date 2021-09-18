using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO.Ports;
using TimeCube.Models;

namespace TimeCube.Builders
{
    public static class TimeCubeWindowBuilder
    {
        public static List<string> serialPorts;
        public static ObservableCollection<ActivityModel> activityModels;
        public static List<string> PrepareSerialPortsCollection()
        {
            if(serialPorts == null)
                serialPorts = new List<string>();

            serialPorts.AddRange(SerialPort.GetPortNames());

            return serialPorts;
        }

        public static ObservableCollection<ActivityModel> InitializeActivityModels(EntryDataModel entryDataModel)
        {
            var timerInitialValue = "00:00:00";
            var timespanInitialValue = new TimeSpan(0);

            return activityModels = new ObservableCollection<ActivityModel>()
            {
                new ActivityModel()
                {
                    ActivityName = entryDataModel.BreakWallName,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = entryDataModel.LeftWallName,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = entryDataModel.RightWallName,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = entryDataModel.UpperWallName,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
                new ActivityModel()
                {
                    ActivityName = entryDataModel.LowerWallName,
                    TimeOutput = timerInitialValue,
                    TimeSpent = timespanInitialValue
                },
            };
        }
    }
}
