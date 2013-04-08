using System.Collections.Generic;

namespace Abra.Internal
{
    internal class SingletonBinding : Binding
    {
        private readonly Binding binding;
        private object instance;
        private bool initialized;

        internal override bool IsResolved
        {
            get { return binding.IsResolved; }
            set { binding.IsResolved = value; }
        }

        internal override bool IsCycleFree
        {
            get { return binding.IsCycleFree; }
            set { binding.IsCycleFree = value; }
        }

        internal override bool IsVisiting
        {
            get { return binding.IsVisiting; }
            set { binding.IsVisiting = value; }
        }

        internal SingletonBinding(Binding binding)
            : base(binding.ProviderKey, binding.MembersKey, true, binding.RequiredBy)
        {
            this.binding = binding;
        }

        internal override object Get()
        {
            if (!initialized)
            {
                lock (this)
                {
                    instance = binding.Get();
                    initialized = true;
                }
            }

            return instance;
        }

        internal override void GetDependencies(ISet<Binding> injectDependencies, ISet<Binding> propertyDependencies)
        {
            binding.GetDependencies(injectDependencies, propertyDependencies);
        }
    }
}
