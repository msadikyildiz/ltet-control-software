using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using System.Threading;
using System.IO;

namespace turbido1
{
    public class MCC_AnalogInput
    {
        MccDaq.MccBoard DaqBoard;

        private int Rate = 500;

        public int HighChan=15, LowChan=0;
        private int lastUsedIndex = 0;

        int NumPoints = 31744000;

        private Double[] ADData; 

        private IntPtr MemHandle;

        public System.Timers.Timer tmrContinuousRead;

        // calibration vectors
        public Double[] p1, p0;
        public string dataPath = @"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\";
        public string currentCalibrationDatafile;


        public MCC_AnalogInput(int mcc_index)
        {
            DaqBoard = new MccDaq.MccBoard(0);

            ADData = new Double[HighChan-LowChan+1];
            MemHandle = MccDaq.MccService.WinBufAllocEx(NumPoints);

            //tmrContinuousRead = new System.Timers.Timer();
            //tmrContinuousRead.Interval = 5000;
            //tmrContinuousRead.Elapsed += tmrContinuousRead_Tick;
            //tmrContinuousRead.AutoReset = true;


            // initialize
            p0 = new Double[HighChan+1]; p1 = new Double[HighChan+1];
            for (int i = 0; i <= HighChan; i++)
            { p1[i] = 1; p0[i] = 0; }
        }

        public void readBlank()
        {
            Double[] OD = StartSingleReadingWindow(5,"OD");
            for (int i = 0; i < OD.Length; i++)
                p0[i] += (-OD[i]);

            string filenameAutoCal = "LaserCalibrationAutoCal-"
                + DateTime.Now.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            using (StreamWriter file = new StreamWriter(dataPath + filenameAutoCal))
            {
                string line_p0 = "", line_p1 = "";
                for (int j = 0; j < OD.Length; j++)
                {
                    line_p0 += p0[j].ToString() + "\t";
                    line_p1 += p1[j].ToString() + "\t";
                }
                file.WriteLine(line_p0);
                file.WriteLine(line_p1);
            }
            currentCalibrationDatafile = dataPath+filenameAutoCal;


        }

        public void readBlankA()
        {
            Double[] OD = StartSingleReadingWindow(3, "OD");
            for (int i = 0; i < 4; i++)
            {
                p0[i] += (-OD[i]);
                p0[8+i] += (-OD[8+i]);
            }

            string filenameAutoCal = "LaserCalibrationAutoCal-"
                + DateTime.Now.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            using (StreamWriter file = new StreamWriter(dataPath + filenameAutoCal))
            {
                string line_p0 = "", line_p1 = "";
                for (int j = 0; j < OD.Length; j++)
                {
                    line_p0 += p0[j].ToString() + "\t";
                    line_p1 += p1[j].ToString() + "\t";
                }
                file.WriteLine(line_p0);
                file.WriteLine(line_p1);
            }
            currentCalibrationDatafile = dataPath + filenameAutoCal;
        }

        public void readBlankB()
        {
            Double[] OD = StartSingleReadingWindow(3, "OD");
            for (int i = 0; i < 4; i++)
            {
                p0[4 + i] += (-OD[4 + i]);
                p0[12 + i] += (-OD[12 + i]);
            }

            string filenameAutoCal = "LaserCalibrationAutoCal-"
                + DateTime.Now.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            using (StreamWriter file = new StreamWriter(dataPath + filenameAutoCal))
            {
                string line_p0 = "", line_p1 = "";
                for (int j = 0; j < OD.Length; j++)
                {
                    line_p0 += p0[j].ToString() + "\t";
                    line_p1 += p1[j].ToString() + "\t";
                }
                file.WriteLine(line_p0);
                file.WriteLine(line_p1);
            }
            currentCalibrationDatafile = dataPath + filenameAutoCal;
        }

        public void updateCalibrationVectors(float[] np1, float[] np0, string datafile)
        {
            np1.CopyTo(p1, 0);
            np0.CopyTo(p0, 0);
            currentCalibrationDatafile = datafile;
        }

        public Double[] returnLastRead()
        {
            return ADData;
        }

        public Double[] returnLastCalibratedODValue()
        {
            return convertADtoOD(ADData);
        }

        private Double[] convertADtoOD(Double[] AD)
        {
            Double[] OD = new Double[HighChan - LowChan + 1];
            Double odc;
            for (int i = LowChan; i <= HighChan; i++)
            {
                odc = (Math.Log(AD[i]) * p1[i]) + p0[i];
                if (odc <= 1e2)
                    OD[i] = odc;
                else
                    OD[i] = (float)-100;
            }
            return OD;
        }

