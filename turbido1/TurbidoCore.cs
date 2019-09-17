 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Collections.ObjectModel;

namespace turbido1
{
    public class TurbidoCore
    {
        public MCC_AnalogInput ODReader = new MCC_AnalogInput(0);
        public MCC_Relaybox relaybox1 = new MCC_Relaybox(1);
        public MCC_Relaybox relaybox2 = new MCC_Relaybox(2);
        public MCC_Relaybox relaybox3 = new MCC_Relaybox(3);
        public MCC_Relaybox[] relays;
        public Phidget_Interface load_sensors = new Phidget_Interface();
        public Stopwatch globalWatch = new Stopwatch();
        public TurbidoAlgorithmLibrary library;
        public TurbidostatControlPanel controlPanel;
        public CultureSimulator sim;
        public System.Timers.Timer DataCollector;
        System.Timers.Timer ScaleDataCollector;
        System.Timers.Timer DataStreamer;
        System.Timers.Timer PrimitiveDataSaver;
        public System.Timers.Timer ReinitializeODReader;
        string previousDataFile = "", previousDataFileScale = "";

        public ODMonitor odm;
        public ScaleMonitor sm;
        public DoubleArrayRef lastOD, lastRawOD, lastMeanOD, lastScale, lastMeanScale;
        public DoubleRef lastTime;
        public List<List<float>> OD = new List<List<float>>();
        public List<float> ODTime = new List<float>();
        public List<List<float>> Scale = new List<List<float>>();
        public List<float> ScaleTime = new List<float>();
        public List<List<UInt16>> Phase = new List<List<UInt16>>();
        public List<List<UInt16>> ScalePhase = new List<List<UInt16>>();
        public ObservableCollection<LogEntry> MainLog = new ObservableCollection<LogEntry>();

        public int[,] ASetThreeWayRelayIDs;
        public int[,] BSetThreeWayRelayIDs;
        public int[,] ASetIsolationValveRelayIDs;
        public int[,] BSetIsolationValveRelayIDs;
        public int[] AirValveToIBA, MediaValveToIBA, WaterValveToIBA, EtOHValveToIBA, BleachValveToIBA;
        public int[] AirValveToIBB, MediaValveToIBB, WaterValveToIBB, EtOHValveToIBB, BleachValveToIBB;
        public int[] KeepingLevelBlockA, KeepingLevelBlockB;
        public int[] AirValveToMediaRezervoire, AirValveToTubeAs, AirValveToTubeBs;
        public int[] AirMixerA, AirMixerB, AirMixerMedia;
        public int[] Drug1ValveToIBA, Drug1ValveToIBB, AirValveToDrug1, AirMixerDrug1;
        public int[] Drug2ValveToIBA, Drug2ValveToIBB, AirValveToDrug2, AirMixerDrug2;
        public int[] IPTGValveToIBA, IPTGValveToIBB, AirValveToIPTG, AirMixerIPTG;
        public int[] LEDPDSwitch;

        
        // global parameters
        int safety_waiting = 1;
        int conc_media_pushback_time = 30; // secs
        public int depressurizationTime = 10; // secs
        public int IBPressurizationTime = 15; // secs
        int dilutionMixTime = 200; // secs
        public int keepingLevelTime = 20; // secs
        double LBRatio = 9.5;
        double BleachRatio = 5;
        public bool simulationMode = false;

        // safety parameters
        public int safetyMaxSecondsDilutionAllowed = 45; // secs

        // asynchronous evacuation
        public bool isKeepingLevelActiveA = false;
        public bool isKeepingLevelActiveB = false;

        // smart dilution
        double[] smartDilRateEstA = {1.2, 1.2, 1.2, 1.2, 1.2, 1.2, 1.2, 1.2};
        double[] smartDilRateEstB = {1.2, 1.2, 1.2, 1.2, 1.2, 1.2, 1.2, 1.2};
        double smartDilMaxTime = 9; // secs

        public TurbidoCore()
        {
            // relay configuration (see the documentation for details)
            relays = new MCC_Relaybox[] { relaybox1, relaybox2, relaybox3 };
            ASetThreeWayRelayIDs = new int[,] { { 1, 8 }, { 1, 6 }, { 1, 4 }, { 1, 2 },
                                              { 0, 13 }, { 0, 15 }, { 0, 17 }, { 0, 19 } };
            BSetThreeWayRelayIDs = new int[,] { { 1, 7 }, { 1, 5 }, { 1, 3 }, { 1, 1 },
                                              { 0, 14 }, { 0, 16 }, { 0, 18 }, { 0, 21 } };
            ASetIsolationValveRelayIDs = new int[,] { { 1, 24 }, { 1, 23 }, { 1, 22 }, { 1, 21 },
                                                    { 1, 16 }, { 1, 15 }, { 1, 14 }, { 1, 13 } };
            BSetIsolationValveRelayIDs = new int[,] { { 1, 20 }, { 1, 19 }, { 1, 18 }, { 1, 17 },
                                                    { 1, 12 }, { 1, 11 }, { 1, 10 }, { 1, 9 } };
            AirValveToIBA = new int[] { 0, 20 };
            AirValveToIBB = new int[] { 0, 12 };
            MediaValveToIBA = new int[] { 0, 1 };
            MediaValveToIBB = new int[] { 0, 2 };
            KeepingLevelBlockA = new int[] { 2, 11 };
            KeepingLevelBlockB = new int[] { 2, 12 };
            BleachValveToIBA = new int[] { 0, 5 };
            BleachValveToIBB = new int[] { 0, 6 };
            AirValveToTubeAs = new int[] { 0, 8 };
            AirValveToTubeBs = new int[] { 0, 7 };
            AirValveToMediaRezervoire = new int[] { 0, 22 };
            //WaterValveToIBA = new int[] { 0, 23 };
            WaterValveToIBA = new int[] { 2, 8 };
            WaterValveToIBB = new int[] { 2, 7 };
            AirMixerA = new int[] { 0, 9 };
            AirMixerB = new int[] { 0, 10 };
            AirMixerMedia = new int[] { 0, 11 };

            Drug1ValveToIBA = new int[] { 2, 24 };
            Drug1ValveToIBB = new int[] { 2, 23 };
            AirMixerDrug1 = new int[] { 2, 22 };
            AirValveToDrug1 = new int[] { 2, 21 };
            Drug2ValveToIBA = new int[] { 2, 20 };
            Drug2ValveToIBB = new int[] { 2, 19 };
            AirMixerDrug2 = new int[] { 2, 18 };
            AirValveToDrug2 = new int[] { 2, 17 };
            IPTGValveToIBA = new int[] { 2, 16 };
            IPTGValveToIBB = new int[] { 2, 15 };
            AirMixerIPTG = new int[] { 2, 14 };
            AirValveToIPTG = new int[] { 2, 13 };

            LEDPDSwitch = new int[] { 2, 1 }; 

            // initialize data arrays
            for (int i = 0; i <= ODReader.HighChan; i++)
            {
                OD.Add(new List<float>());
                Phase.Add(new List<UInt16>());
            }
            Scale.Add(new List<float>());
            Scale.Add(new List<float>());
            ScalePhase.Add(new List<UInt16>());
            ScalePhase.Add(new List<UInt16>());
            lastOD = new DoubleArrayRef(ODReader.HighChan + 1);
            lastMeanOD = new DoubleArrayRef(ODReader.HighChan + 1);
            lastRawOD = new DoubleArrayRef(ODReader.HighChan + 1);
            lastScale = new DoubleArrayRef(2);
            lastMeanScale = new DoubleArrayRef(2);
            lastTime = new DoubleRef();

            // load default laser calibration
            string[] files = Directory.GetFiles(@"C:\Users\Turbidostat\Desktop\turbido_data\calibrations",
                "LaserCalibration*.csv");
            loadNewLaserCalibration(files[files.Length - 1]);

            // start data stream
            globalWatch.Start();
            load_sensors.startContinuousRead();
            DataCollector = new System.Timers.Timer(10 * 1000);
            DataCollector.Elapsed += DataCollector_Tick;
            DataCollector.AutoReset = false;
            //DataCollector.Start();
            ScaleDataCollector = new System.Timers.Timer(1000);
            ScaleDataCollector.Elapsed += ScaleDataCollector_Tick;
            ScaleDataCollector.AutoReset = false;
            ScaleDataCollector.Start();

            //DataStreamer = new System.Timers.Timer(10 * 1000);
            //DataStreamer.Elapsed += DataStreamer_Tick;
            //DataStreamer.Start();
            
            ReinitializeODReader = new System.Timers.Timer(3600 * 1000);
            ReinitializeODReader.Elapsed += ReinitializeODReader_Tick;
            ReinitializeODReader.Start();
        }

        public void assignMonitors(ref ODMonitor odm_, ref ScaleMonitor sm_)
        {
            odm = odm_;
            sm = sm_;
        }
        
        public void ReinitializeODReader_Tick(object state, ElapsedEventArgs e)
        {
            //DataStreamer.Stop();
            DataCollector.Stop();
            //ODReader.stopContinuousRead();
            ODReader = new MCC_AnalogInput(3);

            // load last selected calibration file
            if (controlPanel != null)
            {
                loadNewLaserCalibration(controlPanel.SelectedLaserCalibrationPath());
            }
            // if does not exist load default laser calibration
            else
            {
                string[] files = Directory.GetFiles(@"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\",
                    "LaserCalibration*.csv");
                loadNewLaserCalibration(files[files.Length - 1]);
            }
            //ODReader.tmrContinuousRead.Interval = 1000;
            //ODReader.startContinuousRead();
            DataCollector.Start();
            //Thread.Sleep(10);
            //DataStreamer.Start();
        }

