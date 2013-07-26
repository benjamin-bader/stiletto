using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ValidateBuilds
{
    /// <summary>
    /// Represents the expected (successful) output of post-build assembly
    /// processing.
    /// </summary>
    /// <remarks>
    /// Expected results files take the following form:
    /// <code>
    /// &lt;ExpectedResults Script="path/to/script" &gt;
    ///   &lt;Classes&gt;
    ///     &lt;Include Name="SomeType" /&gt;
    ///     &lt;Exclude Name="OtherType" /&gt;
    ///   &lt;/Classes&gt;
    ///   &lt;Warnings&gt;
    ///     &lt;Pattern&gt; regex &lt;/Pattern&gt;
    ///   &lt;/Warnings&gt;
    ///   &lt;Errors&gt;
    ///     &lt;Pattern&gt; regex &lt;/Pattern&gt;
    ///   &lt;/Errors&gt;
    /// &lt;/ExpectedResults&gt;
    /// </code>
    /// </remarks>
    public class ExpectedResults
    {
        public IList<string> ExpectedWarnings { get; private set; }
        public IList<string> ExpectedErrors { get; private set; }

        public ISet<string> IncludedClasses { get; private set; }
        public ISet<string> ExcludedClasses { get; private set; }

        public string Script { get; private set; }

        private ExpectedResults(
            IEnumerable<string> warnings,
            IEnumerable<string> errors,
            ISet<string> includedClasses,
            ISet<string> excludedClasses)
        {
            ExpectedWarnings = new List<string>(warnings);
            ExpectedErrors = new List<string>(errors);
            IncludedClasses = includedClasses;
            ExcludedClasses = excludedClasses;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="directoryPath">
        /// The path to the directory containing the expected results file.
        /// </param>
        /// <returns></returns>
        public static ExpectedResults Parse(XElement element, string directoryPath)
        {
            var rootNodes = element.DescendantsAndSelf("ExpectedResults").ToList();

            if (rootNodes == null || rootNodes.Count != 1)
            {
                throw new ArgumentException("One 'ExpectedResults' node is required.");
            }

            var root = rootNodes[0];

            var scriptFile = (string)root.Attribute("Script");

            if (scriptFile != null)
            {
                scriptFile = Path.Combine(directoryPath, scriptFile);
                scriptFile = Path.GetFullPath(scriptFile);
            }

            var includedTypes = root
                .Descendants("Classes")
                .Descendants("Include")
                .Select(el => (string)el.Attribute("Name"))
                .ToSet(StringComparer.Ordinal);

            var excludedTypes = root
                .Descendants("Classes")
                .Descendants("Exclude")
                .Select(el => (string)el.Attribute("Name"))
                .ToSet(StringComparer.Ordinal);

            includedTypes.ExceptWith(excludedTypes);

            var warnings = root
                .Descendants("Warnings")
                .Descendants("Pattern")
                .Select(ReadElementAsString);

            var errors = root
                .Descendants("Errors")
                .Descendants("Pattern")
                .Select(ReadElementAsString);

            return new ExpectedResults(warnings, errors, includedTypes, excludedTypes) { Script = scriptFile };
        }

        private static string ReadElementAsString(XElement e)
        {
            return (string)e;
        }
    }
}
