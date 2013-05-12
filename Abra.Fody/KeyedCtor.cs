using System;
using Mono.Cecil;

namespace Abra.Fody
{
    public class KeyedCtor
    {
        private readonly string key;
        private readonly MethodReference ctor;

        public string Key { get { return key; } }
        public MethodReference Ctor { get { return ctor; } }

        public KeyedCtor(string key, MethodReference ctor)
        {
            this.key = Conditions.CheckNotNull(key, "key");
            this.ctor = Conditions.CheckNotNull(ctor, "ctor");
        }
    }
}