        public void startContinuousRead()
        {

            //  per channel sampling rate ((samples per second) per channel)
            MccDaq.ScanOptions Options = MccDaq.ScanOptions.Background
                    | MccDaq.ScanOptions.Continuous;

            //  collect data in background continuously
            DaqBoard.AInScan(LowChan, HighChan, NumPoints,
                ref Rate, MccDaq.Range.Bip10Volts, MemHandle, Options);
            tmrContinuousRead.Start();
        }

        public void stopContinuousRead()
        {
            tmrContinuousRead.Enabled = false;
            DaqBoard.StopBackground(MccDaq.FunctionType.AiFunction);
            MccDaq.MccService.WinBufFreeEx(MemHandle); 
        }

        public void tmrContinuousRead_Tick(object eventSender, ElapsedEventArgs e)
        {
            int i,j;
            int FirstPoint, NumChans=HighChan-LowChan+1, CurIndex, CurCount;
            short Status;
            DaqBoard.GetStatus(out Status, out CurCount,
                out CurIndex, MccDaq.FunctionType.AiFunction);
            FirstPoint = CurIndex;

            // if buffer is full, recycle.
            if (lastUsedIndex > FirstPoint)
            { lastUsedIndex = FirstPoint; tmrContinuousRead.Start(); return; }

            // recently collected data
            int N = FirstPoint - lastUsedIndex + NumChans;
            ushort[] addata = new ushort[N];
            MccDaq.MccService.WinBufToArray(MemHandle, addata, lastUsedIndex, N);
            lastUsedIndex = lastUsedIndex + N;

            //float sum = 0;
            List<float> channel_data = new List<float>();
            for (i = 0; i <= HighChan; ++i)
            {
                //sum = 0;
                channel_data.RemoveRange(0, channel_data.Count);
                for (j = i; j < N; j += NumChans)
                    //sum += addata[j];
                    channel_data.Add(addata[j]);
                // take mean of the collected samples
                // ADData[i] = (sum / (N / NumChans));
                // take median of the collected samples
                channel_data.Sort();
                ADData[i] = channel_data[(Int32)(channel_data.Count / 2)];
                // convert from int to double precision voltage value
                ADData[i] = (ADData[i] - 32768) / (float)3276.8;
            }
        }

        public Double[] StartSingleReadingWindow(double time, string output_fmt="OD")
        {
            DaqBoard = new MccDaq.MccBoard(0);

            MccDaq.ErrorInfo ULStat;
            int FirstPoint, NumChans = HighChan - LowChan + 1, CurIndex, CurCount;
            short Status;
            NumPoints = (int)(time) * Rate * NumChans;
            MemHandle = MccDaq.MccService.WinBufAllocEx(10*NumPoints);
             
            Thread.Sleep(100);
            MccDaq.ScanOptions Options = MccDaq.ScanOptions.ConvertData;
            ULStat=DaqBoard.AInScan(LowChan, HighChan, NumPoints, ref Rate,
                               MccDaq.Range.Bip10Volts, MemHandle, Options);
            DaqBoard.GetStatus(out Status, out CurCount,
                out CurIndex, MccDaq.FunctionType.AiFunction);
            FirstPoint = CurIndex;
            
            // recently collected data
            int N = FirstPoint + NumChans;
            ushort[] addata = new ushort[N];
            MccDaq.MccService.WinBufToArray(MemHandle, addata, 0, N);
            
            List<float> channel_data = new List<float>();
            for (int i = 0; i <= HighChan; ++i)
            {
                //sum = 0;
                channel_data.RemoveRange(0, channel_data.Count);
                for (int j = i; j < N; j += NumChans)
                    //sum += addata[j];
                    channel_data.Add(addata[j]);
                // take median voltage value
                channel_data.Sort();
                ADData[i] = channel_data[(Int32)(channel_data.Count / 2)];
                // convert from int to double precision voltage value
                ADData[i] = (ADData[i] - 32768) / (float)3276.8;
            }

            DaqBoard.StopBackground(MccDaq.FunctionType.AiFunction);
            MccDaq.MccService.WinBufFreeEx(MemHandle);
            if (output_fmt == "OD")
                return convertADtoOD(ADData);
            else
                return ADData;
        }
    }
}
