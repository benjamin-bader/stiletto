using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    /// <summary>
    /// Represents the specification of a concrete implementation
    /// to a dependency.
    /// </summary>
    internal interface IBinding
    {
        bool IsResolved { get; set; }
        object Get();
        void GetDependencies(ISet<IBinding> bindings);
    }
}
