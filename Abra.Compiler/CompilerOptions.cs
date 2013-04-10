using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NDesk.Options;

namespace Abra.Compiler
{
    [Module]
    public class CompilerOptions
    {
        private readonly List<string> source_files = new List<string>();  

        public IList<string> SourceFiles {
            get { return source_files; }
        }

        [Provides]
        public object ProvdeObject()
        {
            return new object();
        }

        public static CompilerOptions ParseCommandLine(IEnumerable<string> args)
        {
            var co = new CompilerOptions();
            var options = new OptionSet
                              {
                                  {"-s", "A source file to analyze", path => co.source_files.Add(path)}
                              };

            options.Parse(args);
            return co;
        }
    }
}
