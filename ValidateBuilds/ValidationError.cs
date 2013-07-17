using System.IO;

namespace ValidateBuilds
{
    public class ValidationError
    {
        public ValidationErrorType Type { get; private set; }
        public string Message { get; private set; }
        public FileInfo ProjectFile { get; private set; }

        public ValidationError(ValidationErrorType type, string message, FileInfo project)
        {
            Type = type;
            Message = message;
            ProjectFile = project;
        }
    }

    public enum ValidationErrorType
    {
        BuildFailed,
        WeaverCrashed,
        ExpectedTypeMissing,
        ExcludedTypePresent,
        ExpectedWarningMissing,
        ExpectedErrorMissing,
        UnexpectedError,
        UnexpectedWarning,
        Custom
    }
}