        public void DataCollector_Tick(object state, ElapsedEventArgs e)
        {
           
            if (simulationMode)
            {
                Double elapsedTime = (sim.simWatch.ElapsedMilliseconds / 1000.0);
                lastOD = sim.lastOD;
                lastRawOD = sim.lastOD;
                lastTime = sim.lastTime;
                AddODData(elapsedTime, lastOD.values);
            }
            else
            {
                Double elapsedTime = (globalWatch.ElapsedMilliseconds / 1000.0);
                // Turn on LED/PDs, and measure OD
                relays[LEDPDSwitch[0]].TurnOn(LEDPDSwitch[1]);
                lastOD.values = ODReader.StartSingleReadingWindow(1,"OD");
                relays[LEDPDSwitch[0]].TurnOff(LEDPDSwitch[1]);
                lastRawOD.values = lastOD.values;
                lastTime.val = elapsedTime;
                // Skip failed analog reads
                if (lastOD.values[0] > -10)
                    AddODData(elapsedTime, lastOD.values);

                // built-in data streamer
                if (odm != null && sm != null && ODTime.Count > 0 && ScaleTime.Count > 0 && lastOD.values[0] > -10)
                {
                    try
                    {
                        odm.AddData(ODTime[ODTime.Count - 1], lastOD.values);
                        sm.AddData(ScaleTime[ScaleTime.Count - 1], lastScale.values);
                    }
                    catch (ArgumentException exp)
                    {
                        odm.AddData(ODTime[ODTime.Count - 1], lastOD.values);
                        sm.AddData(ScaleTime[ScaleTime.Count - 1], lastScale.values);
                    }
                }
            }
            DataCollector.Start();
        }

        public void ScaleDataCollector_Tick(object sate, ElapsedEventArgs e)
        {
            if (load_sensors.bridge.Attached)
            {
                Double elapsedTime = (globalWatch.ElapsedMilliseconds / 1000.0);
                lastScale.values = load_sensors.Read();
                AddScaleData(elapsedTime, lastScale.values);
            }
            ScaleDataCollector.Start();
        }

        public void DataStreamer_Tick(object state, ElapsedEventArgs e)
        {
            if (odm!=null && sm!=null && ODTime.Count > 0  && ScaleTime.Count > 0 && lastOD.values[0]>-10)
            {
                try
                {
                    odm.AddData(ODTime[ODTime.Count - 1], lastOD.values);
                    sm.AddData(ScaleTime[ScaleTime.Count - 1], lastScale.values);
                }
                catch (ArgumentException exp)
                {
                    odm.AddData(ODTime[ODTime.Count - 1], lastOD.values);
                    sm.AddData(ScaleTime[ScaleTime.Count - 1], lastScale.values);
                }
            }
        }

        public void PrimitiveDataSaver_Tick(object state, ElapsedEventArgs e)
        {
            DateTime saveNow = DateTime.Now;
            string path = @"C:\Users\Turbidostat\Desktop\turbido_data\";
            string filename = "OD-" + saveNow.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            string csv_line;

            using (StreamWriter file = new StreamWriter(path + filename))
            {
                for (int i = 0; i < ODTime.Count; i++)
                {
                    csv_line = "";
                    for (int j = 0; j < OD.Count; j++)
                    {
                        try
                        {
                            csv_line += OD[j][i].ToString() + ",";
                        }
                        catch { }
                    }
                    csv_line += ODTime[i].ToString();
                    file.WriteLine(csv_line);
                }
            }

            string filenameScale = "Scale-" + 
                                    saveNow.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";

            using (StreamWriter file = new StreamWriter(path + filenameScale))
            {
                for (int i = 0; i < ScaleTime.Count; i++)
                {
                    csv_line = "";
                    for (int j = 0; j < Scale.Count; j++)
                    {
                        try
                        {
                            csv_line += Scale[j][i].ToString() + ",";
                        }
                        catch { }
                    }
                    csv_line += ScaleTime[i].ToString();
                    file.WriteLine(csv_line);
                }
            }
            // delete the previous version of the data
            if (previousDataFile != "")
            {
                try
                {
                    File.SetAttributes(path + previousDataFile, FileAttributes.Normal);
                    File.Delete(path + previousDataFile);
                }
                catch
                { }
            }
            previousDataFile = filename;
            
            // delete the previous version of the data
            if (previousDataFileScale != "")
            {
                try
                {
                    File.SetAttributes(path + previousDataFileScale, FileAttributes.Normal);
                    File.Delete(path + previousDataFileScale);
                }
                catch
                { }
            }
            previousDataFileScale = filenameScale;
        }

        public void AddODData(Double time, Double[] od)
        {
            ODTime.Add((float)time);
            for (int i = 0; i < OD.Count; i++)
            {
                OD[i].Add((float)od[i]);
                if (library != null)
                    Phase[i].Add(((UInt16)library.currentPhase[i]));
            }
            
            int window = 10; float mean;
            if (OD[0].Count > window)
            {
                for (int i = 0; i < OD.Count; i++)
                {
                    mean = 0;
                    for (int j = 1; j <= window; j++)
                        mean += OD[i][OD[i].Count - j];
                    mean /= window;
                    lastMeanOD.values[i] = mean;
                }
            }
        }

        public void AddScaleData(Double time, Double[] scale)
        {
            ScaleTime.Add((float)time);
            for (int i = 0; i < Scale.Count; i++)
            {
                Scale[i].Add((float)scale[i]);
                if (library != null)
                    ScalePhase[i].Add((UInt16)library.currentScalePhase[i]);
            }

            int window = 5; float mean;
            if (Scale[0].Count > window)
            {
                for (int i = 0; i < Scale.Count; i++)
                {
                    mean = 0;
                    for (int j = 1; j <= window; j++)
                        mean += Scale[i][Scale[i].Count - j];
                    mean /= window;
                    lastMeanScale.values[i] = mean;
                }
            }
        }

        public void loadNewLaserCalibration(string path)
        {
            string[] cal = File.ReadAllLines(path);
            float[] p1 = new float[ODReader.HighChan + 1], p0 = new float[ODReader.HighChan + 1];
            int i = 0;
            foreach (string s in cal[0].Split('\t'))
                if (s != "")
                    p0[i++] = Single.Parse(s);
            i = 0;
            foreach (string s in cal[1].Split('\t'))
                if (s != "")
                    p1[i++] = Single.Parse(s);
            ODReader.updateCalibrationVectors(p1, p0, path);
        }

        // valve controls

        public void logMain(string message)
        {
            MainLog.Add(new LogEntry() {time=DateTime.Now, message = message});
        }

        public void DebubbleTubeAs(double ml)
        {
            (new Thread(() => { DebubbleTubeAs_worker(ml); })).Start();
        }
        public void DebubbleTubeAs_worker(double ml)
        {
            // pressurize IBA
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            Thread.Sleep(depressurizationTime*1000);
            for (int i = 0; i < 8; i++)
            {
                // open tube input
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOn(ASetIsolationValveRelayIDs[i, 1]);
                // wait until 20 ml lb is gone
                double initial_weight = load_sensors.Read()[1];
                while ((load_sensors.Read()[1] - initial_weight) < ml)
                    Thread.Sleep(50);
                // close tube input
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOff(ASetIsolationValveRelayIDs[i, 1]);
            }

            // despressurize IBA
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
        }

        public void DebubbleTubeBs(double ml)
        {
            (new Thread(() => { DebubbleTubeBs_worker(ml); })).Start();
        }
        public void DebubbleTubeBs_worker(double ml)
        {
            // pressurize IBA
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            Thread.Sleep(depressurizationTime*1000);
            for (int i = 0; i < 8; i++)
            {
                // open tube input
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOn(BSetIsolationValveRelayIDs[i, 1]);
                // wait until 20 ml lb is gone
                double initial_weight = load_sensors.Read()[0];
                while ((load_sensors.Read()[0] - initial_weight) < ml)
                    Thread.Sleep(50);
                // close tube input
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOff(BSetIsolationValveRelayIDs[i, 1]);
            }
            // despressurize IBB
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }

        public void KeepLevelA(double time)
        {
            (new Thread(() => { KeepLevelA_worker(time); })).Start();
        }
        public void KeepLevelA_worker(double time)
        {
            // turn on keeping level line
            relays[KeepingLevelBlockA[0]].TurnOn(KeepingLevelBlockA[1]);
            // pressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            Thread.Sleep((Int32)(time * 1000));
            // turn off keeping level line
            relays[KeepingLevelBlockA[0]].TurnOff(KeepingLevelBlockA[1]);
            // depressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
        }

        public void KeepLevelB(double time)
        {
            (new Thread(() => { KeepLevelB_worker(time); })).Start();
        }
        public void KeepLevelB_worker(double time)
        {
            // turn on keeping level line
            relays[KeepingLevelBlockB[0]].TurnOn(KeepingLevelBlockB[1]);
            // pressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            Thread.Sleep((Int32)(time * 1000));
            // turn off keeping level line
            relays[KeepingLevelBlockB[0]].TurnOff(KeepingLevelBlockB[1]);
            // depressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
        }

        public void AltKeepLevelA(double time)
        {
            (new Thread(() => { AltKeepLevelA_worker(time); })).Start();
        }
        public void AltKeepLevelA_worker(double time)
        {
            // asynchronous evacuation
            while (isKeepingLevelActiveB)
                Thread.Sleep(1000);
            isKeepingLevelActiveA = true;

            // change to evacuation line configuration
            for (int i = 0; i < 8; i++)
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
            //logMain("Culture As are in evacuation mode.");
            // pressurize tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            //logMain("Tube As are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valves
            for (int i = 0; i < 8; i++)
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
            //logMain("Culture As are isolated.");

            isKeepingLevelActiveA = false;
        }

        public void AltKeepLevelB(double time)
        {
            (new Thread(() => { AltKeepLevelB_worker(time); })).Start();
        }
        public void AltKeepLevelB_worker(double time)
        {
            // asynchronous evacuation
            while (isKeepingLevelActiveA)
                Thread.Sleep(1000);
            isKeepingLevelActiveB = true;
            
            // change to evacuation line configuration
            for (int i = 0; i < 8; i++)
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            //logMain("Culture Bs are in evacuation mode.");
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            //logMain("Tube Bs are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valves
            for (int i = 0; i < 8; i++)
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            //logMain("Culture As are isolated.");

            isKeepingLevelActiveB = false;
        }

        public void AltKeepLevelASelect(List<int> cultureIDs, double time)
        {
            (new Thread(() => { AltKeepLevelASelect_worker(cultureIDs, time); })).Start();
        }
        public void AltKeepLevelASelect_worker(List<int> cultureIDs, double time)
        {
            // asynchronous evacuation
            while (isKeepingLevelActiveB)
                Thread.Sleep(1000);
            isKeepingLevelActiveA = true;

            // change to evacuation line configuration
            foreach (int cID in cultureIDs)
                relays[ASetThreeWayRelayIDs[cID, 0]].TurnOn(ASetThreeWayRelayIDs[cID, 1]);
            //logMain("Culture As are in evacuation mode.");
            // pressurize tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            //logMain("Tube As are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valves
            foreach (int cID in cultureIDs)
                relays[ASetThreeWayRelayIDs[cID, 0]].TurnOff(ASetThreeWayRelayIDs[cID, 1]);
            //logMain("Culture As are isolated.");

            isKeepingLevelActiveA = false;
        }

