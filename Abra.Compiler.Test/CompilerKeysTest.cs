using System;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using NUnit.Framework;

namespace Abra.Compiler.Test
{
    [TestFixture]
    public class CompilerKeysTest : Abra.Test.KeyTestsBase
    {
        private ICompilation compilation;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            // We need to convert System.Types to ITypes, so we need an ICompilation
            // containing all relevant types needed by the unit tests, including
            // Abra types.  The least-hacky way I know of is to just reflect over
            // loaded assemblies and use a CecilLoader.  We have to force the Abra
            // assembly explicitly, since it may not be loaded at this point otherwise.
            // We accomplish this by declaring it (more specifically, the assembly in which
            // IProvider<T> is defined) to be the "main assembly" of this pseudo-compilation.

            var loader = new CecilLoader();
            var mainAsm = loader.LoadAssemblyFile(typeof(IProvider<>).Assembly.Location);
            var references = AppDomain.CurrentDomain
                                      .GetAssemblies()
                                      .Select(asm => loader.LoadAssemblyFile(asm.Location));

            compilation = new SimpleCompilation(mainAsm, references);
        }

        protected override string GetKey<T>(string name = null)
        {
            var itype = FromSystemType(typeof (T));
            return CompilerKeys.ForTypeDef(itype, name);
        }

        protected override string GetMemberKey<T>()
        {
            return CompilerKeys.GetMemberKey(FromSystemType(typeof (T)));
        }

        protected override string GetProviderKey(string key)
        {
            return CompilerKeys.GetProviderKey(key);
        }

        protected override string GetLazyKey(string key)
        {
            return CompilerKeys.GetLazyKey(key);
        }

        protected override bool IsNamed(string key)
        {
            return CompilerKeys.IsNamed(key);
        }

        private IType FromSystemType(Type t)
        {
            return compilation.FindType(t);
        }
    }
}
