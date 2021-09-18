using System;

namespace TimeCube.Models
{
    public class ActivityModel
    {
        public string ActivityName { get; set; }
        public TimeSpan TimeSpent { get; set; }
        public string TimeOutput { get; set; }
    }
}
