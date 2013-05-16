using System;
using Abra;

namespace LibraryExample
{
    [Module(IsComplete = false, IsLibrary = true)]
    public class BeanModule
    {
        [Provides]
        public IBeans RoastSomeCoffee([Named("coffee-source")] string origin)
        {
            return new IntelligentsiaBeans(origin, DateTime.Now);
        }
    }
}
