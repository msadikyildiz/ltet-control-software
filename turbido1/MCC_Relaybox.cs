using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using MccDaq;

namespace turbido1
{
    public class MCC_Relaybox
    {
        MccDaq.MccBoard relaybox; 
        bool[] currentPumpState = new bool[25];

        public MCC_Relaybox(int mcc_id)
        {
            relaybox = new MccDaq.MccBoard(mcc_id);

            for (int i = 1; i <= 24; i++)
            {
                TurnOff(i);
            }
        }

        public void TurnOn(int index)
        {
            relaybox.DBitOut(MccDaq.DigitalPortType.FirstPortA, index - 1, MccDaq.DigitalLogicState.High);
            Thread.Sleep(10);
            relaybox.DBitOut(MccDaq.DigitalPortType.FirstPortA, index - 1, MccDaq.DigitalLogicState.High);
            currentPumpState[index] = true;
        }

        public void TurnOff(int index)
        {
            relaybox.DBitOut(MccDaq.DigitalPortType.FirstPortA, index - 1, MccDaq.DigitalLogicState.Low);
            Thread.Sleep(10);
            relaybox.DBitOut(MccDaq.DigitalPortType.FirstPortA, index - 1, MccDaq.DigitalLogicState.Low);
            currentPumpState[index] = false;
        }
    }
}
