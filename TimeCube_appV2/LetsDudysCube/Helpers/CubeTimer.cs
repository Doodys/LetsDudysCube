using System;
using Windows.UI.Xaml;

namespace LetsDudysCube.Helpers
{
    internal class CubeTimer
    {
        public bool isStarted = false;

        /// <summary>
        /// Global counter that increments while the timer is active
        /// </summary>
        private TimeSpan totalElapsed = new TimeSpan(0);

        /// <summary>
        /// Save the last relevant point in time to compute the next elapsed increment
        /// </summary>
        private DateTime lastPointInTime = DateTime.MinValue;

        private readonly DispatcherTimer dispatcherTimer;

        public CubeTimer()
        {
            dispatcherTimer = new DispatcherTimer();
        }

        public void Start()
        {
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Tick += DispatcherTimeTicker;
            // reset lastPointInTime in order to skip time from when the timer was stopped
            lastPointInTime = DateTime.Now;
            isStarted = true;
            dispatcherTimer.Start();
        }

        public void Stop()
        {
            isStarted = false;
            dispatcherTimer.Stop();
        }

        public TimeSpan TimerOutput()
        {
            return totalElapsed;
        }

        private void DispatcherTimeTicker(object sender, object o)
        {
            var nextPointInTime = DateTime.Now;
            totalElapsed += nextPointInTime - lastPointInTime;
            lastPointInTime = nextPointInTime;
        }
    }
}