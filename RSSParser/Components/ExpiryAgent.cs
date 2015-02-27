using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DavidNews.Common;

namespace RSSParser.Components
{
    class ExpiryAgent
    {

        private Timer _pollingTimer;
        public int PollingInterval { get; set; }

        #region Methods

        public void Start()
        {
            Trace.TraceInformation("Starting expiry agent...");
            Start(Settings.GetSetting("Redis.ExpirationIntervalInMinutes", 60) * 60);
        }
        public void Start(int pollingInterval)
        {
            PollingInterval = pollingInterval;
            var tCallback = new TimerCallback(PollingTimerTick);
            _pollingTimer = new Timer(tCallback, null, pollingInterval * 1000, Timeout.Infinite);
        }

        public void Stop()
        {
            Trace.TraceInformation("Stopping expiry agent...");

            // Stops the timer
            if (_pollingTimer != null)
            {
                _pollingTimer.Dispose();
                _pollingTimer = null;
            }
        }
        #endregion

        private void PollingTimerTick(object stateInfo)
        {
            try
            {
                if (_pollingTimer != null)  // The timer would be disposed while processing the lapse
                    _pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Expire the items
                Redis.ExpireItems();

                // Expire item points
                Redis.ExpirePoints();

            }
            catch (Exception ex)
            {
                Trace.TraceError("Error: {0}", ex);
            }
            finally
            {
                if (_pollingTimer != null)
                {
                    _pollingTimer.Change(PollingInterval * 1000, Timeout.Infinite);
                }
            }
        }
    }
}
