using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Compiler
{
    public static class Constants
    {
        public static readonly string InjectAttributeName = typeof (InjectAttribute).FullName;
        public static readonly string SingletonAttributeName = typeof (SingletonAttribute).FullName;
        public static readonly string NamedAttributeName = typeof (NamedAttribute).FullName;
        public static readonly string ModuleAttributeName = typeof (ModuleAttribute).FullName;
        public static readonly string ProvidesAttributeName = typeof (ProvidesAttribute).FullName;
    }
}
