using Abra;

namespace Example
{
    [Module]
    class PumpModule
    {
        [Provides]
        public IPump GetPump(Thermosiphon thermosiphon)
        {
            return thermosiphon;
        }
    }
}
