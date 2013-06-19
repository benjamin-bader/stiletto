using System.IO;

namespace ValidateBuilds
{
    public class BuildState
    {
        public FileInfo ProjectFile { get; private set; }
        public ExpectedResults ExpectedResults { get; private set; }
        public FileInfo OutputAssembly { get; set; }

        public BuildState(FileInfo projectFilePath, ExpectedResults expectedResults)
        {
            ProjectFile = projectFilePath;
            ExpectedResults = expectedResults;
        }
    }
}
