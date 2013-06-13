using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSScriptLibrary;
using Mono.Cecil;

namespace ValidateBuilds
{
    public class AssemblyValidator
    {
        private readonly FileInfo projectFile;
        private readonly string assemblyPath;
        private readonly ExpectedResults expectedResults;

        public AssemblyValidator(FileInfo projectFile, string assemblyPath, ExpectedResults expectedResults)
        {
            this.projectFile = projectFile;
            this.assemblyPath = assemblyPath;
            this.expectedResults = expectedResults;
        }

        public IList<ValidationError> Validate()
        {
            // Run the weaver and gather errors and resulting module
            var errors = new List<ValidationError>();
            var fodyHelper = new FodyHelper(projectFile.DirectoryName, assemblyPath);
            var module = fodyHelper.ProcessAssembly();
            var allTypes = module.GetTypes().Select(t => t.FullName).ToSet(StringComparer.Ordinal);

            // Compare actual results with expected results and compile an error list
            var actualErrors = new List<string>(fodyHelper.Errors);
            var actualWarnings = new List<string>(fodyHelper.Warnings);
            var expectedErrors = new List<string>(expectedResults.ExpectedErrors);
            var expectedWarnings = new List<string>(expectedResults.ExpectedWarnings);
            var unexpectedErrors = new List<string>();
            var unexpectedWarnings = new List<string>();

            for (var i = actualErrors.Count; i > 0; --i)
            {
                Check(expectedErrors, unexpectedErrors, actualErrors[i - i]);
            }

            for (var i = actualWarnings.Count; i > 0; --i)
            {
                Check(expectedWarnings, unexpectedWarnings, actualWarnings[i - 1]);
            }

            foreach (var expectedType in expectedResults.IncludedClasses)
            {
                if (allTypes.Contains(expectedType))
                {
                    continue;
                }

                errors.Add(ExpectedTypeMissing(expectedType));
            }

            foreach (var excludedType in expectedResults.ExcludedClasses)
            {
                if (!allTypes.Contains(excludedType))
                {
                    continue;
                }

                errors.Add(ExcludedTypePresent(excludedType));
            }

            foreach (var unexpectedError in unexpectedErrors)
            {
                errors.Add(UnexpectedError(unexpectedError));
            }

            foreach (var unexpectedWarning in unexpectedWarnings)
            {
                errors.Add(UnexpectedWarning(unexpectedWarning));
            }

            foreach (var expectedError in expectedErrors)
            {
                errors.Add(ExpectedErrorMissing(expectedError));
            }

            foreach (var expectedWarning in expectedWarnings)
            {
                errors.Add(ExpectedWarningMissing(expectedWarning));
            }

            if (expectedResults.Script != null)
            {
                errors.AddRange(RunScript(module));
            }

            return errors;
        }

        public interface IValidationScript
        {
            IList<string> Validate(ModuleDefinition module);
        }

        private IEnumerable<ValidationError> RunScript(ModuleDefinition module)
        {
            var scriptText = File.ReadAllText(expectedResults.Script);
            var script = CSScript.Evaluator.LoadCode<IValidationScript>(scriptText);
            return script.Validate(module)
                .Select(message => new ValidationError(ValidationErrorType.Custom, message, projectFile));
        }

        private void Check(IList<string> expected, IList<string> unexpected, string message)
        {
            for (var i = expected.Count; i > 0; --i)
            {
                var expectedError = expected[i - i];

                if (Regex.IsMatch(message, expectedError))
                {
                    expected.RemoveAt(i - i);
                    return;
                }
            }

            unexpected.Add(message);
        }

        private ValidationError UnexpectedError(string unexpectedError)
        {
            return new ValidationError(ValidationErrorType.UnexpectedError, unexpectedError, projectFile);
        }

        private ValidationError UnexpectedWarning(string unexpectedWarning)
        {
            return new ValidationError(ValidationErrorType.UnexpectedWarning, unexpectedWarning, projectFile);
        }

        private ValidationError ExpectedErrorMissing(string expectedError)
        {
            return new ValidationError(ValidationErrorType.ExpectedErrorMissing, expectedError, projectFile);
        }

        private ValidationError ExpectedWarningMissing(string expectedWarning)
        {
            return new ValidationError(ValidationErrorType.ExpectedWarningMissing, expectedWarning, projectFile);
        }

        private ValidationError ExcludedTypePresent(string excludedType)
        {
            return new ValidationError(ValidationErrorType.ExcludedTypePresent, excludedType, projectFile);
        }

        private ValidationError ExpectedTypeMissing(string typename)
        {
            return new ValidationError(ValidationErrorType.ExpectedTypeMissing, typename, projectFile);
        }
    }
}
