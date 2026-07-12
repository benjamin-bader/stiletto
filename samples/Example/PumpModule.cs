using Stiletto;

namespace Example
{
    [Module(IsComplete = false)]
    class PumpModule
    {
        [Provides]
        public IPump GetPump(Thermosiphon thermosiphon)
        {
            return thermosiphon;
        }
    }
}
