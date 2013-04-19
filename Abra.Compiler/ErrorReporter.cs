using System;

namespace Abra.Compiler
{
    public class ErrorReporter
    {
        private enum ErrorState
        {
            Valid,
            Warn,
            Error
        }

        private ErrorState state = ErrorState.Valid;

        public bool IsValid
        {
            get { return state != ErrorState.Error; }
        }

        public void Warn(string message, params object[] args)
        {
            Report(ErrorState.Warn, OnWarn, message, args);
        }

        public void Error(string message, params object[] args)
        {
            Report(ErrorState.Error, OnError, message, args);
        }

        protected virtual void OnWarn(string message)
        {
            Console.WriteLine(message);
        }

        private void Report(ErrorState minimumState, Action<string> action, string message, object[] args)
        {
            if (state < minimumState) {
                state = minimumState;
            }

            if (args.Length > 0) {
                message = string.Format(message, args);
            }

            action(message);
        }

        protected virtual void OnError(string message)
        {
            Console.WriteLine(message);
        }
    }
}
