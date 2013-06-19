using System.IO;

namespace ValidateBuilds
{
    public class PipeSeparatedErrorWriter : TextErrorWriter
    {
        private const string Separator = "|";

        public PipeSeparatedErrorWriter(TextWriter writer)
            : base(writer)
        {
        }

        public override void Write(ValidationError error)
        {
            Writer.Write(error.Type.ToString());
            Writer.Write(Separator);
            Writer.Write(EscapeMessage(error.Message));
            Writer.Write(Separator);
            Writer.Write(error.ProjectFile.FullName);
            Writer.WriteLine();
        }

        private static string EscapeMessage(string message)
        {
            return message
                .Replace(Separator, "--")
                .Replace("\r\n", "; ")
                .Replace("\n", "; ");
        }
    }
}
