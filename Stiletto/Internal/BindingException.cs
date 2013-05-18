using System;

namespace Stiletto.Internal
{
    public class BindingException : ApplicationException
    {
        public BindingException(string message)
            : base(message)
        {
        }
    }
}
