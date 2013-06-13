using System;
using System.IO;

namespace ValidateBuilds
{
    public class PipeSeparatedErrorWriter : IErrorWriter
    {
        private const string Separator = "|";

        private TextWriter writer;
        private bool disposed;

        public PipeSeparatedErrorWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        public void Write(ValidationError error)
        {
            writer.Write(error.Type.ToString());
            writer.Write(Separator);
            writer.Write(error.Message.Replace(Separator, "--"));
            writer.Write(Separator);
            writer.Write(error.ProjectFile.FullName);
            writer.WriteLine();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }

            disposed = true;
        }
    }
}
