using System;
using Stiletto;

namespace LibraryExample
{
    public class IntelligentsiaBeans : IBeans
    {
        public string Origin { get; private set; }
        public DateTime RoastedOn { get; private set; }

        [Inject]
        public IntelligentsiaBeans(string origin, DateTime roastedOn)
        {
            Origin = origin;
            RoastedOn = roastedOn;
        }
    }
}
