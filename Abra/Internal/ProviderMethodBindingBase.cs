namespace Abra.Internal
{
    public abstract class ProviderMethodBindingBase : Binding
    {
        private readonly string moduleName;
        private readonly string methodName;

        public string ProviderMethodName
        {
            get { return moduleName + "." + methodName; }
        }

        public ProviderMethodBindingBase(
            string providerKey, string membersKey, bool isSingleton, object requiredBy,
            string moduleName, string methodName)
            : base(providerKey, membersKey, isSingleton, requiredBy)
        {
            this.moduleName = moduleName;
            this.methodName = methodName;
        }
    }
}
