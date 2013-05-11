using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Cecil;

namespace Abra.Fody
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var asm = @"C:\Users\ben\Development\abra-ioc\Abra.Test\bin\Debug\Abra.Test.dll";

            var ad = AssemblyDefinition.ReadAssembly(asm, new ReaderParameters { ReadSymbols = true });
            var md = ad.MainModule;

            var weaver = new ModuleWeaver()
                             {
                                 LogError = Console.WriteLine,
                                 ModuleDefinition = md
                             };

            weaver.Execute();

            ad.Write(asm, new WriterParameters() { WriteSymbols = true });
        }
    }
}
