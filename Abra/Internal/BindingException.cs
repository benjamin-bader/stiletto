using System;

namespace Abra.Internal
{
    public class BindingException : ApplicationException
    {
        public BindingException(string message)
            : base(message)
        {
        }
    }
}
