using System;
using Abra;

namespace Example
{
    class Thermosiphon : IPump
    {
        private readonly IHeater heater;

        [Inject]
        public Thermosiphon(IHeater heater)
        {
            this.heater = heater;
        }

        public void Pump()
        {
            if (heater.IsHot) {
                Console.WriteLine("~~~~pumping~~~~");
            }
        }
    }
}
