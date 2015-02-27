using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DavidNews.Common;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.ServiceModel.Syndication;
using RSSParser.Components;


namespace RSSParser
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent _runCompleteEvent = new ManualResetEvent(false);
        private ExpiryAgent _expiryAgent;



        private int RefreshInterval
        {
            get { return Settings.GetSetting("RefreshIntervalInMinutes", 5) * 60 * 1000; }
        }
        public override void Run()
        {
            Trace.TraceInformation("RSSParser is running");

            try
            {
                // Create another thread to continuosly get the RSS feeds
                ThreadPool.QueueUserWorkItem(o => RefreshRSSFeeds());

                RunAsync(_cancellationTokenSource.Token).Wait();
            }
            finally
            {
                _runCompleteEvent.Set();
            }
        }

        private void RefreshRSSFeeds()
        {
            while (true)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                var feeds = RoleEnvironment.GetConfigurationSettingValue("RSSFeeds").Split(';');
                foreach (var feed in feeds)
                {
                    try
                    {
                        // Read the feed
                        SyndicationFeed syndFeed = null;
                        Retry.Do(() =>
                        {
                            using (var r = XmlReader.Create(feed))
                            {
                                syndFeed = SyndicationFeed.Load(r);
                            }
                        }, TimeSpan.FromSeconds(3));

                        // Store the feed items on Redis
                        Trace.TraceInformation("Processing feed items from {0}", feed);
                        Redis.Insert(syndFeed);
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                    }                    
                }

                Thread.Sleep(RefreshInterval);
            }
        }

        private void LogException(Exception ex)
        {
            Trace.TraceError(ex.ToString());
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections (for Azure Storage better performance)
            // http://social.msdn.microsoft.com/Forums/en-US/windowsazuredata/thread/d84ba34b-b0e0-4961-a167-bbe7618beb83
            ServicePointManager.DefaultConnectionLimit = Settings.GetSetting("DefaultConnectionLimit", 1000);

            try
            {
                _expiryAgent = new ExpiryAgent();
                _expiryAgent.Start();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Cannot start Expiry Agent: {0}", ex.Message);
            }

            bool result = base.OnStart();
            Trace.TraceInformation("RSSParser has been started");
            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("RSSParser is stopping");

            if (_expiryAgent != null)
            {
                _expiryAgent.Stop();
                _expiryAgent = null;
            }

            _cancellationTokenSource.Cancel();
            _runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("RSSParser has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
