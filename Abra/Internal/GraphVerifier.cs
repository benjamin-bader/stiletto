using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal class GraphVerifier
    {
        internal void Verify(IEnumerable<Binding> bindings)
        {
            DetectCircularDependencies(bindings, new Stack<Binding>());
        }

        private void DetectCircularDependencies(IEnumerable<Binding> bindings, Stack<Binding> path)
        {
            foreach (var binding in bindings)
            {
                if (binding.IsCycleFree)
                {
                    continue;
                }

                if (binding.IsVisiting)
                {
                    var sb = new StringBuilder("Cycle detected:").AppendLine();
                    var message = Enumerable.Range(1, path.Count)
                        .Zip(path.Reverse(), Tuple.Create)
                        .Aggregate(sb, (s, tup) =>
                            s.Append("\t")
                             .Append(tup.Item1)
                             .Append(". ")
                             .Append(tup.Item2.ToString()))
                        .ToString();

                    throw new InvalidOperationException(message);
                }

                binding.IsVisiting = true;
                path.Push(binding);

                try
                {
                    var dependencies = new HashSet<Binding>();
                    binding.GetDependencies(dependencies, dependencies);
                    DetectCircularDependencies(dependencies, path);
                    binding.IsCycleFree = true;
                }
                finally
                {
                    path.Pop();
                    binding.IsVisiting = false;
                }
            }
        }
    }
}
