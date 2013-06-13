using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using ValidateBuilds;

public class Validators
{
    public IList<ValidationError> Validate(ModuleDefinition module, FileInfo projectFile)
    {
        var errors = new List<ValidationError>();
        var allTypes = new HashSet<string>(module.GetTypes().Select(t => t.FullName));

        if (!allTypes.Contains("Stiletto.Generated.$CompiledPlugin$"))
        {
            var err = new ValidationError(
                ValidationErrorType.Custom,
                "Expected calls to Container.Create to be replaced with Container.CreateWithPlugins.",
                projectFile);

            errors.Add(err);
        }

        return errors;
    }
}