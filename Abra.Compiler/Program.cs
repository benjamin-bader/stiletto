using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Roslyn.Compilers.Compilation;
using Roslyn.Compilers.CSharp;

namespace Abra.Compiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var file = @"C:\Users\ben\Development\abra-ioc\Abra.Compiler\CompilerOptions.cs";
            var tree = SyntaxTree.ParseFile(file, ParseOptions.Default);
            var root = tree.GetRoot();
            var ns = (NamespaceDeclarationSyntax) root.Members.First();
            var classes = ns.Members.OfType<ClassDeclarationSyntax>().ToArray();

            foreach (var c in classes) {
                Console.WriteLine(c.Identifier);
            }
        }
    }
}
