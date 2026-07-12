using System;
using Stiletto;
using LibraryExample;

namespace Example
{
    class CoffeeMaker
    {
        [Inject]
        public Lazy<IHeater> Heater { get; set; }

        [Inject]
        public IPump Pump { get; set; }

        [Inject]
        public IBeans Beans { get; set; }

        public void Brew()
        {
            Heater.Value.On();
            Pump.Pump();
            Console.WriteLine("[_]P coffee from {0} is ready!", Beans.Origin);
            Heater.Value.Off();
        }
    }
}
