using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public SingletonBinding(Binding binding)
            : base(binding.ProviderKey, binding.MembersKey, true, binding.RequiredBy)
        {
            this.binding = binding;
        }

        public object Get()
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

        public void GetDependencies(ISet<IBinding> bindings)
        {
            binding.GetDependencies(bindings);
        }
    }
}
