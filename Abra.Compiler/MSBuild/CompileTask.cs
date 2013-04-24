using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Abra.Compiler.MSBuild
{
    public class CompileTask : Task
    {
        private const string DefaultGeneratedClassName = "CompilerGeneratedPlugin";

        ///////////////////////////////
        // Task properties
        
        [Required, Output]
        public ITaskItem OutputFile { get; set; }

        public string PluginClassName { get; set; }

        public bool ShouldValidate { get; set; }

        ///////////////////////////////
        // Private non-task properties

        private Project project;
        private Project Project
        {
            get { return project ?? (project = new Project(BuildEngine2.ProjectFileOfTaskNode)); }
        }

        private string ProjectNamespace
        {
            get { return Project.GetPropertyValue("RootNamespace"); }
        }

        private string ProjectName
        {
            get { return Project.GetPropertyValue("AssemblyName"); }
        }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Output: {0}\nClass: {1}\nValidate: {2}\n", OutputFile.ItemSpec, PluginClassName, ShouldValidate);

            var pluginClass = GetPluginClassName();

            if (pluginClass == null) {
                return false;
            }

            var settings = new Settings(
                GetOutputFilePath(),
                Project.FullPath,
                pluginClass,
                ShouldValidate,
                new TaskErrorReporter(this));

            var proc = new System.Diagnostics.ProcessStartInfo
                           {
                               FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                               CreateNoWindow = true,
                               ErrorDialog = false,
                               RedirectStandardError = true,
                               RedirectStandardOutput = true,
                               UseShellExecute = false,
                               WorkingDirectory = Project.DirectoryPath,
                               Arguments =
                                   string.Format("-p={0} -o={1} -n={2}", Project.FullPath, GetOutputFilePath(),
                                                 pluginClass)
                           };

            var process = Process.Start(proc);

            process.BeginOutputReadLine();
            process.OutputDataReceived += (o, d) => {
                if (process.HasExited) {
                    process.CancelOutputRead();
                }
                
                if (d.Data == null) {
                    return;
                }

                Log.LogMessage(d.Data);
            };

            process.BeginErrorReadLine();
            process.ErrorDataReceived += (o, d) => {
                if (process.HasExited) {
                    process.CancelErrorRead();
                }

                if (d.Data == null) {
                    return;
                }

                Log.LogError(d.Data);
            };

            process.WaitForExit();

            return !Log.HasLoggedErrors;
        }

        private string GetOutputFilePath()
        {
            if (OutputFile == null) {
                throw new Exception("assert false, why is a [Required] property null?");
            }

            var path = OutputFile.ItemSpec;
            
            if (Path.IsPathRooted(path)) {
                return path;
            }

            // Relative paths are relative to the project file's location.
            return Path.GetFullPath(Path.Combine(Project.DirectoryPath, path));
        }

        private string GetPluginClassName()
        {
            var name = PluginClassName;
            if (string.IsNullOrEmpty(name)) {
                name = DefaultGeneratedClassName;
            }

            // Is a namespace included?
            var lastDot = name.LastIndexOf('.');
            var hasNamespace = lastDot > 0;

            var namePart = hasNamespace ? name.Substring(lastDot + 1) : name;

            if (!System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(namePart)) {
                Log.LogError("{0} is not a valid class name", name);
                return null;
            }
                
            return hasNamespace
                ? name
                : (ProjectNamespace ?? ProjectName) + "." + name;
        }
    }
}
