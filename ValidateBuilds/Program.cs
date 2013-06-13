using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Execution;

namespace ValidateBuilds
{
    public class Program : IDisposable
    {
        private readonly string currentDirectory;
        private readonly Dictionary<string, string> globalBuildProperties = new Dictionary<string, string>
        {
            { "Configuration", "Debug" },
            { "Platform", "AnyCPU" },
        };

        private IErrorWriter errorWriter;

        public static void Main()
        {
            using (var program = new Program(Environment.CurrentDirectory))
            {
                program.Run();
            }
        }

        public Program(string currentDirectory)
        {
            this.currentDirectory = currentDirectory;

            var stdout = Console.OpenStandardOutput();
            var streamWriter = new StreamWriter(stdout);
            errorWriter = new JsonErrorWriter(streamWriter);
        }

        public void Run()
        {
            var builds = from file in GetProjectFiles()
                         let projectDirectory = Path.GetDirectoryName(file)
                         let expectedResultsFile = Path.Combine(projectDirectory, "expectedResults.xml")
                         let expectedResults = ReadExpectedResults(expectedResultsFile)
                         select new BuildState(new FileInfo(file), expectedResults);

            foreach (var build in builds)
            {
                if (!ExecuteBuild(build))
                {
                    var err = new ValidationError(ValidationErrorType.BuildFailed, string.Empty, build.ProjectFile);
                    errorWriter.Write(err);
                    continue;
                }

                var validator = new AssemblyValidator(
                    build.ProjectFile,
                    build.OutputAssembly.FullName,
                    build.ExpectedResults);

                var errors = validator.Validate();

                foreach (var error in errors)
                {
                    errorWriter.Write(error);
                }
            }
        }

        private bool ExecuteBuild(BuildState state)
        {
            var req = new BuildRequestData(state.ProjectFile.FullName, globalBuildProperties, "4.0", new[] {"Clean", "Build"}, null);
            var parameters = new BuildParameters();
            var buildResult = BuildManager.DefaultBuildManager.Build(parameters, req);

            // This assumes that one and only one item is output from the build.
            state.OutputAssembly = new FileInfo(buildResult["Build"].Items.First().ItemSpec);
            return buildResult.OverallResult == BuildResultCode.Success;
        }

        private IEnumerable<string> GetProjectFiles()
        {
            return Directory.EnumerateFiles(currentDirectory, "*.csproj", SearchOption.AllDirectories);
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
