using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace ValidateBuilds
{
    public class Flags
    {
        private readonly OptionSet options;

        public bool Verbose { get; private set; }
        public bool HelpRequested { get; private set; }
        public IErrorWriter ErrorWriter { get; private set; }
        public string WorkingDirectory { get; private set; }

        private static IErrorWriter ParseErrorWriter(string name)
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput());

            switch ((name ?? "").ToLowerInvariant())
            {
                case "pipe":
                    return new PipeSeparatedErrorWriter(stdout);

                case "json":
                default:
                    return new JsonErrorWriter(stdout);
            }
        }

        public Flags(IEnumerable<string> args)
        {
            options = new OptionSet
            {
                {"v", "Enable verbose output", v => Verbose = v != null},
                {"f=|format=", "Set output format.  Valid values are \"pipe\" and \"json\".", fmt => ErrorWriter = ParseErrorWriter(fmt)},
                {"h|?|help", "Print this message.", v => HelpRequested = v != null},
                {"dir=", "The directory containing the integration tests to run.  Defaults to the current directory.", dir => WorkingDirectory = dir},
            };

            options.Parse(args);

            if (ErrorWriter == null)
            {
                ErrorWriter = ParseErrorWriter("json");
            }

            if (WorkingDirectory == null)
            {
                WorkingDirectory = Environment.CurrentDirectory;
            }
        }

        public void ShowUsage(TextWriter writer)
        {
            options.WriteOptionDescriptions(writer);
        }
    }
}
