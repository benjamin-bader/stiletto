using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    public class InjectAttribute : Attribute
    {
    }
}
