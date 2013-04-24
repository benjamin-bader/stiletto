using Microsoft.Build.Utilities;

namespace Abra.Compiler.MSBuild
{
    class TaskErrorReporter : ErrorReporter
    {
        private readonly TaskLoggingHelper log;

        public TaskErrorReporter(Task task)
        {
            log = task.Log;
        }

        protected override void OnLog(string message)
        {
            log.LogMessage(message);
        }

        protected override void OnWarn(string message)
        {
            log.LogWarning(message);
        }

        protected override void OnError(string message)
        {
            log.LogError(message);
        }
    }
}