        public void AltKeepLevelBSelect(List<int> cultureIDs, double time)
        {
            (new Thread(() => { AltKeepLevelBSelect_worker(cultureIDs, time); })).Start();
        }
        public void AltKeepLevelBSelect_worker(List<int> cultureIDs, double time)
        {
            // asynchronous evacuation
            while (isKeepingLevelActiveA)
                Thread.Sleep(1000);
            isKeepingLevelActiveB = true;

            // change to evacuation line configuration
            foreach (int cID in cultureIDs)
                relays[BSetThreeWayRelayIDs[cID, 0]].TurnOn(BSetThreeWayRelayIDs[cID, 1]);
            //logMain("Culture Bs are in evacuation mode.");
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            //logMain("Tube Bs are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valves
            foreach (int cID in cultureIDs)
                relays[BSetThreeWayRelayIDs[cID, 0]].TurnOff(BSetThreeWayRelayIDs[cID, 1]);
            //logMain("Culture As are isolated.");

            isKeepingLevelActiveB = false;
        }

public void DiluteTubeA(List<int> cultureIDs, double time)
        {
            (new Thread(() => { DiluteTubeA_worker(cultureIDs, time); })).Start();
        }
        public void DiluteTubeA_worker(List<int> cultureIDs, double time)
        {
            KeepLevelA_worker(keepingLevelTime);
            // wait for depressurization
            Thread.Sleep(3000);
            // dilute
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            // wait for pressure stability
            Thread.Sleep(depressurizationTime * 1000);
            foreach (int cID in cultureIDs)
                relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOn(ASetIsolationValveRelayIDs[cID, 1]);

            Thread.Sleep((Int32)(time * 1000));

            foreach (int cID in cultureIDs)
                relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOff(ASetIsolationValveRelayIDs[cID, 1]);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
        }

        public void DiluteTubeAByScale(List<int> cultureIDs, double ml)
        {
            (new Thread(() => { DiluteTubeAByScale_worker(cultureIDs, ml); })).Start();
        }
        public void DiluteTubeAByScale_worker(List<int> cultureIDs, double ml)
        {
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            KeepLevelA_worker(keepingLevelTime);
            if (IBPressurizationTime > keepingLevelTime)
                Thread.Sleep((IBPressurizationTime - keepingLevelTime) * 1000);

            double current_w, initial_w;
            foreach (int cID in cultureIDs)
            {
                initial_w = current_w = this.lastMeanScale.values[1];
                while (initial_w-current_w < ml)
                {
                    relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOn(ASetIsolationValveRelayIDs[cID, 1]);
                    Thread.Sleep((Int32)(1000));
                    relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOff(ASetIsolationValveRelayIDs[cID, 1]);
                    Thread.Sleep(6 * 1000);
                    current_w = this.lastScale.values[1];
                }
            }
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
        }

        public void DiluteTubeBByScale(List<int> cultureIDs, double ml)
        {
            (new Thread(() => { DiluteTubeBByScale_worker(cultureIDs, ml); })).Start();
        }
        public void DiluteTubeBByScale_worker(List<int> cultureIDs, double ml)
        {
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            KeepLevelB_worker(keepingLevelTime);
            if (IBPressurizationTime > keepingLevelTime)
                Thread.Sleep((IBPressurizationTime - keepingLevelTime) * 1000);

            double current_w, initial_w;
            foreach (int cID in cultureIDs)
            {
                initial_w = current_w = this.lastMeanScale.values[0];
                while (initial_w - current_w < ml)
                {
                    relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOn(BSetIsolationValveRelayIDs[cID, 1]);
                    Thread.Sleep((Int32)(1000));
                    relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOff(BSetIsolationValveRelayIDs[cID, 1]);
                    Thread.Sleep(12 * 1000);
                    current_w = this.lastScale.values[0];
                }
            }
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }

        public void SmartDiluteTubeA(List<int> cultureIDs, double target_od)
        {
            (new Thread(() => { SmartDiluteTubeA_worker(cultureIDs, target_od); })).Start();
        }
        public void SmartDiluteTubeA_worker(List<int> cultureIDs, double target_od)
        {
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            KeepLevelA_worker(keepingLevelTime);
            if(IBPressurizationTime > keepingLevelTime)
                Thread.Sleep((IBPressurizationTime-keepingLevelTime) * 1000);

            int channel_id;
            double current_od, v1, v0 = 6, wait_time;
            foreach (int cID in cultureIDs)
            {
                // calculate dilution time for this culture
                if (cID > 3) channel_id = cID + 4;
                else channel_id = cID;
                current_od = this.lastMeanOD.values[channel_id];
                v1 = current_od * v0 / target_od;
                wait_time = (v1 - v0) / smartDilRateEstA[cID];

                if (wait_time > smartDilMaxTime)
                    wait_time = smartDilMaxTime;

                logMain("Culture A" + (cID + 1).ToString() + " is being diluted from " + current_od.ToString() +
                        " to " + target_od.ToString() + " for " + wait_time.ToString() + " seconds.");

                // dilute this culture
                relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOn(ASetIsolationValveRelayIDs[cID, 1]);
                Thread.Sleep((Int32)(wait_time * 1000));
                relays[ASetIsolationValveRelayIDs[cID, 0]].TurnOff(ASetIsolationValveRelayIDs[cID, 1]);

                // update dilution estimation
                (new Thread(() => { SmartDiluteUpdateRateEstA(cID, current_od, wait_time); })).Start();
            }
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
        }

        public void SmartDiluteUpdateRateEstA(int cID, double initial_od, double wait_time)
        {
            Thread.Sleep(dilutionMixTime*1000);

            int channel_id;
            double current_od, v1, v0 = 6;
            if (cID > 3) channel_id = cID + 4;
            else channel_id = cID;
            current_od = this.lastMeanOD.values[channel_id];
            v1 = initial_od * v0 / current_od;

            smartDilRateEstA[cID] = (v1 - v0) / wait_time;
            logMain("Culture A" + (cID + 1).ToString() + " dilution rate estimation = " + smartDilRateEstA[cID].ToString() + "ml/sec");
        }

        public void SmartDiluteTubeB(List<int> cultureIDs, double target_od)
        {
            (new Thread(() => { SmartDiluteTubeB_worker(cultureIDs, target_od); })).Start();
        }
        public void SmartDiluteTubeB_worker(List<int> cultureIDs, double target_od)
        {
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            KeepLevelB_worker(keepingLevelTime);
            if (IBPressurizationTime > keepingLevelTime)
                Thread.Sleep((IBPressurizationTime - keepingLevelTime) * 1000);

            int channel_id;
            double current_od, v1, v0 = 6, wait_time;
            foreach (int cID in cultureIDs)
            {
                // calculate dilution time for this culture
                if (cID > 3) channel_id = cID + 8;
                else channel_id = cID + 4;
                current_od = this.lastMeanOD.values[channel_id];
                v1 = current_od * v0 / target_od;
                wait_time = (v1 - v0) / smartDilRateEstB[cID];

                if (wait_time > smartDilMaxTime)
                    wait_time = smartDilMaxTime;

                logMain("Culture B" + (cID + 1).ToString() + " is being diluted from " + current_od.ToString() +
                        " to " + target_od.ToString() + " for " + wait_time.ToString() + " seconds.");

                // dilute this culture
                relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOn(BSetIsolationValveRelayIDs[cID, 1]);
                Thread.Sleep((Int32)(wait_time * 1000));
                relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOff(BSetIsolationValveRelayIDs[cID, 1]);

                // update dilution estimation
                (new Thread(() => { SmartDiluteUpdateRateEstB(cID, current_od, wait_time); })).Start();
            }
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }

        public void SmartDiluteUpdateRateEstB(int cID, double initial_od, double wait_time)
        {
            Thread.Sleep(dilutionMixTime * 1000);

            int channel_id;
            double current_od, v1, v0 = 6;
            if (cID > 3) channel_id = cID + 8;
            else channel_id = cID + 4;
            current_od = this.lastMeanOD.values[channel_id];
            v1 = initial_od * v0 / current_od;

            smartDilRateEstB[cID] = (v1 - v0) / wait_time;
            logMain("Culture B" + (cID + 1).ToString() + " dilution rate estimation = " + smartDilRateEstB[cID].ToString() + "ml/sec");
        }

        public void DiluteTubeB(List<int> cultureIDs, double time)
        {
            (new Thread(() => { DiluteTubeB_worker(cultureIDs, time); })).Start();
        }
        public void DiluteTubeB_worker(List<int> cultureIDs, double time)
        {
            KeepLevelB_worker(keepingLevelTime);
            // wait for depressurization
            Thread.Sleep(3000);
            // dilute
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            // wait for pressure stability
            Thread.Sleep(depressurizationTime * 1000);
            foreach (int cID in cultureIDs)
                relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOn(BSetIsolationValveRelayIDs[cID, 1]);

            Thread.Sleep((Int32)(time*1000));

            foreach (int cID in cultureIDs)
                relays[BSetIsolationValveRelayIDs[cID, 0]].TurnOff(BSetIsolationValveRelayIDs[cID, 1]);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }
     
        public void PressurizeTubeA(double time)
        {
            Thread worker = new Thread(() => PressurizeTubeA_worker(time));
            worker.Start();
        }
        public void PressurizeTubeA_worker(double time)
        {
            // pressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            logMain("Tube As are pressurized.");
            // wait for specified time
            Thread.Sleep((Int32)(time * 1000));
            // depressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            logMain("Tube As are depressurized.");
        }

        public void PressurizeTubeB(double time)
        {
            Thread worker = new Thread(() => PressurizeTubeB_worker(time));
            worker.Start();
        }
        public void PressurizeTubeB_worker(double time)
        {
            // pressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            logMain("Tube Bs are pressurized.");
            // wait for specified time
            Thread.Sleep((Int32)(time * 1000));
            // depressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            logMain("Tube Bs are depressurized.");
        }

