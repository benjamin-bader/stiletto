using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Abra.Compiler
{
    public sealed class Env
    {
        private const int OK = 0;
        private const int WARN = 1;
        private const int ERROR = 2;

        private volatile int state = OK;

        public bool HasError
        {
            get { return state >= ERROR; }
        }

        public void Warn(string warning, params object[] args)
        {
            UpdateState(WARN);
            Console.Error.WriteLine(warning, args);
        }

        public void Error(string error, params object[] args)
        {
            UpdateState(ERROR);
            Console.Error.WriteLine(error, args);
        }

        private void UpdateState(int newState)
        {
            var currentState = state;

            if (currentState < newState)
                return;

            do {
                if (currentState != Interlocked.CompareExchange(ref state, newState, currentState)) {
                    break;
                }
                currentState = state;
            } while (currentState < newState);
        }
    }
}
