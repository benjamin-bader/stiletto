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
            var asm = @"C:\Users\ben\Development\abra-ioc\Example\bin\Debug\Example.exe";

            var ad = AssemblyDefinition.ReadAssembly(asm, new ReaderParameters { ReadSymbols = true });
            var md = ad.MainModule;

            var weaver = new ModuleWeaver()
                             {
                                 LogError = Console.WriteLine,
                                 ModuleDefinition = md
                             };

            weaver.Execute();

            ad.Write("out.exe", new WriterParameters() { WriteSymbols = true });
        }
    }
}
