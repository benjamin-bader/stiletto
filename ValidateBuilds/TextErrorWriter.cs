using System;
using System.IO;

namespace ValidateBuilds
{
    public abstract class TextErrorWriter : IErrorWriter
    {
        private bool disposed;

        protected TextWriter Writer { get; private set; }
        public bool HasError { get; protected set; }

        protected TextErrorWriter(TextWriter writer)
        {
            Writer = writer;
        }

        public abstract void Write(ValidationError error);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (Writer != null)
                {
                    Writer.Dispose();
                    Writer = null;
                }
            }

            disposed = true;
        }
    }
}