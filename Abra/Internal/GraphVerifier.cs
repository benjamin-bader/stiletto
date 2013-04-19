using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Abra.Internal
{
    internal class GraphVerifier
    {
        internal void VerifyModuleSpecs(IEnumerable<ModuleAttribute> modules)
        {
            DetectCircularDependencies(
                modules.Select(VisitableWrapper.Wrap),
                w => GetIncludedModules(w.Data).Select(VisitableWrapper.Wrap),
                new Stack<VisitableWrapper<ModuleAttribute>>());
        }

        internal void Verify(IEnumerable<Binding> bindings)
        {
            DetectCircularDependencies(bindings, GetDependencies, new Stack<Binding>());
        }

        private void DetectCircularDependencies<T>(
            IEnumerable<T> items,
            Func<T, IEnumerable<T>> getConnectedItems,
            Stack<T> path)
            where T : Visitable
        {
            foreach (var item in items) {
                if (item.IsCycleFree) {
                    continue;
                }

                if (item.IsVisiting) {
                    var sb = new StringBuilder("Cycle detected:").AppendLine();
                    var message = Enumerable.Range(1, path.Count)
                        .Zip(path.Reverse(), Tuple.Create)
                        .Aggregate(sb, (s, tup) =>
                            s.Append("\t")
                             .Append(tup.Item1)
                             .Append(". ")
                             .AppendLine(tup.Item2.ToString()))
                        .ToString();

                    throw new InvalidOperationException(message);
                }

                item.IsVisiting = true;
                path.Push(item);

                try {
                    DetectCircularDependencies(getConnectedItems(item), getConnectedItems, path);
                    item.IsCycleFree = true;
                }
                finally {
                    item.IsVisiting = false;
                }
            }
        }

        private static IEnumerable<ModuleAttribute> GetIncludedModules(ModuleAttribute attr)
        {
            return attr.IncludedModules.Select(t => t.GetSingleAttribute<ModuleAttribute>());
        }

        private static IEnumerable<Binding> GetDependencies(Binding binding)
        {
            var dependencies = new HashSet<Binding>();
            binding.GetDependencies(dependencies, dependencies);
            return dependencies;
        }
    }
}
