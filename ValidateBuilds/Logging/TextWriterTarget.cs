using System.IO;
using NLog;
using NLog.Common;
using NLog.Targets;

namespace ValidateBuilds.Logging
{
    public class TextWriterTarget : TargetWithLayout
    {
        private bool disposed;

        private TextWriter writer;

        public TextWriterTarget(TextWriter writer)
        {
            this.writer = writer;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var output = Layout.Render(logEvent);
            writer.WriteLine(output);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            var output = Layout.Render(logEvent.LogEvent);
            writer.WriteLineAsync(output)
                  .ContinueWith(task => logEvent.Continuation(task.Exception));
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            writer.FlushAsync()
                  .ContinueWith(task => asyncContinuation(task.Exception));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
            }

            disposed = true;

            base.Dispose(disposing);
        }
    }
}