        public void TransferAtoB(double time)
        {
            Thread worker = new Thread(() => TransferAtoB_worker(time));
            worker.Start();
        }
        public void TransferAtoB_worker(double time)
        {
            // dilute before transferring
            //FillTubeAs_worker(6);
            // wait to mix well
            //Thread.Sleep(30*1000);
            // open the transfer line
            for (int i = 0; i < 8; i++)
            {
                //logMain("Transferring culture " + (i+1).ToString() + "...");
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            }
            
            // keeping level line blocked by default
            // relays[KeepingLevelBlockA[0]].TurnOff(KeepingLevelBlockA[1]);
            // relays[KeepingLevelBlockB[0]].TurnOff(KeepingLevelBlockB[1]);


            Thread.Sleep((Int32)(time * 1000));

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);

            // keeping level line blocked by default
            // open back the keeping level line
            //relays[KeepingLevelBlockA[0]].TurnOff(KeepingLevelBlockA[1]);
            //relays[KeepingLevelBlockB[0]].TurnOff(KeepingLevelBlockB[1]);
            
            for (int i = 0; i < 8; i++)
            {
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            }
        }

        public void TransferBtoA(double time)
        {
            Thread worker = new Thread(() => TransferBtoA_worker(time));
            worker.Start();
        }
        public void TransferBtoA_worker(double time)
        {
            // dilute before transferring
            //FillTubeBs_worker(6);
            // wait to mix well
            //Thread.Sleep(30 * 1000);

            // open the transfer lines
            for (int i = 0; i < 8; i++)
            {
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            }

            // keeping level line blocked by default
            // block the keeping level line
            //relays[KeepingLevelBlockA[0]].TurnOn(KeepingLevelBlockA[1]);
            //relays[KeepingLevelBlockB[0]].TurnOn(KeepingLevelBlockB[1]);
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);

            Thread.Sleep((Int32)(time * 1000));

