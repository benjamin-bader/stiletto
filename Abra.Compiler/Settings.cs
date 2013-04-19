using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NDesk.Options;

namespace Abra.Compiler
{
    public class Settings
    {
        private OptionSet options;

        public FileInfo OutputFile { get; private set; }
        public FileInfo ProjectFile { get; private set; }
        public bool ShouldValidate { get; private set; }

        public Settings(IEnumerable<string> args)
        {
            options = new OptionSet {
                {"p|project-file=", "The .csproj to process", path => ProjectFile = new FileInfo(path)},
                {"o|outfile=", "The destination file, defaults to Tophat.Generated.cs.", o => OutputFile = new FileInfo(o)},
                {"v|validate", "Perform validation of the dependency graph.", v => ShouldValidate = v != null}
            };

            var unknownArgs = options.Parse(args);

            if (unknownArgs.Count > 0) {
                throw Error("Unknown args: " + string.Join(" ", unknownArgs));
            }

            if (!ProjectFile.Exists) {
                throw Error("Project file '{0}' can not be found, please supply a valid .csproj file.", ProjectFile.FullName);
            }

            if (OutputFile == null) {
                var path = Path.GetDirectoryName(ProjectFile.FullName) ?? Environment.CurrentDirectory;
                OutputFile = new FileInfo(Path.Combine(path, "Tophat.Generated.cs"));
            }

            options = null;
        }

        private Exception Error(string message, params object[] args)
        {
            var sb = new StringBuilder()
                .AppendFormat(message, args)
                .AppendLine()
                .AppendLine();

            using (var writer = new StringWriter(sb)) {
                options.WriteOptionDescriptions(writer);
                writer.Flush();
                throw new Exception(message);
            }
        }
    }
}
