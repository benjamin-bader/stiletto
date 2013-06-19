using System;

namespace ValidateBuilds
{
    public interface IErrorWriter : IDisposable
    {
        void Write(ValidationError error);
    }
}