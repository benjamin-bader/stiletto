using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers.CSharp;

namespace Abra.Compiler
{
    public interface IPass
    {
        void Operate(ISet<ClassDeclarationSyntax> classes, Env env);
    }
}
