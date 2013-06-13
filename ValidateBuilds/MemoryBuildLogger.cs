using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace ValidateBuilds
{
    public class MemoryBuildLogger : ILogger
    {
        private IEventSource currentEventSource;

        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; } 

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            currentEventSource = eventSource;
            Warnings = new List<string>();
            Errors = new List<string>();

            eventSource.WarningRaised += EventSourceOnWarningRaised;
            eventSource.ErrorRaised += EventSourceOnErrorRaised;
        }

        private void EventSourceOnWarningRaised(object sender, BuildWarningEventArgs e)
        {
            Warnings.Add(e.Message);
        }

        private void EventSourceOnErrorRaised(object sender, BuildErrorEventArgs e)
        {
            Errors.Add(e.Message);
        }

        public void Shutdown()
        {
            currentEventSource.WarningRaised -= EventSourceOnWarningRaised;
            currentEventSource.ErrorRaised -= EventSourceOnErrorRaised;
            currentEventSource = null;
        }
    }
}
