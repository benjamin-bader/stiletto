using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using Mono.Cecil;
using NUnit.Framework;

using Abra.Internal;

namespace Abra.Compiler.Test
{
    [TestFixture]
    public class ModuleNameTests
    {
        private ICompilation compilation;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            var loader = new CecilLoader();
            var asm = loader.LoadAssemblyFile(Assembly.GetExecutingAssembly().Location);
            compilation = new SimpleCompilation(asm);
        }

        [Test]
        public void TypeExtensionToCodeLiteral_MatchesCodeHelperToCodeLiteral()
        {
            var runtimeId = GetRuntimeLiteral<SomeModule>();
        }

        private string GetRuntimeLiteral<T>()
        {
            return typeof (T).ToCodeLiteral();
        }

        private string GetCompiledLiteral<T>()
        {
            var type = compilation.FindType(typeof (T));
            var typeDefinition = type.GetDefinition();
            return type.Namespace + CodeHelpers.ToCodeLiteral(typeDefinition);
        }

        private class SomeModule
        {
            
        }
    }
}
