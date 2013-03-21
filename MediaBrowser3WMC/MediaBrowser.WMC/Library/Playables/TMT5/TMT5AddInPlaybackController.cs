using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MediaBrowser.Library.Events;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library.Playables.TMT5
{
    public class TMT5AddInPlaybackController : TMT5PlaybackController
    {
        /// <summary>
        /// Gets arguments to be passed to the command line.
        /// </summary>
        protected override List<string> GetCommandArgumentsList(PlayableItem playbackInfo)
        {
            List<string> args = new List<string>();

            args.Add("uri={0}");

            return args;
        }

        /// <summary>
        /// Removes double quotes and flips slashes
        /// </summary>
        protected override string GetFilePathCommandArgument(IEnumerable<string> filesToPlay)
        {
            return base.GetFilePathCommandArgument(filesToPlay).Replace("\"", string.Empty).Replace('\\', '/');
        }

        protected override void OnExternalPlayerLaunched(PlayableItem playbackInfo)
        {
            base.OnExternalPlayerLaunched(playbackInfo);

            Async.Queue("Wait for process to exit", () => WaitForProcessToExit());
        }

        /// <summary>
        /// Provides an alternate way of detecting the player has closed, in case the base class file watcher doesn't work
        /// </summary>
        private void WaitForProcessToExit()
        {
            string processName = "uMCEPlayer5";

            Process process = Process.GetProcessesByName(processName).FirstOrDefault();

            // First wait for the process to start, for a max of 10 seconds
            int count = 0;

            while (process == null && count < 40)
            {
                Thread.Sleep(250);

                process = Process.GetProcessesByName(processName).FirstOrDefault();

                count++;
            }

            // Now wait for the process to exit
            if (process != null)
            {
                Logger.ReportVerbose("{0} has started", processName);

                process.WaitForExit();

                Logger.ReportVerbose("{0} has exited", processName);

                if (!_HasStopped)
                {
                    _HasStopped = true;
                    OnPlaybackFinished(GetFinishedPlaybackState());
                }
            }
        }

        protected override void HandleStoppedState(PlaybackStateEventArgs args)
        {
            OnPlaybackFinished(args);
        }

        protected override void ClosePlayer()
        {
            StopInternal();
        }
    }
}
