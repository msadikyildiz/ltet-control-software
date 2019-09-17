using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Phidgets;
using Phidgets.Events;

namespace turbido1
{
    public class Phidget_Interface
    {
        public Bridge bridge;

        // scale calibration parameters
        float[] p0 = { (float)10721.538461, (float)10721.538461 }, p1 = { (float)15384.615384, (float)15384.615384 };
        string data_files_path = @"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\";
        
        public Phidget_Interface()
        {
            bridge = new Bridge();
            bridge.BridgeData += new BridgeDataEventHandler(this.bridge_BridgeData);

            // parse in calibration parameters
            try
            {

                string[] scales = File.ReadAllLines(data_files_path+ "last_scale_calibration.txt");
                p1[0] = Single.Parse(scales[0].Split(' ')[0]);
                p0[0] = Single.Parse(scales[0].Split(' ')[1]);
                p1[1] = Single.Parse(scales[1].Split(' ')[0]);
                p0[1] = Single.Parse(scales[1].Split(' ')[1]);
            }
            catch 
            {

            }
        }
        
        public float[,] last_values = new float[2,128];
        public float[,] last_values_raw = new float[2, 128];

        int[] datacounters = {0,0};

        public void bridge_BridgeData(object sender, BridgeDataEventArgs e)
        {
            last_values[e.Index, datacounters[e.Index]] = p1[e.Index] * (float)e.Value + p0[e.Index];
            last_values_raw[e.Index, datacounters[e.Index]] = (float)e.Value;
            if (++datacounters[e.Index] > 127)
                datacounters[e.Index] = 0;
        }

        public void recalibrateScale(int scaleIndex, float lowRaw, float lowSet, float highRaw, float highSet)
        {
            p1[scaleIndex] = (highSet - lowSet) /  (highRaw - lowRaw);
            p0[scaleIndex] = lowSet - lowRaw * p1[scaleIndex];
            string text = p1[0].ToString() + " " + p0[0].ToString() + Environment.NewLine
                + p1[1].ToString() + " " + p0[1].ToString() + Environment.NewLine;
            File.WriteAllText(data_files_path+@"last_scale_calibration.txt", text);
            
        }

        public void startContinuousRead()
        {
            bridge.open();
            bridge.waitForAttachment();
            bridge.bridges[0].Enabled = true;
            bridge.bridges[0].Gain = BridgeInput.Gains.GAIN_128;
            bridge.bridges[1].Enabled = true;
            bridge.bridges[1].Gain = BridgeInput.Gains.GAIN_128;
            //bridge.bridges[2].Enabled = true;
            //bridge.bridges[2].Gain = BridgeInput.Gains.GAIN_128;
        }

        public void stopContinuousRead()
        {
            bridge.close();
        }


        public Double[] Read()
        {
            //if (datacounters[0] < 32 || datacounters[1] < 32)
            //{
            //    Thread.Sleep(10);
            //    return Read();
            //}
            Double[] means={0,0};
            for (int index = 0; index < 2; index++)
            {
                for (int i = 0; i < 127; i++)
                    means[index] += last_values[index, i];
                means[index] /= 127;
            }
            return means;
        }

        public float[] ReadRaw()
        {
            float[] means = { 0, 0 };
            for (int index = 0; index < 2; index++)
            {
                for (int i = 0; i < datacounters[index]; i++)
                    means[index] += last_values_raw[index, i];
                means[index] /= datacounters[index];
            }
            return means;
        }
    }
}
