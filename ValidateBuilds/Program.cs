using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Execution;
using NLog;
using NLog.Config;
using ValidateBuilds.Logging;

namespace ValidateBuilds
{
    public class Program : IDisposable
    {
        private static Logger logger;

        private readonly string workingDirectory;
        private readonly Dictionary<string, string> globalBuildProperties = new Dictionary<string, string>
        {
            { "Configuration", "Debug" },
            { "Platform", "AnyCPU" },
        };

        private int numTestsFailed;
        private IErrorWriter errorWriter;

        public static void Main(string[] args)
        {
            var flags = new Flags(args);

            if (flags.HelpRequested)
            {
                flags.ShowUsage(Console.Out);
                return;
            }

            ConfigureLogging(flags.Verbose);

            using (var program = new Program(flags.WorkingDirectory, flags.ErrorWriter))
            {
                program.Run();
            }
        }

        private static void ConfigureLogging(bool verbose)
        {
            var config = new LoggingConfiguration();
            var standardError = Console.OpenStandardError();
            var target = new TextWriterTarget(new StreamWriter(standardError));
            var rule = new LoggingRule("*", verbose ? LogLevel.Debug : LogLevel.Info, target);

            target.Layout = "${message}";

            config.AddTarget("debug", target);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;
            logger = LogManager.GetCurrentClassLogger();
        }

        public Program(string workingDirectory, IErrorWriter errorWriter)
        {
            this.workingDirectory = workingDirectory;
            this.errorWriter = errorWriter;
        }

        public void Run()
        {
            var builds = from file in GetProjectFiles()
                         let projectDirectory = Path.GetDirectoryName(file)
                         let expectedResultsFile = Path.Combine(projectDirectory, "expectedResults.xml")
                         let expectedResults = ReadExpectedResults(expectedResultsFile)
                         select new BuildState(new FileInfo(file), expectedResults);

            var buildList = builds.ToList();

            logger.Debug("Building {0} integration tests.", buildList.Count);

            foreach (var build in buildList)
            {
                logger.Debug("Building {0}...", build.ProjectFile.FullName);
                if (!ExecuteBuild(build))
                {
                    var err = new ValidationError(ValidationErrorType.BuildFailed, string.Empty, build.ProjectFile);
                    errorWriter.Write(err);
                    ++numTestsFailed;
                    continue;
                }

                var validator = new AssemblyValidator(
                    build.ProjectFile,
                    build.OutputAssembly.FullName,
                    build.ExpectedResults);

                logger.Debug("Validating {0}...", build.ProjectFile.FullName);
                var errors = validator.Validate();

                foreach (var error in errors)
                {
                    errorWriter.Write(error);
                }

                if (errors.Count > 0)
                {
                    ++numTestsFailed;
                }
            }

            logger.Debug("Test run finished with {0} failure{1}", numTestsFailed, numTestsFailed == 1 ? string.Empty : "s");
        }

        private bool ExecuteBuild(BuildState state)
        {
            var req = new BuildRequestData(state.ProjectFile.FullName, globalBuildProperties, "4.0", new[] {"Clean", "Build"}, null);
            var parameters = new BuildParameters();
            var buildResult = BuildManager.DefaultBuildManager.Build(parameters, req);

            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                if (buildResult.Exception != null)
                {
                    logger.ErrorException("Build failed for " + state.ProjectFile.FullName, buildResult.Exception);
                }
                else
                {
                    logger.Debug("Build failed for {0}", state.ProjectFile.FullName);
                }

                return false;
            }

            // This assumes that one and only one item is output from the build.
            state.OutputAssembly = new FileInfo(buildResult["Build"].Items.First().ItemSpec);
            return true;
        }

        private IEnumerable<string> GetProjectFiles()
        {
            return Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories);
        }

        private static readonly XElement emptyResults = new XElement("ExpectedResults");
        private ExpectedResults ReadExpectedResults(string expectedResultsFilePath)
        {
            if (string.IsNullOrEmpty(expectedResultsFilePath) || !File.Exists(expectedResultsFilePath))
            {
                return ExpectedResults.Parse(emptyResults, "");
            }

            using (var fileStream = File.OpenRead(expectedResultsFilePath))
            {
                var xml = XElement.Load(fileStream);
                return ExpectedResults.Parse(xml, Path.GetDirectoryName(expectedResultsFilePath));
            }
        }

        public void Dispose()
        {
            if (errorWriter != null)
            {
                errorWriter.Dispose();
                errorWriter = null;
            }
        }
    }
}