            // depressurize tube Bs
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeBs[1]);
            // keeping level line blocked by default
            // open back the keeping level line
            //relays[KeepingLevelBlockA[0]].TurnOff(KeepingLevelBlockA[1]);
            //relays[KeepingLevelBlockB[0]].TurnOff(KeepingLevelBlockB[1]);

            for (int i = 0; i < 8; i++)
            {
                // close the transfer line
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            }
        }

        public void TransferAtoBSelect(List<int> culture_ids, double time)
        {
            Thread worker = new Thread(() => TransferAtoBSelect_worker(culture_ids, time));
            worker.Start();
        }
        public void TransferAtoBSelect_worker(List<int> culture_ids, double time)
        {
            // pressurize tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            Thread.Sleep(3000);
            // open the transfer line
            foreach (int i in culture_ids)
            {
                logMain("Transferring culture " + (i+1).ToString() + ".");
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            
                Thread.Sleep((Int32)(time * 1000));

                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            }

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
        }

        public void TransferBtoASelect(List<int> culture_ids, double time)
        {
            Thread worker = new Thread(() => TransferBtoASelect_worker(culture_ids, time));
            worker.Start();
        }
        public void TransferBtoASelect_worker(List<int> culture_ids, double time)
        {
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            Thread.Sleep(3000);

            foreach (int i in culture_ids)
            {
                logMain("Transferring culture " + (i + 1).ToString() + ".");
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
                Thread.Sleep((Int32)(time * 1000));
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            }
                
            // depressurize tube Bs
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeBs[1]);
        }


        public void FillTubeA(int cultureID, double time)
        {
            (new Thread(() => FillTubeA_worker(cultureID, time))).Start();
        }
        public void FillTubeA_worker(int cultureID, double time)
        {
            // pressurize IBA
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            // wait pressure to be stabilized
            Thread.Sleep(depressurizationTime*1000);

            relays[ASetIsolationValveRelayIDs[cultureID, 0]].TurnOn(
                ASetIsolationValveRelayIDs[cultureID, 1]);

            Thread.Sleep((Int32)(time * 1000));

            // depressurize IBA
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close the isolation valve
            relays[ASetIsolationValveRelayIDs[cultureID, 0]].TurnOff(
                ASetIsolationValveRelayIDs[cultureID, 1]);
        }

        public void FillTubeAs(double time)
        {
            (new Thread(() => FillTubeAs_worker(time))).Start();
        }
        public void FillTubeAs_worker(double time)
        {
            // pressurize IBA
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            // wait pressure to be stabilized
            Thread.Sleep(IBPressurizationTime/3 * 1000);

            // open the isolation valves
            for (int i = 0; i < 8; i++)
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOn(ASetIsolationValveRelayIDs[i, 1]);
            logMain("IBA is pressurized.");
            logMain("Tube As are filling.");

            Thread.Sleep((Int32)(time * 1000));

            // depressurize IBA
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            logMain("IBA is depressurized.");
            // close the isolation valves
            for (int i = 0; i < 8; i++)
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOff(ASetIsolationValveRelayIDs[i, 1]);
        }

        public void FillTubeAsByScale(double ml)
        {
            (new Thread(() => FillTubeAsByScale_worker(ml))).Start();
        }
        public void FillTubeAsByScale_worker(double ml)
        {
            // pressurize IBA
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            // wait pressure to be stabilized
            Thread.Sleep(IBPressurizationTime * 1000);
            for (int i = 0; i < 8; i++)
            {
                // open the isolation valve
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOn(ASetIsolationValveRelayIDs[i, 1]);

                double start = load_sensors.Read()[1];
                double end = start;
                // if IBs are empty or failing use stopwatch
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (Math.Abs(start - end) < ml && sw.ElapsedMilliseconds < safetyMaxSecondsDilutionAllowed * 1000)
                {
                    end = load_sensors.Read()[1];
                    Thread.Sleep(10);
                }
                // close the isolation valve
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOff(ASetIsolationValveRelayIDs[i, 1]);

                // if IBs are emptied skip filling rest of the tubes
                if (sw.ElapsedMilliseconds >= safetyMaxSecondsDilutionAllowed * 1000)
                    break;

                Thread.Sleep(1000);
            }
            // depressurize IBA
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]); 
        }
        public void FillTubeAsByScale(List<int> cultureIDs,
                     double ml, bool pre_pressurized=false)
        {
            (new Thread(() => FillTubeAsByScale_worker(cultureIDs, ml, pre_pressurized))).Start();
        }
        public void FillTubeAsByScale_worker(List<int> cultureIDs,
                     double ml, bool pre_pressurized=false)
        {
            if (pre_pressurized == false)
            {
                // pressurize IBA
                relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
                // wait pressure to be stabilized
                Thread.Sleep(IBPressurizationTime * 1000);
            }
            foreach (int i in cultureIDs)
            {
                // open the isolation valve
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOn(ASetIsolationValveRelayIDs[i, 1]);

                double start = load_sensors.Read()[1];
                double end = start;
                // if IBs are empty or failing use stopwatch
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (Math.Abs(start - end) < ml && sw.ElapsedMilliseconds < safetyMaxSecondsDilutionAllowed*1000)
                {
                    end = load_sensors.Read()[1];
                    Thread.Sleep(10);
                }

                // close the isolation valve
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOff(ASetIsolationValveRelayIDs[i, 1]);

                // if IBs are emptied skip filling rest of the tubes
                if (sw.ElapsedMilliseconds >= safetyMaxSecondsDilutionAllowed * 1000)
                    break;

                Thread.Sleep(1000);
            }
            // push out the liquid accumulated inside the filters
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            Thread.Sleep(2000);
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);

            if (pre_pressurized == false)
            {
                // depressurize IBA
                relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            }
        }

        public void FillTubeB(int cultureID, double time)
        {
            (new Thread(() => FillTubeB_worker(cultureID, time))).Start();
        }
        public void FillTubeB_worker(int cultureID, double time)
        {
            // pressurize IBB
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            // wait pressure to be stabilized
            Thread.Sleep(depressurizationTime * 1000);
            relays[BSetIsolationValveRelayIDs[cultureID, 0]].TurnOn(
                BSetIsolationValveRelayIDs[cultureID, 1]);

            Thread.Sleep((Int32)(time * 1000));

            // depressurize IBB
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close the isolation valve
            relays[BSetIsolationValveRelayIDs[cultureID, 0]].TurnOff(
                BSetIsolationValveRelayIDs[cultureID, 1]);
            
        }

        public void FillTubeBsByScale(double ml)
        {
            (new Thread(() => FillTubeBsByScale_worker(ml))).Start();
        }
        public void FillTubeBsByScale_worker(double ml)
        {
            // pressurize IBB
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            // wait pressure to be stabilized
            Thread.Sleep(IBPressurizationTime * 1000);
            for (int i = 0; i < 8; i++)
            {
                // open the isolation valve
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOn(BSetIsolationValveRelayIDs[i, 1]);

                double start = load_sensors.Read()[0];
                double end = start;
                // if IBs are empty or failing use stopwatch
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (Math.Abs(start - end) < ml && sw.ElapsedMilliseconds < safetyMaxSecondsDilutionAllowed*1000)
                {
                    end = load_sensors.Read()[0];
                    Thread.Sleep(10);
                }
                // close the isolation valve
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOff(BSetIsolationValveRelayIDs[i, 1]);

                // if IBs are emptied skip filling rest of the tubes
                if (sw.ElapsedMilliseconds >= safetyMaxSecondsDilutionAllowed * 1000)
                    break;

                Thread.Sleep(1000);
            }
            // depressurize IBB
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }
        public void FillTubeBsByScale(List<int> cultureIDs,
                    double ml, bool pre_pressurized=false)
        {
            (new Thread(() => FillTubeBsByScale_worker(cultureIDs, ml, pre_pressurized))).Start();
        }
        public void FillTubeBsByScale_worker(List<int> cultureIDs,
                    double ml, bool pre_pressurized=false)
        {
            if (pre_pressurized == false)
            {
                // pressurize IBB
                relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
                // wait pressure to be stabilized
                Thread.Sleep(IBPressurizationTime * 1000);
            }

            foreach(int i in cultureIDs)
            {
                // open the isolation valve
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOn(BSetIsolationValveRelayIDs[i, 1]);

                double start = load_sensors.Read()[0];
                double end = start;
                // if IBs are empty or failing use stopwatch
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (Math.Abs(start - end) < ml && sw.ElapsedMilliseconds < safetyMaxSecondsDilutionAllowed*1000)
                {
                    end = load_sensors.Read()[0];
                    Thread.Sleep(10);
                }

                // close the isolation valve
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOff(BSetIsolationValveRelayIDs[i, 1]);

                // if IBs are emptied skip filling rest of the tubes
                if (sw.ElapsedMilliseconds >= safetyMaxSecondsDilutionAllowed * 1000)
                    break;
                Thread.Sleep(1000);
            }
            // push out the liquid accumulated inside the filters
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            Thread.Sleep(3000);
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);

            if (pre_pressurized == false)
            {
                // depressurize IBB
                relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            }
        }

        public void FillTubeBs(double time)
        {
            (new Thread(() => FillTubeBs_worker(time))).Start();
        }
        public void FillTubeBs_worker(double time)
        {
            // pressurize IBB
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            // wait pressure to be stabilized
            Thread.Sleep(IBPressurizationTime/3 * 1000);
            // open the isolation valves
            for (int i = 0; i < 8; i++)
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOn(BSetIsolationValveRelayIDs[i, 1]);

            logMain("IBB is pressurized.");
            logMain("Tube Bs are filling.");

            Thread.Sleep((Int32)(time * 1000));

            // depressurize IBB
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            logMain("IBB is depressurized.");
            // close the isolation valves
            for (int i = 0; i < 8; i++)
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOff(BSetIsolationValveRelayIDs[i, 1]);
        }

        public void EvacuateTubeA(int cultureID, double time)
        {
            (new Thread(() => EvacuateTubeA_worker(cultureID, time))).Start();
        }
        public void EvacuateTubeA_worker(int cultureID, double time)
        {
            // first go through the keeping level line
            KeepLevelA_worker(3);

            // change to evacuation line configuration
            relays[ASetThreeWayRelayIDs[cultureID, 0]].TurnOn(ASetThreeWayRelayIDs[cultureID, 1]);
            //logMain("Culture " + (cultureID + 1).ToString() + "A is in evacuation mode.");
            // pressurize tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            //logMain("Tube As are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valve
            relays[ASetThreeWayRelayIDs[cultureID, 0]].TurnOff(ASetThreeWayRelayIDs[cultureID, 1]);
            //logMain("Culture " + (cultureID + 1).ToString() + "A is isolated.");
        }

        public void AltEvacuateTubeAs(double time)
        {
            (new Thread(() => AltEvacuateTubeAs_worker(time))).Start();
        }
        public void AltEvacuateTubeAs_worker(double time)
        {
            // turn on keeping level line
            relays[KeepingLevelBlockA[0]].TurnOn(KeepingLevelBlockA[1]);
            // pressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            Thread.Sleep((Int32)(time * 1000));
            // turn off keeping level line
            relays[KeepingLevelBlockA[0]].TurnOff(KeepingLevelBlockA[1]);
            // depressurize Tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
        }

        public void AltEvacuateTubeBs(double time)
        {
            (new Thread(() => AltEvacuateTubeBs_worker(time))).Start();
        }
        public void AltEvacuateTubeBs_worker(double time)
        {           
            // turn on keeping level line
            relays[KeepingLevelBlockB[0]].TurnOn(KeepingLevelBlockB[1]);
            // pressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            Thread.Sleep((Int32)(time * 1000));
            // turn off keeping level line
            relays[KeepingLevelBlockB[0]].TurnOff(KeepingLevelBlockB[1]);
            // depressurize Tube Bs
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
        }

        public void EvacuateTubeAs(double time)
        {
            (new Thread(() => EvacuateTubeAs_worker(time))).Start();
        }
        public void EvacuateTubeAs_worker(double time)
        {
            // first go through the keeping level line
            KeepLevelA_worker(3);

            // change to evacuation line configuration
            for (int i = 0; i < 8; i++)
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
            //logMain("Culture As are in evacuation mode.");
            // pressurize tube As
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
            //logMain("Tube As are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube As
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            //logMain("Tube As are depressurized.");
            // close the isolation valves
            for (int i = 0; i < 8; i++)
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
            //logMain("Culture As are isolated.");
        }

        public void EvacuateTubeB(int cultureID, double time)
        {
            (new Thread(() => EvacuateTubeB_worker(cultureID, time))).Start();
        }
        public void EvacuateTubeB_worker(int cultureID, double time)
        {
            // first go through the keeping level line
            KeepLevelB_worker(5);

            // change to evacuation configuration
            relays[BSetThreeWayRelayIDs[cultureID, 0]].TurnOn(BSetThreeWayRelayIDs[cultureID, 1]);
            //logMain("Culture " + (cultureID + 1).ToString() + "B is in evacuation mode.");
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            //logMain("Tube Bs are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));
            
            // depressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            //logMain("Tube Bs are depressurized.");
            // change to isolation configuration
            relays[BSetThreeWayRelayIDs[cultureID, 0]].TurnOff(BSetThreeWayRelayIDs[cultureID, 1]);
            //logMain("Culture " + (cultureID + 1).ToString() + "B is isolated.");
        }

        public void EvacuateTubeBs(double time)
        {
            (new Thread(() => EvacuateTubeBs_worker(time))).Start();
        }
        public void EvacuateTubeBs_worker(double time)
        {
            // first go through the keeping level line
            KeepLevelB_worker(5);
            // change to evacuation configuration
            for (int i = 0; i < 8; i++)
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            //logMain("Culture Bs are in evacuation mode.");
            // pressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
            //logMain("Tube Bs are pressurized.");

            Thread.Sleep((Int32)((time) * 1000));

            // depressurize tube Bs
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            //logMain("Tube Bs are depressurized.");
            // change to isolation configuration
            for (int i = 0; i < 8; i++)
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            //logMain("Culture Bs are isolated.");
        }

        public void DebubbleTubingsA(double time)
        {
            (new Thread(() => DebubbleTubingsA_worker(time))).Start();
        }
        public void DebubbleTubingsA_worker(double time)
        {
            int n_cycles = (int)(time / 23);

            for(int i = 0; i < n_cycles; i++)
            {
                relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);
                Thread.Sleep(5000);
                relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
                Thread.Sleep(5000);
                FillTubeAs_worker(3);
            }
        }

        public void DebubbleTubingsB(double time)
        {
            (new Thread(() => DebubbleTubingsB_worker(time))).Start();
        }
        public void DebubbleTubingsB_worker(double time)
        {
            int n_cycles = (int)(time / 23);

            for (int i = 0; i < n_cycles; i++)
            {
                relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);
                Thread.Sleep(5000);
                relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
                Thread.Sleep(5000);
                FillTubeBs_worker(3);
            }
        }

        public void FillWaterIntoIBA(double time)
        {
            Thread worker = new Thread(() => FillWaterIntoIBA_worker(time));
            worker.Start();
        }
        public void FillWaterIntoIBA_worker(double amount)
        {
            double blank_bottle_weight = load_sensors.Read()[1];
            // open water valve to IBA
            relays[WaterValveToIBA[0]].TurnOn(WaterValveToIBA[1]);
            //logMain("Water is flowing to IBA.");
            // wait until enough water has landed
            double rtw = load_sensors.Read()[1];
            while ((rtw - blank_bottle_weight) < amount || Double.IsInfinity(rtw)
                                                        || Double.IsNaN(rtw) )
            {
                Thread.Sleep(50);
                rtw = load_sensors.Read()[1];
            }
            // close water valve to IBA
            relays[WaterValveToIBA[0]].TurnOff(WaterValveToIBA[1]);
        }

        public void FillWaterIntoIBAUptoWeight(double total_amount)
        {
            (new Thread(() => FillWaterIntoIBAUptoWeight_worker(total_amount))).Start();
        }
        public void FillWaterIntoIBAUptoWeight_worker(double total_amount)
        {
            // open water valve to IBA
            relays[WaterValveToIBA[0]].TurnOn(WaterValveToIBA[1]);
            // wait for the specified time
            Double last_value = load_sensors.Read()[1];
            while (last_value < total_amount || Double.IsInfinity(last_value)
                                             || Double.IsNaN(last_value))
            {
                Thread.Sleep(100);
                last_value = load_sensors.Read()[1];
            }
            // close water valve to IBA
            relays[WaterValveToIBA[0]].TurnOff(WaterValveToIBA[1]);
        }

        public void FillWaterIntoIBB(double time)
        {
            Thread worker = new Thread(() => FillWaterIntoIBB_worker(time));
            worker.Start();
        }
        public void FillWaterIntoIBB_worker(double amount)
        {
            double blank_bottle_weight = load_sensors.Read()[0];
            // open water valve to IBA
            relays[WaterValveToIBB[0]].TurnOn(WaterValveToIBB[1]);

            // wait until enough water has landed
            double rtw = load_sensors.Read()[0];
            while ((rtw - blank_bottle_weight) < amount || Double.IsInfinity(rtw)
                                                        || Double.IsNaN(rtw))
            {
                Thread.Sleep(50);
                rtw = load_sensors.Read()[0];
            }
            // close water valve to IBA
            relays[WaterValveToIBB[0]].TurnOff(WaterValveToIBB[1]);
        }

        public void FillWaterIntoIBBUptoWeight(double total_amount)
        {
            (new Thread(() => FillWaterIntoIBBUptoWeight_worker(total_amount))).Start();
        }
        public void FillWaterIntoIBBUptoWeight_worker(double total_amount)
        {
            // open water valve to IBB
            relays[WaterValveToIBB[0]].TurnOn(WaterValveToIBB[1]);
            // wait for the specified time
            Double last_value=load_sensors.Read()[0];
            while (last_value < total_amount || Double.IsInfinity(last_value)
                                             || Double.IsNaN(last_value))
            {
                Thread.Sleep(100);
                last_value = load_sensors.Read()[0];
            }
            // close water valve to IBB
            relays[WaterValveToIBB[0]].TurnOff(WaterValveToIBB[1]);
        }

        public void FillMediaIntoIBA(double time)
        {
            Thread worker = new Thread(() => FillMediaIntoIBA_worker(time));
            worker.Start();
        }
        public void FillMediaIntoIBA_worker(double time)
        {
            // open media valve to IBA
            relays[AirValveToMediaRezervoire[0]].TurnOn(AirValveToMediaRezervoire[1]);
            relays[MediaValveToIBA[0]].TurnOn(MediaValveToIBA[1]);
            // wait for the specified time
            Thread.Sleep((Int32)(time * 1000));
            // push media in tubings back
            relays[AirValveToMediaRezervoire[0]].TurnOff(AirValveToMediaRezervoire[1]);
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            logMain("Pushing back the LB in the tubings.");
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close media valve to IBA
            relays[MediaValveToIBA[0]].TurnOff(MediaValveToIBA[1]);
            relays[AirValveToMediaRezervoire[0]].TurnOff(AirValveToMediaRezervoire[1]);
        }

        public void FillMediaIntoIBB(double time)
        {
            Thread worker = new Thread(() => FillMediaIntoIBB_worker(time));
            worker.Start();
        }
        public void FillMediaIntoIBB_worker(double time)
        {
            relays[AirValveToMediaRezervoire[0]].TurnOn(AirValveToMediaRezervoire[1]);
            // open water valve to IBB
            relays[MediaValveToIBB[0]].TurnOn(MediaValveToIBB[1]);
            // wait for the specified time
            Thread.Sleep((Int32)(time * 1000));
            // push media in tubings back
            relays[AirValveToMediaRezervoire[0]].TurnOff(AirValveToMediaRezervoire[1]);
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            //logMain("Pushing back the LB in the tubings.");
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close media valve to IBB
            relays[MediaValveToIBB[0]].TurnOff(MediaValveToIBB[1]);
        }

        public void FillEtOHIntoIBA(double time)
        {
            (new Thread(() => FillEtOHIntoIBA_worker(time))).Start();
        }
        public void FillEtOHIntoIBA_worker(double time)
        {
            // open water valve to IBA
            relays[EtOHValveToIBA[0]].TurnOn(EtOHValveToIBA[1]);
            // wait for the specified time
            Thread.Sleep((Int32)(time * 1000));
            // close water valve to IBA
            relays[EtOHValveToIBA[0]].TurnOff(EtOHValveToIBA[1]);
        }

        public void FillEtOHIntoIBAUptoWeight(double total_amount)
        {
            (new Thread(() => FillEtOHIntoIBAUptoWeight_worker(total_amount))).Start();
        }
        public void FillEtOHIntoIBAUptoWeight_worker(double total_amount)
        {
            // open water valve to IBA
            relays[EtOHValveToIBA[0]].TurnOn(EtOHValveToIBA[1]);
            // wait for the specified time
            while (load_sensors.Read()[1] < total_amount)
                Thread.Sleep(500);
            // close water valve to IBA
            relays[EtOHValveToIBA[0]].TurnOff(EtOHValveToIBA[1]);
        }

        // EtOH functionality is disabled in this implementation.
        //
        //public void FillEtOHIntoIBBUptoWeight(double total_amount)
        //{
        //    (new Thread(() => FillEtOHIntoIBBUptoWeight_worker(total_amount))).Start();
        //}
        //public void FillEtOHIntoIBBUptoWeight_worker(double total_amount)
        //{
        //    // open water valve to IBB
        //    relays[EtOHValveToIBB[0]].TurnOn(EtOHValveToIBB[1]);
        //    // wait for the specified time
        //    while (load_sensors.Read()[0] < total_amount)
        //        Thread.Sleep(500);
        //    // close water valve to IBB
        //    relays[EtOHValveToIBB[0]].TurnOff(EtOHValveToIBB[1]);
        //}

        //public void FillEtOHIntoIBB(double time)
        //{
        //    (new Thread(() => FillEtOHIntoIBB_worker(time))).Start();
        //}
        //public void FillEtOHIntoIBB_worker(double time)
        //{
        //    // open water valve to IBB
        //    relays[EtOHValveToIBB[0]].TurnOn(EtOHValveToIBB[1]);
        //    // wait for the specified time
        //    Thread.Sleep((Int32)(time * 1000));
        //    // close water valve to IBB
        //    relays[EtOHValveToIBB[0]].TurnOff(EtOHValveToIBB[1]);
        //}

        public void FillBleachIntoIBA(double time)
        {
            (new Thread(() => FillBleachIntoIBA_worker(time))).Start();
        }
        public void FillBleachIntoIBA_worker(double time)
        {
            // open water valve to IBA
            relays[BleachValveToIBA[0]].TurnOn(BleachValveToIBA[1]);
            // wait for the specified time
            Thread.Sleep((Int32)(time * 1000));
            // close water valve to IBA
            relays[BleachValveToIBA[0]].TurnOff(BleachValveToIBA[1]);
        }

        public void FillBleachIntoIBAUptoWeight(double total_amount)
        {
            (new Thread(() => FillBleachIntoIBAUptoWeight_worker(total_amount))).Start();
        }
        public void FillBleachIntoIBAUptoWeight_worker(double total_amount)
        {
            // open water valve to IBA
            relays[BleachValveToIBA[0]].TurnOn(BleachValveToIBA[1]);
            // wait for the specified time
            Double last_value = load_sensors.Read()[1];
            while (last_value < total_amount || Double.IsInfinity(last_value)
                                             || Double.IsNaN(last_value))
            {
                Thread.Sleep(100);
                last_value = load_sensors.Read()[1];
            }
            // close water valve to IBB
            relays[BleachValveToIBA[0]].TurnOff(BleachValveToIBA[1]);
        }

        public void FillBleachIntoIBB(double time)
        {
            (new Thread(() => FillBleachIntoIBB_worker(time))).Start();
        }
        public void FillBleachIntoIBB_worker(double time)
        {
            // open water valve to IBB
            relays[BleachValveToIBB[0]].TurnOn(BleachValveToIBB[1]);
            // wait for the specified time
            Thread.Sleep((Int32)(time * 1000));
            // close water valve to IBB
            relays[BleachValveToIBB[0]].TurnOff(BleachValveToIBB[1]);
        }

        public void FillBleachIntoIBBUptoWeight(double total_amount)
        {
            (new Thread(() => FillBleachIntoIBBUptoWeight_worker(total_amount))).Start();
        }
        public void FillBleachIntoIBBUptoWeight_worker(double total_amount)
        {
            // open water valve to IBA
            relays[BleachValveToIBB[0]].TurnOn(BleachValveToIBB[1]);
            // wait for the specified time
            Double last_value = load_sensors.Read()[0];
            while (last_value < total_amount || Double.IsInfinity(last_value)
                                             || Double.IsNaN(last_value))
            {
                Thread.Sleep(100);
                last_value = load_sensors.Read()[0];
            }
            // close water valve to IBB
            relays[BleachValveToIBB[0]].TurnOff(BleachValveToIBB[1]);
        }

        public void AirMixIBA(double time)
        {
            (new Thread(() => AirMixIBA_worker(time))).Start();

        }
        public void AirMixIBA_worker(double time)
        {
            int mix_step = 5; // seconds
            for (int i = 0; i < time; i += mix_step)
            {
                relays[AirMixerA[0]].TurnOn(AirMixerA[1]);
                Thread.Sleep((Int32)(mix_step * 1000));
                relays[AirMixerA[0]].TurnOff(AirMixerA[1]);
                //Thread.Sleep((Int32)(mix_step * 1000));
            }
        }
        public void AirMixIBB(double time)
        {
            (new Thread(() => AirMixIBB_worker(time))).Start();

        }
        public void AirMixIBB_worker(double time)
        {
            int mix_step = 5; // seconds
            for (int i = 0; i < time; i += mix_step)
            {
                relays[AirMixerB[0]].TurnOn(AirMixerB[1]);
                Thread.Sleep((Int32)(mix_step * 1000));
                relays[AirMixerB[0]].TurnOff(AirMixerB[1]);
                //Thread.Sleep((Int32)(mix_step * 1000));
            }
        }

        public void WashTubeA(int cultureID, double time)
        {
            (new Thread(() => WashTubeA_worker(cultureID, time))).Start();
        }
        public void WashTubeA_worker(int cultureID, double time)
        {
            FillTubeA_worker(cultureID, time);
            Thread.Sleep((Int32)(safety_waiting) * 1000);
            EvacuateTubeA_worker(cultureID, time);
        }

        public void WashTubeAs(double time, double waiting_time)
        {
            (new Thread(() => WashTubeAs_worker(time, waiting_time))).Start();
        }
        public void WashTubeAs_worker(double time, double waiting_time)
        {
            FillTubeAs_worker(time);
            Thread.Sleep((Int32)(waiting_time * 1000));
            EvacuateTubeAs_worker(time+15);
        }

        public void WashTubeB(int cultureID, double time)
        {
            (new Thread(() => WashTubeB_worker(cultureID, time))).Start();
        }
        public void WashTubeB_worker(int cultureID, double time)
        {
            FillTubeB_worker(cultureID, time);
            Thread.Sleep((Int32)(safety_waiting) * 1000);
            EvacuateTubeB_worker(cultureID, time);
        }

        public void WashTubeBs(double time, double waiting_time)
        {
            (new Thread(() => WashTubeBs_worker(time, waiting_time))).Start();
        }
        public void WashTubeBs_worker(double time, double waiting_time)
        {
            FillTubeBs_worker(time);
            Thread.Sleep((Int32)(waiting_time * 1000));
            EvacuateTubeBs_worker(time+15);
        }

        public void MixLBReservoir(double time)
        {
            Thread worker = new Thread(() => MixLBReservoir_worker(time));
            worker.Start();
        }
        public void MixLBReservoir_worker(double time)
        {
            relays[AirMixerMedia[0]].TurnOn(AirMixerMedia[1]);
            Thread.Sleep((Int32)(time * 1000));
            relays[AirMixerMedia[0]].TurnOff(AirMixerMedia[1]);
        }

        public void MakeLBA(double total_amount)
        {
            Thread worker = new Thread(() => MakeLBA_worker(total_amount));
            worker.Start();
        }
        public void MakeLBA_worker(double total_amount)
        {
            library.mediaAirationAllowed = false;
            
            // action
            double ratio = LBRatio;
            double blank_bottle_weight = load_sensors.Read()[1];
           
            // open water valve to IBA
            relays[WaterValveToIBA[0]].TurnOn(WaterValveToIBA[1]);
            //logMain("Water is flowing to IBA.");
            // wait until enough water has landed
            double rtw = load_sensors.Read()[1];
            double water_limit = total_amount * (ratio - 1) / ratio;
            while ((rtw - blank_bottle_weight) < water_limit || Double.IsInfinity(rtw)
                                                             || Double.IsNaN(rtw) )
            {
                Thread.Sleep(50);
                rtw = load_sensors.Read()[1];
            }
            // close water valve to IBA
            relays[WaterValveToIBA[0]].TurnOff(WaterValveToIBA[1]);
            // wait until water is comletelely off
            Thread.Sleep(10000);
            //logMain("Water flow to IBA is stopped.");
            // open media valve to IBA
            relays[AirValveToMediaRezervoire[0]].TurnOn(AirValveToMediaRezervoire[1]);
            relays[MediaValveToIBA[0]].TurnOn(MediaValveToIBA[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(5000);
            rtw = load_sensors.Read()[1];
            logMain("[LB Making A] Water amount: " + (rtw - blank_bottle_weight).ToString() + " gr");
            // wait until enough LB has landed
            Double last_val = load_sensors.Read()[1];
            while ((last_val - rtw) < ((rtw - blank_bottle_weight) / (ratio - 1))
                    || Double.IsInfinity(last_val) || Double.IsNaN(last_val))
            {
                Thread.Sleep(50);
                last_val = load_sensors.Read()[1];
            }
            logMain("[LB Making A] Concentrated LB amount: " + (load_sensors.Read()[1] - rtw).ToString() + " gr");
            //logMain("Concentrated LB flow to IBA is stopped.");
            // push media in tubings back
            relays[AirValveToMediaRezervoire[0]].TurnOff(AirValveToMediaRezervoire[1]);
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            //logMain("Pushing back the LB in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close media valve
            relays[MediaValveToIBA[0]].TurnOff(MediaValveToIBA[1]);
            library.mediaAirationAllowed = true;
            AirMixIBA_worker(20);
            MixLBReservoir(60);
        }

        public void MakeLBB(double total_amount)
        {
            Thread worker = new Thread(() => MakeLBB_worker(total_amount));
            worker.Start();
        }
        public void MakeLBB_worker(double total_amount)
        {
            library.mediaAirationAllowed = false;
            // action
            double ratio = LBRatio;
            double blank_bottle_weight = load_sensors.Read()[0];
           
            // open water valve to IBB
            relays[WaterValveToIBB[0]].TurnOn(WaterValveToIBB[1]);
            //logMain("Water is flowing to IBB.");
            // wait until enough water has landed
            double water_limit=total_amount*(ratio-1)/ratio;
            Double last_val = load_sensors.Read()[0];
            while ((last_val - blank_bottle_weight) < water_limit
                   || Double.IsInfinity(last_val) || Double.IsNaN(last_val))
            {
                Thread.Sleep(50);
                last_val = load_sensors.Read()[0];
            } 
            // close water valve to IBB
            relays[WaterValveToIBB[0]].TurnOff(WaterValveToIBB[1]);
            //logMain("Water flow to IBB is stopped.");
            // wait until water is completely off
            Thread.Sleep(10000);
            logMain("[LB Making B] Water amount: " + (load_sensors.Read()[0] - blank_bottle_weight).ToString() + " gr");

            // open media valve to IBB
            relays[AirValveToMediaRezervoire[0]].TurnOn(AirValveToMediaRezervoire[1]);
            relays[MediaValveToIBB[0]].TurnOn(MediaValveToIBB[1]);
            //logMain("Concentrated LB is flowing to IBB.");
            // wait until enough LB has landed
            Thread.Sleep(5000);
            double rtw = load_sensors.Read()[0];
            last_val = load_sensors.Read()[0];
            while ((last_val - rtw) < ((rtw - blank_bottle_weight) / (ratio - 1))
                    || Double.IsInfinity(last_val) || Double.IsNaN(last_val))
            {
                Thread.Sleep(50);
                last_val = load_sensors.Read()[0];
            }
            logMain("[LB Making B] Concentrated LB amount: " + (load_sensors.Read()[0] - rtw).ToString() + " gr");
            //logMain("Concentrated LB flow to IBB is stopped.");
            // push media in tubings back
            relays[AirValveToMediaRezervoire[0]].TurnOff(AirValveToMediaRezervoire[1]);
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            //logMain("Pushing back the LB in the tubings.");
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close media valve
            relays[MediaValveToIBB[0]].TurnOff(MediaValveToIBB[1]);
            library.mediaAirationAllowed = true;
            AirMixIBB_worker(20);
            MixLBReservoir(60);
        }

        public void MakeBleachA(double total_amount)
        {
            Thread worker = new Thread(() => MakeBleachA_worker(total_amount));
            worker.Start();
        }
        public void MakeBleachA_worker(double total_amount)
        {
            library.mediaAirationAllowed = false;
            // action
            double ratio = BleachRatio;
            double blank_bottle_weight = load_sensors.Read()[1];
            
            // open water valve to IBA
            relays[WaterValveToIBA[0]].TurnOn(WaterValveToIBA[1]);
            //logMain("Water is flowing to IBA.");
            // wait until enough water has landed
            Double rtw = load_sensors.Read()[1];
            while ((rtw - blank_bottle_weight) < total_amount * (ratio-1) / ratio
                    || Double.IsInfinity(rtw) || Double.IsNaN(rtw))
            {
                Thread.Sleep(50);
                rtw = load_sensors.Read()[1];
            }
            // close water valve to IBA
            relays[WaterValveToIBA[0]].TurnOff(WaterValveToIBA[1]);
            // wait until water is completely off
            Thread.Sleep(10000);
            rtw = load_sensors.Read()[1];
            double water_added = rtw - blank_bottle_weight;
            logMain("[Bleach Making A] Water amount: " + water_added.ToString() + " gr");

            // open bleach valve to IBA
            relays[BleachValveToIBA[0]].TurnOn(BleachValveToIBA[1]);
            logMain("Bleach is flowing to IBA.");
            // wait until enough bleach has landed
            Double last_val = load_sensors.Read()[1];
            while ((last_val - blank_bottle_weight) < water_added * ratio / (ratio - 1)
                    || Double.IsInfinity(last_val) || Double.IsNaN(last_val))
            {
                Thread.Sleep(200);
                last_val = load_sensors.Read()[1];
            }
            // close bleach valve
            relays[BleachValveToIBA[0]].TurnOff(BleachValveToIBA[1]);
            logMain("[Bleach Making A] Concentrated Bleach amount: " + (load_sensors.Read()[1] - rtw).ToString() + " gr");
            library.mediaAirationAllowed = true;
            AirMixIBA_worker(90);
        }

        public void MakeBleachB(double total_amount)
        {
            Thread worker = new Thread(() => MakeBleachB_worker(total_amount));
            worker.Start();
        }
        public void MakeBleachB_worker(double total_amount)
        {
            library.mediaAirationAllowed = false;
            // action
            double ratio = BleachRatio;
            double blank_bottle_weight = load_sensors.Read()[0];
            
            // open water valve to IBA
            relays[WaterValveToIBB[0]].TurnOn(WaterValveToIBB[1]);
            //logMain("Water is flowing to IBB.");
            // wait until enough water has landed
            Double rtw = load_sensors.Read()[0];
            while ((rtw - blank_bottle_weight) < total_amount *(ratio-1) / ratio
                    || Double.IsInfinity(rtw) || Double.IsNaN(rtw) )
            {
                Thread.Sleep(50);
                rtw = load_sensors.Read()[0];
            }
            // close water valve to IBA
            relays[WaterValveToIBB[0]].TurnOff(WaterValveToIBB[1]);
            // wait until water is completely off
            Thread.Sleep(10000);
            rtw = load_sensors.Read()[0];
            double water_added = rtw - blank_bottle_weight;
            logMain("[Bleach Making A] Water amount: " + water_added.ToString() + " gr");
            // open bleach valve to IBA
            relays[BleachValveToIBB[0]].TurnOn(BleachValveToIBB[1]);
            logMain("Bleach is flowing to IBB.");
            // wait until enough bleach has landed
            Double last_val = load_sensors.Read()[0];
            while ((last_val - blank_bottle_weight) < water_added * ratio / (ratio-1)
                    || Double.IsInfinity(last_val) || Double.IsNaN(last_val))
            {
                Thread.Sleep(200);
                last_val = load_sensors.Read()[0];
            }
            // close bleach valve
            relays[BleachValveToIBB[0]].TurnOff(BleachValveToIBB[1]);
            logMain("[Bleach Making B] Concentrated Bleach amount: " + (load_sensors.Read()[0] - rtw).ToString() + " gr");
            library.mediaAirationAllowed = true;
            AirMixIBB_worker(90);
        }

        public void setODA(double set_od_level, int culture_id)
        {
            (new Thread(() => setODA_worker(set_od_level, culture_id))).Start();
        }
        public void setODA_worker(double set_od_level, int culture_id)
        {
            int channel_id, avg_N=10; double initial_od = 0, next_od, delta_vf, delta_v1, delta_t, v0 = 5;
            double test_time = 1.5, mix_time = 60;

            // pressurize IBB
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            Thread.Sleep(IBPressurizationTime * 1000);

            // setting OD loop

            if (culture_id > 4) channel_id = culture_id + 3;
            else channel_id = culture_id - 1;

            /// Estimate total dilution time

            // average over last avg_N data points to calculate initial od
            initial_od = 0;
            for (int i = 0; i < avg_N; i++)
                initial_od += OD[channel_id][ODTime.Count - i - 1]; //this.lastOD[channel_id];
            initial_od /= avg_N;
            
            logMain("Test dilution started.");
            relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
            Thread.Sleep((Int32)(test_time * 1000));
            relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
            logMain("Test dilution ended.");
            Thread.Sleep((Int32)(mix_time * 1000));

            // average over last avg_N data points to calculate next od
            next_od = 0;
            for (int i = 0; i < avg_N; i++)
                next_od += OD[channel_id][ODTime.Count - i - 1]; //this.lastOD[channel_id];
            next_od /= avg_N;

            delta_v1 = ((initial_od - next_od) / next_od) * v0;
            delta_vf = ((initial_od - set_od_level) / set_od_level) * v0;
            delta_t = ((delta_vf - delta_v1) / delta_v1) * test_time;

            logMain("initial_od = " + initial_od.ToString());
            logMain("next_od = " + next_od.ToString());
            logMain("delta_v1 = " + delta_v1.ToString());
            logMain("delta_vf = " + delta_vf.ToString());
            logMain("delta_t = " + delta_t.ToString());

            double I;
            for (I = test_time; I <= delta_t; I += test_time)
            {
                logMain("Estimated dilution cycle " + ((Int32)(I / test_time)).ToString() + "...");
                relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep((Int32)(test_time * 1000));
                relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep(1000);
            }

            double estimated_dil = delta_t - I + test_time;
            if (estimated_dil > 5 || estimated_dil < 0)
            {
                this.logMain("Error at dilution estimation = " + estimated_dil.ToString() + ". Dilution skipped.");
            }
            else
            {
                logMain("Estimated dilution cycle last = "+(delta_t - I + test_time).ToString() );
                relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep((Int32)((delta_t - I + test_time) * 1000));
                relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
            }
            //while (current_od > set_od_level)
            //{
            //    // open corresponding isolation valve
            //    relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
            //    Thread.Sleep(1000);
            //    // close corresponding isolation valve
            //    relays[ASetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(ASetIsolationValveRelayIDs[culture_id - 1, 1]);
            //    Thread.Sleep(5000);
            //    current_od = this.lastOD[culture_id];
            //}

            // depressurize IBB
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
        }

        public void setODB(double set_od_level, int culture_id)
        {
            (new Thread(() => setODB_worker(set_od_level, culture_id))).Start();
        }
        public void setODB_worker(double set_od_level, int culture_id)
        {
            int channel_id; double current_od, initial_od, next_od, delta_vf, delta_v1, delta_t, v0 = 6;
            double test_time = 1.5, mix_time = 30;

            // pressurize IBB
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            Thread.Sleep(IBPressurizationTime * 1000);

            // setting OD loop

            if (culture_id > 4) channel_id = culture_id + 7;
            else channel_id = culture_id + 3;

            /// Estimate total dilution time
            initial_od = this.lastOD.values[channel_id];
            relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
            Thread.Sleep((Int32)(test_time*1000));
            relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
            Thread.Sleep((Int32)(mix_time*1000));
            next_od = this.lastOD.values[channel_id];

            delta_v1 = ((initial_od - next_od) / next_od) * v0;
            delta_vf = ((initial_od - set_od_level) / set_od_level) * v0;
            delta_t = ((delta_vf - delta_v1) / delta_v1) * test_time;

            double i;
            for (i = test_time; i <= delta_t; i += test_time)
            {
                relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep((Int32)(test_time * 1000));
                relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep(1000);
            }

            double estimated_dil = delta_t - i + test_time;
            if (estimated_dil > 5 || estimated_dil < 0)
            {
                this.logMain("Error at dilution estimation = " + estimated_dil.ToString() + ". Dilution skipped.");
            }
            else
            {
                relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
                Thread.Sleep((Int32)((delta_t - i + test_time) * 1000));
                relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
            }
            //while (current_od > set_od_level)
            //{
            //    // open corresponding isolation valve
            //    relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOn(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
            //    Thread.Sleep(1000);
            //    // close corresponding isolation valve
            //    relays[BSetIsolationValveRelayIDs[culture_id - 1, 0]].TurnOff(BSetIsolationValveRelayIDs[culture_id - 1, 1]);
            //    Thread.Sleep(5000);
            //    current_od = this.lastOD[culture_id];
            //}

            // depressurize IBB
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
        }

        public void quickWasteIBA()
        {
            (new Thread(() => quickWasteIBA_worker())).Start();
        }
        public void quickWasteIBA_worker()
        {
            //logMain("IBA is quick wasting.");
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            for (int i = 0; i < 8; i++)
            {
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOn(ASetIsolationValveRelayIDs[i, 1]);
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOn(ASetThreeWayRelayIDs[i, 1]);
            }
            Thread.Sleep(20000);
            relays[AirValveToTubeAs[0]].TurnOn(AirValveToTubeAs[1]);


            double first = 10, second = 0;
            while (Math.Abs(first - second) > 1)
            {
                first = load_sensors.Read()[1];
                Thread.Sleep(10000);
                second = load_sensors.Read()[1];
            }

            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            relays[AirValveToTubeAs[0]].TurnOff(AirValveToTubeAs[1]);
            for (int i = 0; i < 8; i++)
            {
                relays[ASetIsolationValveRelayIDs[i, 0]].TurnOff(ASetIsolationValveRelayIDs[i, 1]);
                relays[ASetThreeWayRelayIDs[i, 0]].TurnOff(ASetThreeWayRelayIDs[i, 1]);
            }
            for (int i = 0; i < 8; i++)
                EvacuateTubeA(i, 10);
            //logMain("IBA is emptied.");

        }

        public void quickWasteIBB()
        {
            (new Thread(() => quickWasteIBB_worker())).Start();
        }
        public void quickWasteIBB_worker()
        {
            //logMain("IBB is quick wasting.");
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            for (int i = 0; i < 8; i++)
            {
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOn(BSetIsolationValveRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOn(BSetThreeWayRelayIDs[i, 1]);
            }
            Thread.Sleep(20000);
            relays[AirValveToTubeBs[0]].TurnOn(AirValveToTubeBs[1]);


            double first = 10, second = 0;
            while (Math.Abs(first - second) > 1)
            {
                first = load_sensors.Read()[0];
                Thread.Sleep(10000);
                second = load_sensors.Read()[0];
            }

            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            relays[AirValveToTubeBs[0]].TurnOff(AirValveToTubeBs[1]);
            for (int i = 0; i < 8; i++)
            {
                relays[BSetIsolationValveRelayIDs[i, 0]].TurnOff(BSetIsolationValveRelayIDs[i, 1]);
                relays[BSetThreeWayRelayIDs[i, 0]].TurnOff(BSetThreeWayRelayIDs[i, 1]);
            }
            for (int i = 0; i < 8; i++)
                EvacuateTubeB(i, 10);
            //logMain("IBA is emptied.");

        }

        public void AddDrug1IntoIBA(double grams)
        {
            (new Thread(() => AddDrug1IntoIBA_worker(grams))).Start();
        }

        public void AddDrug1IntoIBA_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[1];

            // open drug 1 valve to IBA
            relays[AirValveToDrug1[0]].TurnOn(AirValveToDrug1[1]);
            relays[Drug1ValveToIBA[0]].TurnOn(Drug1ValveToIBA[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[1];
            // wait until enough drug has landed
            while ((load_sensors.Read()[1] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToDrug1[0]].TurnOff(AirValveToDrug1[1]);
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close media valve
            relays[Drug1ValveToIBA[0]].TurnOff(Drug1ValveToIBA[1]);
        }

        public void AddDrug1IntoIBB(double grams)
        {
            (new Thread(() => AddDrug1IntoIBB_worker(grams))).Start();
        }

        public void AddDrug1IntoIBB_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[0];

            // open drug 1 valve to IBA
            relays[AirValveToDrug1[0]].TurnOn(AirValveToDrug1[1]);
            relays[Drug1ValveToIBB[0]].TurnOn(Drug1ValveToIBB[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[0];
            // wait until enough drug has landed
            while ((load_sensors.Read()[0] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToDrug1[0]].TurnOff(AirValveToDrug1[1]);
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close media valve
            relays[Drug1ValveToIBB[0]].TurnOff(Drug1ValveToIBB[1]);
        }

        public void AddDrug2IntoIBA(double grams)
        {
            (new Thread(() => AddDrug2IntoIBA_worker(grams))).Start();
        }
        public void AddDrug2IntoIBA_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[1];

            // open drug 1 valve to IBA
            relays[AirValveToDrug2[0]].TurnOn(AirValveToDrug2[1]);
            relays[Drug2ValveToIBA[0]].TurnOn(Drug2ValveToIBA[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[1];
            // wait until enough drug has landed
            while ((load_sensors.Read()[1] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToDrug2[0]].TurnOff(AirValveToDrug2[1]);
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close media valve
            relays[Drug2ValveToIBA[0]].TurnOff(Drug2ValveToIBA[1]);
        }

        public void AddDrug2IntoIBB(double grams)
        {
            (new Thread(() => AddDrug2IntoIBB_worker(grams))).Start();
        }

        public void AddDrug2IntoIBB_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[0];

            // open drug 1 valve to IBA
            relays[AirValveToDrug2[0]].TurnOn(AirValveToDrug2[1]);
            relays[Drug2ValveToIBB[0]].TurnOn(Drug2ValveToIBB[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[0];
            // wait until enough drug has landed
            while ((load_sensors.Read()[0] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToDrug2[0]].TurnOff(AirValveToDrug2[1]);
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close media valve
            relays[Drug2ValveToIBB[0]].TurnOff(Drug2ValveToIBB[1]);
        }

        public void AddIPTGIntoIBA(double grams)
        {
            (new Thread(() => AddIPTGIntoIBA_worker(grams))).Start();
        }
        public void AddIPTGIntoIBA_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[1];

            // open drug 1 valve to IBA
            relays[AirValveToIPTG[0]].TurnOn(AirValveToIPTG[1]);
            relays[IPTGValveToIBA[0]].TurnOn(IPTGValveToIBA[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[1];
            // wait until enough drug has landed
            while ((load_sensors.Read()[1] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToIPTG[0]].TurnOff(AirValveToIPTG[1]);
            relays[AirValveToIBA[0]].TurnOn(AirValveToIBA[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBA[0]].TurnOff(AirValveToIBA[1]);
            // close media valve
            relays[IPTGValveToIBA[0]].TurnOff(IPTGValveToIBA[1]);
        }

        public void AddIPTGIntoIBB(double grams)
        {
            (new Thread(() => AddIPTGIntoIBB_worker(grams))).Start();
        }

        public void AddIPTGIntoIBB_worker(double total_amount)
        {
            // action
            double blank_bottle_weight = load_sensors.Read()[0];

            // open drug 1 valve to IBA
            relays[AirValveToIPTG[0]].TurnOn(AirValveToIPTG[1]);
            relays[IPTGValveToIBB[0]].TurnOn(IPTGValveToIBB[1]);
            //logMain("Concentrated LB is flowing to IBA.");
            Thread.Sleep(2000);
            double rtw = load_sensors.Read()[0];
            // wait until enough drug has landed
            while ((load_sensors.Read()[0] - rtw) < total_amount)
                Thread.Sleep(200);
            // push drug in tubings back
            relays[AirValveToIPTG[0]].TurnOff(AirValveToIPTG[1]);
            relays[AirValveToIBB[0]].TurnOn(AirValveToIBB[1]);
            //logMain("Pushing back the drug in the tubings.");
            Thread.Sleep(depressurizationTime * 1000);
            Thread.Sleep(conc_media_pushback_time * 1000);
            relays[AirValveToIBB[0]].TurnOff(AirValveToIBB[1]);
            // close media valve
            relays[IPTGValveToIBB[0]].TurnOff(IPTGValveToIBB[1]);
        }
    }

    public class CalibrationFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class LogEntry
    {
        public string message { get; set; }
        public DateTime time { get; set; }
    }

}
