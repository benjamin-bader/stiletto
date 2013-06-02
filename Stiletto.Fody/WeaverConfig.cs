using System.Xml.Linq;

namespace Stiletto.Fody
{
    public class WeaverConfig
    {
        public bool SuppressUnusedBindingErrors { get; private set; }

        private WeaverConfig()
        {
        }

        public static WeaverConfig Load(XElement config)
        {
            var noUnusedBindingErrs = (bool?) config.Attribute("SuppressUnusedBindingsErrors")
                                   ?? (bool?) config.Element("SuppressUnusedBindingsErrors");

            return new WeaverConfig
                   {
                       SuppressUnusedBindingErrors = noUnusedBindingErrs ?? false,
                   };
        }
    }
}
