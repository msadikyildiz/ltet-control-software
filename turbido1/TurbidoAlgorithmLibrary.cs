using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;


namespace turbido1
{
    public class TurbidoAlgorithmLibrary
    {

        TurbidoCore core;
        CultureSimulator sim;
        System.Timers.Timer cycler;
        System.Timers.Timer transfer_cycler;
        System.Timers.Timer media_airation_cycler;

        System.Timers.Timer ODVoltageScaleDataSaver_cycler;
        string previousODFile;
        string previousVoltageFile;
        string previousScaleFile;


        string parameter_path;
        double[] ODThreshold = {0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1};

        double[] ODLowerThreshold = {0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1,
                                0.1, 0.1, 0.1, 0.1};

        double[] GlobalThreshold = {0.5, 0.5, 0.5, 0.5,
                                    0.5, 0.5, 0.5, 0.5,
                                    0.5, 0.5, 0.5, 0.5,
                                    0.5, 0.5, 0.5, 0.5};

        int[] DilutionStreak = { 0, 0, 0, 0,
                                 0, 0, 0, 0,
                                 0, 0, 0, 0,
                                 0, 0, 0, 0 };
        List<int> activeCultures = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

        public char culture_set = 'A';
        private bool simulationMode = false;
        public Double assumedGrowthRate = 20; // doubling time in mins
        double transferTime = 6;
        double evacuationTime = 45;
        double fBleachTime = 23;
        double fWaterTime = 25;
        double bleachTime = 21;
        double wasteTime = 12;
        double waterTime = 22;
        double dilutionTime = 8;
        double LBLowerThresold = 100;// ml
        double LBRefresh = 200;      // ml
        double LBInitialFill = 400; // ml
        double bleachVolume = 200;   // ml
        double waterVolume = 400;    // ml
        double fWaterVolume = 1000;   // ml
        double fBleachVolume = 600;  // ml
        int waterMixTime = 60;   // secs
        int bleachMixTime = 10; // secs
        int beforeTransferMixTime = 30; // secs
        int bleachCycleWaitTime = 10; // mins
        int waterCycleWaitTime = 5; // mins
        int fullGrowthCurveTime = 80; // mins
        public double dilutionCyclePeriod = 5; // minutes

        private double mediaAirationCyclePeriod = 60; // seconds
        public double MediaAirationCyclePeriod
        {
            get { return mediaAirationCyclePeriod; }
            set
            {
                mediaAirationCyclePeriod = value;
                if (media_airation_cycler != null)
                {
                    media_airation_cycler.Stop();
                    media_airation_cycler.Interval = (value * 1000);
                    media_airation_cycler.Start();
                    core.logMain("Media airation cycle period changed to " + value.ToString() + " seconds.");
                }
            }
        }

        private double transferCyclePeriod = 8; // hours
        public double TransferCyclePeriod
        {
            get { return transferCyclePeriod; }
            set
            {
                transferCyclePeriod = value;
                if (transfer_cycler != null)
                {
                    transfer_cycler.Stop();
                    transfer_cycler.Interval = (value * 1000 * 3600);
                    transfer_cycler.Start();
                    core.logMain("Transfer cycle period changed to " + value.ToString() + " hours.");
                }
            }
        }


        // DilutionWithFeedback params
        public List<List<float>> dilTimes = new List<List<float>>();
        int[] dilTimesI = new int[16];
        System.Timers.Timer recordOverThrTiming;
        int recordOverThrPeriod = 10; // seconds
        public double[] tOverThr1, tOverThr2;
        public bool[] tOverThrFlag;

        public bool turbidostatCycleActive;
        public bool mediaAirationActive = false;
        public bool mediaAirationAllowed = true;
        public string[] PhaseLabels = { "I", "G", "D1", "D0", "K1", "K0", "T", "C", "C0", "B", "M", "W","DR1","DR2","DR3","F"};
        public enum Phase { I, G, D1, D0, K1, K0, T, C, C0, B, M, W, DR1, DR2, DR3, F};
        public Phase[] currentPhase = new Phase[16];
        public Phase[] currentScalePhase = new Phase[2];
        public List<List<List<int>>> scalePhaseTubingIDs = new List<List<List<int>>>(); // 2xNx8
        int[] scalePhaseTubingIDsI = new int [2];

        // Maintenance
        DoubleArrayRef lastODp;
        DoubleArrayRef lastMeanODp;
        DoubleRef lastTimep = new DoubleRef();
        public bool mainThreadOnWait = false;

        public bool SimulationMode
        {
            get 
            { return simulationMode;  }
            set 
            {
                simulationMode = value;
                core.simulationMode = value;
            }
        }

        public TurbidoAlgorithmLibrary(ref TurbidoCore core_)
        {
            // Create simulator
            sim = new CultureSimulator(this);
            // Grant TurbidoCore and TurbidoAlgorithmLibrary access to the simulator
            sim.core = core_;
            core = core_;
            core.sim = sim;
            // Sync simulationMode
            core.simulationMode = SimulationMode;
            // Switch mode
            sim.StartExponentialGrowthSimulation();

            // Initialize data arrays
            turbidostatCycleActive = false;
            parameter_path = @"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\";
            for (int i = 0; i < 16; i++)
            {
                dilTimes.Add(new List<float>());
                dilTimes[i].Add((float)dilutionTime);
            }
            // Initialize phase logging arrays
            scalePhaseTubingIDs.Add(new List<List<int>>());
            scalePhaseTubingIDs.Add(new List<List<int>>());

            // Initialize tOvers
            tOverThr1 = new double[16];
            tOverThr2 = new double[16];
            tOverThrFlag = new bool[16];

            for (int i = 0; i < 16; i++)
            {
                tOverThr1[i] = -1;
                tOverThr2[i] = -1;
                tOverThrFlag[i] = false;
            }

           //updateThresholds();
        }

        // algorithms
        public void StartSimpleTurbidostat()
        {
            core.logMain("Simple turbidostat algorithm is started.");
            cycler = new System.Timers.Timer(dilutionCyclePeriod * 60 * 1000);
            cycler.Elapsed += TurbidostatCycle;
            transfer_cycler = new System.Timers.Timer(transferCyclePeriod * 3600 * 1000);
            transfer_cycler.Elapsed += TransferCycle;
        }

        public void StopTurbidostat()
        {
            core.logMain("Turbidostat algorithm is stopped.");
            cycler.Stop();
            transfer_cycler.Stop();
        }

        public void StartAutomatedTurbidostat()
        {
            //transferTime = 8;
            // always start from culture B's
            // B tubes are assumed to be initialized with cultures manually 
            // transfer from B to A
            //core.TransferBtoA_worker(transferTime);
            //MainWashingProtocolB();

            if (simulationMode)
            {
                lastODp = sim.lastOD;
                lastMeanODp = sim.lastOD;
                lastTimep = sim.lastTime;
            }
            else 
            {
                lastODp = core.lastOD;
                lastMeanODp = core.lastMeanOD;
                lastTimep = core.lastTime;
            }

            cycler = new System.Timers.Timer(dilutionCyclePeriod * 60 * 1000);
            cycler.Elapsed += AutomatedTurbidostatCycle;
            transfer_cycler = new System.Timers.Timer(transferCyclePeriod * 3600 * 1000);
            transfer_cycler.Elapsed += TransferCycle;
            media_airation_cycler = new System.Timers.Timer(MediaAirationCyclePeriod * 1000);
            media_airation_cycler.Elapsed += MediaAirationCycle;
            recordOverThrTiming = new System.Timers.Timer(recordOverThrPeriod * 1000);
            recordOverThrTiming.Elapsed += recordOverThrTimingCycle;

            for (int i = 0; i < 4; i++)
            {
                if (culture_set == 'A')
                {
                    currentPhase[i] = currentPhase[8 + i] = Phase.G;
                    currentPhase[4 + i] = currentPhase[12 + i] = Phase.I;
                }
                else
                {
                    currentPhase[i] = currentPhase[8 + i] = Phase.I;
                    currentPhase[4 + i] = currentPhase[12 + i] = Phase.G;
                }
            }
            core.DataCollector.Start();
            core.ReinitializeODReader.Start();
            cycler.Start();
            recordOverThrTiming.Start();
            transfer_cycler.Start();
            media_airation_cycler.Start();
            core.logMain("Automated turbidostat algorithm is started.");
        }

        private void MediaAirationCycle(object sender, ElapsedEventArgs e)
        {
            if (!mediaAirationAllowed) return;
            if (turbidostatCycleActive) return;

            mediaAirationActive = true;
            if (culture_set == 'A')
            {
                if (currentPhase[4] != Phase.W)
                    core.AirMixIBB(5);
                core.AirMixIBA_worker(5);
            }
            else if (culture_set == 'B')
            {
                if (currentPhase[0] != Phase.W)
                    core.AirMixIBA(5);
                core.AirMixIBB_worker(5);
            }
            mediaAirationActive = false;
        }

        private void recordOverThrTimingCycle(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < 16; i++)
                if (!tOverThrFlag[i] && lastMeanODp.values[i]>ODThreshold[i])
                {
                    tOverThrFlag[i] = true;
                    tOverThr2[i] = tOverThr1[i];
                    tOverThr1[i] = lastTimep.val;
                }
        }

        public void TurbidostatCycle(object state, ElapsedEventArgs e)
        {
            turbidostatCycleActive = true;
            core.logMain("Turbidostat cycle is active.");
            List<int> over_thrA = new List<int>();
            List<int> over_thrB = new List<int>();
            Double[] od = lastMeanODp.values;

            string over_thrAs = "A", over_thrBs = "B";

            for (int i = 0; i < 4; i++)
            {
                if (od[i] > ODThreshold[i] && od[i] < GlobalThreshold[i])
                {
                    over_thrA.Add(i);
                    over_thrAs += (i).ToString() + " ";
                    currentPhase[i] = Phase.D1;
                }
                if (od[4 + i] > ODThreshold[4+i] && od[4+i] < GlobalThreshold[4+i])
                {
                    over_thrB.Add(i);
                    over_thrBs += (i).ToString() + " ";
                    currentPhase[4+i] = Phase.D1;
                }
                if (od[8 + i] > ODThreshold[8+i] && od[8+i] < GlobalThreshold[8+i])
                {
                    over_thrA.Add(i+4);
                    over_thrAs += (i+4).ToString() + " ";
                    currentPhase[8+i] = Phase.D1;

                }
                if (od[12 + i] > ODThreshold[12+i] && od[12+i] < GlobalThreshold[12+i])
                {
                    over_thrB.Add(i+4);
                    over_thrBs += (i+4).ToString() + " ";
                    currentPhase[12+i] = Phase.D1;

                }

            }

            core.logMain("Cultures " + over_thrAs + "are getting diluted.");
            core.DiluteTubeA(over_thrA, 8);
            core.logMain("Cultures " + over_thrBs + "are getting diluted.");
            core.DiluteTubeB_worker(over_thrB, 8);

            // check media supplies
            Double[] media_supplies = core.load_sensors.Read();

            if (culture_set == 'A')
            {
                if (media_supplies[1] < LBLowerThresold)
                    core.MakeLBA(LBRefresh);
            }
            else if (culture_set == 'B')
            {
                if (media_supplies[0] < LBLowerThresold)
                    core.MakeLBB(LBRefresh);
            }

            core.logMain("Turbidostat cycle is done.");
            turbidostatCycleActive = false;
        }

        public void AutomatedTurbidostatCycle(object state, ElapsedEventArgs e)
        {
            while (turbidostatCycleActive)
                Thread.Sleep(1000);
            while (mediaAirationActive)
                Thread.Sleep(1000);
            turbidostatCycleActive = true;
            core.logMain("Turbidostat cycle is active.");
            List<int> over_thrA = new List<int>();
            List<int> over_thrB = new List<int>();
            Double[] od = lastMeanODp.values;

            //if (core.ODReader.tmrContinuousRead.Enabled)
            //{
            //    od = core.ODReader.returnLastCalibratedODValue();
            //    //od = core.ODReader.returnLastRead();
            //}
            //else return;

            string over_thrAs = "A", over_thrBs = "B";

            // check over threshold cultures
            for (int i = 0; i < 4; i++)
            {
                // ODThreshold[i] = ODThreshold[4 + i] = ODThreshold[8 + i] = ODThreshold[12 + i] = 0.1;
                if (culture_set == 'A')
                {
                    if (od[i] > ODThreshold[i]  && DilutionStreak[i] < 1)
                    {
                        DilutionStreak[i] = 2;
                        over_thrA.Add(i);
                        over_thrAs += (i + 1).ToString() + " ";
                    }
                    else
                    {
                        DilutionStreak[i] -= 1;
                        if (DilutionStreak[i] < 0) DilutionStreak[i] = 0;
                    }

                    if (od[8 + i] > ODThreshold[8 + i]  && DilutionStreak[8 + i] < 1)
                    {
                        DilutionStreak[8 + i] = 2;
                        over_thrA.Add(i + 4);
                        over_thrAs += (i + 5).ToString() + " ";
                    }
                    else
                    {
                        DilutionStreak[8 + i] -= 1;
                        if (DilutionStreak[8+i] < 0) DilutionStreak[8+i] = 0;
                    }
                }
                if (culture_set == 'B')
                {
                    if (od[4 + i] > ODThreshold[4 + i]  && DilutionStreak[4 + i] < 1)
                    {
                        DilutionStreak[4 + i] = 2;
                        over_thrB.Add(i);
                        over_thrBs += (i + 1).ToString() + " ";
                    }
                    else
                    {
                        DilutionStreak[4 + i] -= 1;
                        if (DilutionStreak[4+i] < 0) DilutionStreak[4+i] = 0;
                    }

                    if (od[12 + i] > ODThreshold[12 + i]  && DilutionStreak[12+i]<1)
                    {
                        DilutionStreak[12 + i] = 2;
                        over_thrB.Add(i + 4);
                        over_thrBs += (i + 5).ToString() + " ";
                    }
                    else
                    {
                        DilutionStreak[12 + i] -= 1;
                        if (DilutionStreak[12+i] < 0) DilutionStreak[12+i] = 0;
                    }
                }

            }

            // dilute over threshold cultures
            if (culture_set == 'A')
            {
                core.logMain("Cultures " + over_thrAs + "are getting diluted.");
                // Dilute
                mainThreadOnWait = true;
                if (simulationMode)
                {
                    sim.DiluteTubeAWithFeedback(over_thrA);
                }
                else
                {
                    //core.DiluteTubeAWithFeedback(over_thrA);
                    //core.SmartDiluteTubeA_worker(over_thrA, ODLowerThreshold[0]);
                    DiluteTubeAByScale_worker(over_thrA);
                }
                while (mainThreadOnWait) Thread.Sleep(200);

                // update phase
                foreach (int i in over_thrA)
                    if (i < 4)
                        currentPhase[i] = Phase.G;
                    else
                        currentPhase[4 + i] = Phase.G;
            }
            if (culture_set == 'B')
            {
                core.logMain("Cultures " + over_thrBs + "are getting diluted.");
                // Dilute
                mainThreadOnWait = true;
                if (simulationMode)
                {
                    sim.DiluteTubeBWithFeedback(over_thrB);
                }
                else
                {
                    //DiluteTubeBWithFeedback(over_thrB);
                    //core.DiluteTubeB_worker(over_thrB, dilutionTime);
                    //core.SmartDiluteTubeB_worker(over_thrB, ODLowerThreshold[0]);
                    DiluteTubeBByScale_worker(over_thrB);
                }
                while (mainThreadOnWait) Thread.Sleep(200);

                //update phase
                foreach (int i in over_thrB)
                    if (i < 4)
                        currentPhase[4 + i] = Phase.G;
                    else
                        currentPhase[8 + i] = Phase.G;
            }

            // check media supplies
            Double[] media_supplies = core.load_sensors.Read();

            if (culture_set == 'A')
            {
                if (media_supplies[1] < LBLowerThresold)
                    core.MakeLBA_worker(LBRefresh);
            }
            else if (culture_set == 'B')
            {
                if (media_supplies[0] < LBLowerThresold)
                    core.MakeLBB_worker(LBRefresh);
            }

            core.logMain("Turbidostat cycle is done.");
            turbidostatCycleActive = false;
        }

        public void DiluteTubeBWithFeedback(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeBWithFeedback_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeBWithFeedback_worker(List<int> cultureIDs)
        {
            if (cultureIDs.Count == 0)
            {
                // release main thread
                mainThreadOnWait = false;
                return;
            }

            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    currentPhase[4 + cID] = Phase.K1;
                else
                    currentPhase[8 + cID] = Phase.K1;
            // pressurize IB
            core.relays[core.AirValveToIBB[0]].TurnOn(core.AirValveToIBB[1]);

            // keep level
            core.AltKeepLevelBSelect_worker(cultureIDs, core.keepingLevelTime);

            currentScalePhase[1] = Phase.F;
            scalePhaseTubingIDs[0].Add(new List<int>());
            // start dilution
            foreach (int cID in cultureIDs)
            {
                core.relays[core.BSetIsolationValveRelayIDs[cID, 0]].TurnOn(core.BSetIsolationValveRelayIDs[cID, 1]);
                scalePhaseTubingIDs[0][scalePhaseTubingIDs[0].Count - 1].Add(cID);
            }

            try
            {
                core.logMain("Dilutions are started.");
                // update phase D
                foreach (int cID in cultureIDs)
                    if (cID < 4)
                        currentPhase[4 + cID] = Phase.D1;
                    else
                        currentPhase[8 + cID] = Phase.D1;
                // calculate waiting order
                List<float> waitTimes = new List<float>();
                waitTimes.Add(0);
                foreach (int cID in cultureIDs)
                    if (cID < 4)
                        waitTimes.Add(dilTimes[4 + cID][dilTimes[4 + cID].Count - 1]);
                    else
                        waitTimes.Add(dilTimes[8 + cID][dilTimes[8 + cID].Count - 1]);

                cultureIDs.Insert(0, -1);
                var waitTimesSorted = waitTimes.Select((x, i) => new KeyValuePair<float, int>(x, cultureIDs[i])).OrderBy(x => x.Key).ToList();
                cultureIDs.Remove(-1);
                List<float> B = waitTimesSorted.Select(x => x.Key).ToList();
                List<int> idx = waitTimesSorted.Select(x => x.Value).ToList();
                int wi = 1; float wt;
                // Stop dilution
                for (int i = 1; i < waitTimes.Count; i++)
                {
                    int cID = idx[i];
                    wt = (B[wi] - B[wi - 1]); wi++;
                    core.logMain("Culture B" + (cID + 1).ToString() + " waitin for " + wt.ToString() + " s to stop.");
                    Thread.Sleep((Int32)(wt * 1000));
                    core.relays[core.BSetIsolationValveRelayIDs[cID, 0]].TurnOff(core.BSetIsolationValveRelayIDs[cID, 1]);
                }
            }
            catch 
            {
                core.logMain("Error during dilution feedback: Dilution immediately stopped.");
                // Stop dilution in case of any error for safety
                foreach (int cID in cultureIDs)
                {
                    core.relays[core.BSetIsolationValveRelayIDs[cID, 0]].TurnOff(core.BSetIsolationValveRelayIDs[cID, 1]);
                }
            }

            currentScalePhase[1] = Phase.I;
            // update overThr flag
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    tOverThrFlag[4 + cID] = false;
                else
                    tOverThrFlag[8 + cID] = false;
            // depressurize IB
            core.relays[core.AirValveToIBB[0]].TurnOff(core.AirValveToIBB[1]);

            // release main thread
            mainThreadOnWait = false;
        }

        public void DiluteTubeAWithFeedback(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeAWithFeedback_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeAWithFeedback_worker(List<int> cultureIDs)
        {

            if (cultureIDs.Count == 0)
            {
                // release main thread
                mainThreadOnWait = false;
                return;
            }

            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    currentPhase[cID] = Phase.K1;
                else
                    currentPhase[4 + cID] = Phase.K1;
            // pressurize IB
            core.relays[core.AirValveToIBA[0]].TurnOn(core.AirValveToIBA[1]);

            // keep level
            core.AltKeepLevelASelect_worker(cultureIDs, core.keepingLevelTime);

            currentScalePhase[1] = Phase.F;
            scalePhaseTubingIDs[1].Add(new List<int>());

            // start dilution
            foreach (int cID in cultureIDs)
            {
                scalePhaseTubingIDs[1][scalePhaseTubingIDs[1].Count - 1].Add(cID);
                core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOn(core.ASetIsolationValveRelayIDs[cID, 1]);
            }
            
            try
            {
                core.logMain("Dilutions are started.");
                // update phase D
                foreach (int cID in cultureIDs)
                    if (cID < 4)
                        currentPhase[cID] = Phase.D1;
                    else
                        currentPhase[4 + cID] = Phase.D1;

                // calculate waiting order
                List<float> waitTimes = new List<float>();
                waitTimes.Add(0);
                foreach (int cID in cultureIDs)
                    if (cID < 4)
                        waitTimes.Add(dilTimes[cID][dilTimes[cID].Count - 1]);
                    else
                        waitTimes.Add(dilTimes[4 + cID][dilTimes[4 + cID].Count - 1]);
                cultureIDs.Insert(0, -1);
                var waitTimesSorted = waitTimes.Select((x, i) => new KeyValuePair<float, int>(x, cultureIDs[i])).OrderBy(x => x.Key).ToList();
                cultureIDs.Remove(-1);

                List<float> B = waitTimesSorted.Select(x => x.Key).ToList();
                List<int> idx = waitTimesSorted.Select(x => x.Value).ToList();
                int wi = 1; float wt;
                // Stop dilution
                for (int i = 1; i < waitTimes.Count; i++)
                {
                    int cID = idx[i];
                    wt = (B[wi] - B[wi - 1]); wi++;
                    core.logMain("Culture A" + (cID + 1).ToString() + " waitin for " + wt.ToString() + " s to stop.");
                    Thread.Sleep((Int32)(wt * 1000));
                    core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOff(core.ASetIsolationValveRelayIDs[cID, 1]);
                }
            }
            catch 
            {
                core.logMain("Error during dilution feedback: Dilution immediately stopped.");
                // Stop dilution in case of any error for safety
                foreach (int cID in cultureIDs)
                {
                    core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOff(core.ASetIsolationValveRelayIDs[cID, 1]);
                }
            }
            currentScalePhase[0] = Phase.I;
            // update overThr flag
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    tOverThrFlag[cID] = false;
                else
                    tOverThrFlag[4+cID] = false;
            // depressurize IB
            core.relays[core.AirValveToIBA[0]].TurnOff(core.AirValveToIBA[1]);
            // release main thread
            mainThreadOnWait = false;
        }

        public void DiluteTubeAByScale(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeAByScale_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeAByScale_worker(List<int> cultureIDs)
        {
            if (cultureIDs.Count == 0)
            {
                // release main thread
                mainThreadOnWait = false;
                return;
            }

            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    currentPhase[cID] = Phase.K1;
                else
                    currentPhase[4 + cID] = Phase.K1;
            // pressurize IB
            core.relays[core.AirValveToIBA[0]].TurnOn(core.AirValveToIBA[1]);
            // keep level
            core.AltKeepLevelASelect_worker(cultureIDs, core.keepingLevelTime);
            // update phase dilution
            string cultures="";
            foreach (int cID in cultureIDs)
            {
                if (cID < 4)
                    currentPhase[cID] = Phase.D1;
                else
                    currentPhase[4 + cID] = Phase.D1;
                cultures += (cID + 1).ToString() + " ";
            }
            core.logMain("Cultures " + cultures + " are getting diluted.");
            core.FillTubeAsByScale_worker(cultureIDs, dilutionTime, pre_pressurized: true);
            // depressurize IB
            core.relays[core.AirValveToIBA[0]].TurnOff(core.AirValveToIBA[1]);

            mainThreadOnWait = false;
        }

        public void DiluteTubeBByScale(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeBByScale_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeBByScale_worker(List<int> cultureIDs)
        {
            if (cultureIDs.Count == 0)
            {
                // release main thread
                mainThreadOnWait = false;
                return;
            }

            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    currentPhase[4 + cID] = Phase.K1;
                else
                    currentPhase[8 + cID] = Phase.K1;
            // pressurize IB
            core.relays[core.AirValveToIBB[0]].TurnOn(core.AirValveToIBB[1]);
            // keep level
            core.AltKeepLevelBSelect_worker(cultureIDs, core.keepingLevelTime);
            // update phase dilution
            string cultures = "";
            foreach (int cID in cultureIDs)
            {
                if (cID < 4)
                    currentPhase[4 + cID] = Phase.D1;
                else
                    currentPhase[8 + cID] = Phase.D1;
                cultures += (cID + 1).ToString() + " ";
            }
            core.logMain("Cultures " + cultures + " are getting diluted.");
            core.FillTubeBsByScale_worker(cultureIDs, dilutionTime, pre_pressurized: true);
            // depressurize IB
            core.relays[core.AirValveToIBB[0]].TurnOff(core.AirValveToIBB[1]);

            mainThreadOnWait = false;
        }

        public void TransferCycle(object state, ElapsedEventArgs e)
        {
            // modify the active tubes for transfer
            List<int> select = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8};
            for (int i = 0; i < select.Count; i++) select[i]--;

            while (mediaAirationActive) Thread.Sleep(1000);
            media_airation_cycler.Stop();
            while (!turbidostatCycleActive) Thread.Sleep(100);
            while (turbidostatCycleActive) Thread.Sleep(1000);
            cycler.Stop();
            core.logMain("Transfer cycle is active.");
           
            for (int i = 0; i < 16; i++)
                currentPhase[i] = Phase.T;

            if (culture_set == 'A')
            {
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.FillTubeAsByScale_worker(dilutionTime);
                Thread.Sleep(beforeTransferMixTime*1000);
                core.TransferAtoBSelect_worker(select, transferTime);
                //core.FillTubeBs_worker(5);
                //core.AltKeepLevelB_worker(core.keepingLevelTime);
                culture_set = 'B';
            }
            else
            {
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.FillTubeBsByScale_worker(dilutionTime);
                Thread.Sleep(beforeTransferMixTime * 1000);
                core.TransferBtoASelect_worker(select, transferTime);
                //core.FillTubeAs_worker(5);
                //core.AltKeepLevelA_worker(core.keepingLevelTime);
                culture_set = 'A';
            }

            for (int i = 0; i < 16; i++)
                currentPhase[i] = Phase.G;
            cycler.Start();

            core.logMain("Transfer cycle is done.");

            
            // wash and get ready original tubesdilutionTime
            if (culture_set == 'A')
            {
                media_airation_cycler.Start();

                /*
                // Take a full growth curve before wash
                // check media supplies
                Double[] media_supplies = core.load_sensors.Read();
                if (media_supplies[0] < 300)
                    core.MakeLBB_worker(350 - media_supplies[0]);
                //single wash
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.FillTubeBsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.FillTubeBsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.FillTubeBsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.FillTubeBsByScale_worker(dilutionTime);

                ////double wash
                // register for idle phase
                //for (int i = 0; i < 4; i++)
                //    currentPhase[4 + i] = currentPhase[12 + i] = Phase.I;
                //Thread.Sleep(airationBeforeInitiationTime * 60 * 1000);
                //core.FillTubeBsByScale(dilutionTime);
                // Fill evacuation line
                //core.AltEvacuateTubeBs(2);
                // Prime LB (again)
                //core.FillTubeBsByScale_worker(dilutionTime*1.5);
                // reading blank phase
                // for (int i = 0; i < 4; i++)
                //     currentPhase[4 + i] = currentPhase[12 + i] = Phase.C0;
                // core.AltKeepLevelB_worker(core.keepingLevelTime);
                // // innoculate cells from other side
                // while (!turbidostatCycleActive)
                //     Thread.Sleep(1000);
                // while (turbidostatCycleActive)
                //     Thread.Sleep(1000);
                // core.AltKeepLevelA(core.keepingLevelTime);
                // // pressurize IBA
                // core.relays[core.AirValveToIBA[0]].TurnOn(core.AirValveToIBA[1]);
                // // wait pressure to be stabilized
                // Thread.Sleep(core.IBPressurizationTime * 1000);
                // foreach(int cID in select)
                // {
                //     List<int> select_one=new List<int>(); select_one.Add(cID);
                //     core.FillTubeAsByScale_worker(select_one, dilutionTime, pre_pressurized:true);

                // }
                // Thread.Sleep(beforeTransferMixTime/2 * 1000);
                // foreach (int cID in select)
                // {
                //     List<int> select_one = new List<int>(); select_one.Add(cID);
                //     core.TransferAtoBSelect_worker(select_one, transferTime);
                // }
                //// depressurize IB
                // core.relays[core.AirValveToIBA[0]].TurnOff(core.AirValveToIBA[1]);
                // Thread.Sleep(2000);

                // growth curve phase started
                for (int i = 0; i < 4; i++)
                    currentPhase[4 + i] = currentPhase[12 + i] = Phase.C;
                core.logMain("Growth curve period started at culture Bs.");
                Thread.Sleep(fullGrowthCurveTime * 60 * 1000);
                */

                // register for second cleaning phase
                for (int i = 0; i < 4; i++)
                    currentPhase[4 + i] = currentPhase[12 + i] = Phase.W;
                // evacuate any leftovers
                core.AltEvacuateTubeBs_worker(evacuationTime);
                // waste remaining media
                WasteIBB_worker();
                // start cleaning protocol
                currentScalePhase[0] = Phase.B;
                BleachWashingProtocolB_worker("normal");
                // make media ready for next cycle
                currentScalePhase[0] = Phase.M;
                core.MakeLBB_worker(LBInitialFill);
                currentScalePhase[0] = Phase.F;
                // Prime LB
                core.FillTubeBsByScale_worker(dilutionTime*1.5);
                currentScalePhase[0] = Phase.I;

                // let them be ready
                for (int i = 0; i < 4; i++)
                    currentPhase[4 + i] = currentPhase[12 + i] = Phase.I;
            }
            if (culture_set == 'B')
            {
                media_airation_cycler.Start();

                /*
                // Take a full growth curve before wash
                // check media supplies
                Double[] media_supplies = core.load_sensors.Read();
              
                if (media_supplies[1] < 300)
                    core.MakeLBA_worker(350-media_supplies[1]);
                
                //single wash
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.FillTubeAsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.FillTubeAsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.FillTubeAsByScale_worker(dilutionTime);
                Thread.Sleep(Convert.ToInt32(dilutionTime) * 1000);
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.FillTubeAsByScale_worker(dilutionTime);

                // growth curve phase started
                for (int i = 0; i < 4; i++)
                    currentPhase[i] = currentPhase[8 + i] = Phase.C;
                core.logMain("Growth curve period started at culture As.");
                Thread.Sleep(fullGrowthCurveTime * 60 * 1000);
                */
                // register for second cleaning phase
                for (int i = 0; i < 4; i++)
                    currentPhase[i] = currentPhase[8 + i] = Phase.W;
                // evacuate any leftovers
                core.AltEvacuateTubeAs_worker(evacuationTime);
                // waste remaining media
                WasteIBA_worker();
                // start cleaning protocol
                currentScalePhase[1] = Phase.B;
                BleachWashingProtocolA_worker("normal");
                // make media ready for next cycle
                currentScalePhase[1] = Phase.M;
                core.MakeLBA_worker(LBInitialFill);
                currentScalePhase[1] = Phase.F;
                core.FillTubeAsByScale_worker(dilutionTime*1.5);
                currentScalePhase[1] = Phase.I;

                // let them be ready
                for (int i = 0; i < 4; i++)
                    currentPhase[i] = currentPhase[8 + i] = Phase.I;
            }

        }

        // data management
        public void StartODVoltageScaleDataSaver()
        {
            ODVoltageScaleDataSaver_cycler = new System.Timers.Timer(10*60*1000);
            ODVoltageScaleDataSaver_cycler.Elapsed += ODVoltageScaleDataSaver;
            ODVoltageScaleDataSaver_cycler.Start();

        }
        public void ODVoltageScaleDataSaver(object state, ElapsedEventArgs e)
        {
            DateTime saveNow = DateTime.Now;
            string path = @"C:\Users\Turbidostat\Desktop\turbido_data\";
            string filenameVoltage = "Voltage-" + saveNow.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            string csv_line;

            // reinitialize dilTimeIndexes
            for (int i = 0; i < 16; i++)
                dilTimesI[i] = 1;
            for (int j = 0; j < core.Scale.Count; j++)
                scalePhaseTubingIDsI[j] = 0;

            //using (StreamWriter file = new StreamWriter(path + filenameVoltage))
            //{
            //    for (int i = 0; i < core.ODTime.Count; i++)
            //    {
            //        csv_line = core.ODTime[i].ToString() + ",";
            //        for (int j = 0; j < core.OD.Count; j++)
            //        {
            //            try
            //            { csv_line += Math.Exp((double)(core.OD[j][i] - core.ODReader.p0[j]) / core.ODReader.p1[j]).ToString() + ","; }
            //            catch
            //            { csv_line += "Error,"; }
            //        }
            //        for (int j = 0; j < core.Phase.Count; j++)
            //            csv_line += PhaseLabels[core.Phase[j][i]] + ",";

            //        file.WriteLine(csv_line);
            //    }
            //}

            string filenameOD = "OD-" + saveNow.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            bool[] d1Ind = new bool[core.OD.Count];
            for (int j = 0; j < core.OD.Count - 1; j++, d1Ind[j] = false) ;
            using (StreamWriter file = new StreamWriter(path + filenameOD))
            {
                for (int i = 0; i < core.ODTime.Count; i++)
                {
                    csv_line = core.ODTime[i].ToString()+",";
                    for (int j = 0; j < core.OD.Count; j++)
                    {
                        try
                        { csv_line += core.OD[j][i].ToString() + ","; }
                        catch
                        { csv_line += "Error,"; }

                    }

                    for (int j = 0; j < core.Phase.Count; j++)
                    {
                        try
                        {
                            if (core.Phase[j][i] == (UInt16)Phase.D1)
                            {
                                csv_line += PhaseLabels[core.Phase[j][i]] + "_" + String.Format("{0:0.###}", dilTimes[j][dilTimesI[j]]) + ",";
                                d1Ind[j] = true;
                            }
                            else
                            {
                                csv_line += PhaseLabels[core.Phase[j][i]] + ",";
                            }
                        }
                        catch 
                        {
                            csv_line += "Error,";
                        }
                    }

                    for (int j = 0; j < core.Phase.Count; j++)
                        if (core.Phase[j][i] != (UInt16)Phase.D1)
                        {
                            if (d1Ind[j])
                                dilTimesI[j]++;
                            d1Ind[j] = false;
                        }
                    file.WriteLine(csv_line);
                }
            }

            string filenameScale = "Scale-" + saveNow.ToString("yyyy-MMM-dd-HH-mm-ss") + ".csv";
            bool[] fInd = new bool[core.Scale.Count];
            for (int j = 0; j < core.Scale.Count-1; j++, fInd[j] = false) ;


            try
            {
                using (StreamWriter file = new StreamWriter(path + filenameScale))
                {
                    for (int i = 0; i < core.ScaleTime.Count - 1; i++)
                    {
                        csv_line = core.ScaleTime[i].ToString() + ",";
                        for (int j = 0; j < core.Scale.Count; j++)
                        {

                            try
                            {
                                csv_line += core.Scale[j][i].ToString() + ",";
                            }
                            catch { }
                        }

                        //// print F lines with culture IDs
                        //for (int j = 0; j < core.Scale.Count; j++)
                        //{
                        //    if (core.ScalePhase[j][i] == (UInt16)Phase.F)
                        //    {
                        //        string st = "";
                        //        try
                        //        {
                        //            foreach (int k in scalePhaseTubingIDs[j][scalePhaseTubingIDsI[j]])
                        //                st += k.ToString();
                        //        }
                        //        catch
                        //        {
                        //            st += "None";
                        //        }
                        //        csv_line += PhaseLabels[core.ScalePhase[j][i]] + "_" + st + ",";
                        //        fInd[j] = true;
                        //    }
                        //    else
                        //    {
                        //        csv_line += PhaseLabels[core.ScalePhase[j][i]] + ",";
                        //    }
                        //}

                        //for (int j = 0; j < core.Scale.Count; j++)
                        //    if (core.ScalePhase[j][i] != (UInt16)Phase.F)
                        //    {
                        //        if (fInd[j])
                        //            scalePhaseTubingIDsI[j]++;
                        //        fInd[j] = false;
                        //    }

                        file.WriteLine(csv_line);
                    }
                }
            }
            catch
            {
                core.logMain("Save File Error: Scale data couldn't be written.");
            }

            // delete the previous version of the data
            if (previousODFile != "")
            {
                try
                {
                    File.SetAttributes(path + previousODFile, FileAttributes.Normal);
                    File.Delete(path + previousODFile);
                }
                catch
                { }
            }
            previousODFile = filenameOD;

            if (previousScaleFile != "")
            {
                try
                {
                    File.SetAttributes(path + previousScaleFile, FileAttributes.Normal);
                    File.Delete(path + previousScaleFile);
                }
                catch
                { }
            }
            previousScaleFile = filenameScale;

            if (previousVoltageFile != "")
            {
                try
                {
                    File.SetAttributes(path + previousVoltageFile, FileAttributes.Normal);
                    File.Delete(path + previousVoltageFile);
                }
                catch
                { }
            }
            previousVoltageFile = filenameVoltage;
        }


        // protocols
        public void MainWashingProtocolA(string strength)
        {
            (new Thread(() => { MainWashingProtocolA_worker(strength); })).Start();
        }
        public void MainWashingProtocolA_worker(string strength)
        {
            // mark phase
            for (int i = 0; i < 4; i++)
                currentPhase[i] = currentPhase[8 + i] = Phase.W;

            // evacuate any leftovers
            core.logMain("[Main Washing Protocol A] Evacuating tubes.");
            core.AltEvacuateTubeAs_worker(evacuationTime);
            // waste remaining media
            core.logMain("[Main Washing Protocol A] Wasting IBA.");
            WasteIBA_worker();
            // start cleaning protocol
            core.logMain("[Main Washing Protocol A] Starting bleach washing.");
            BleachWashingProtocolA_worker(strength);
            // make media ready for next cycle
            //core.logMain("[Main Washing Protocol A] Making LB for next cycle.");
            //core.MakeLBA_worker(LBInitialFill);
            ////core.FillWaterIntoIBAUptoWeight_worker(500);
            //// debubble tubes, and fill with 20 ml media
            //core.logMain("[Main Washing Protocol A] Priming LB into tubes.");
            //core.FillTubeAsByScale_worker(dilutionTime*1.5);
            core.logMain("[Main Washing Protocol A] Completed.");

            // mark phase
            for (int i = 0; i < 4; i++)
                currentPhase[i] = currentPhase[8 + i] = Phase.I;
        }
        public void MainWashingProtocolB(string strength)
        {
            (new Thread(() => { MainWashingProtocolB_worker(strength); })).Start();
        }
        public void MainWashingProtocolB_worker(string strength)
        {
            // mark phase
            for (int i = 0; i < 4; i++)
                currentPhase[4+i] = currentPhase[12 + i] = Phase.W;

            // evacuate any leftovers
            core.logMain("[Main Washing Protocol B] Evacuating tubes.");
            core.AltEvacuateTubeBs_worker(evacuationTime);
            // waste remaining media
            core.logMain("[Main Washing Protocol B] Wasting IBB.");
            WasteIBB_worker();
            // start cleaning protocol
            core.logMain("[Main Washing Protocol B] Starting bleach washing.");
            BleachWashingProtocolB_worker(strength);
            //// make media ready for next cycle
            //core.logMain("[Main Washing Protocol B] Making LB for next cycle.");
            //core.MakeLBB_worker(LBInitialFill);
            //// debubble tubes, and fill with 20 ml media
            //core.logMain("[Main Washing Protocol B] Priming LB into tubes.");
            //core.FillTubeBsByScale_worker(dilutionTime*1.5);
            //core.logMain("[Main Washing Protocol B] Completed.");

            // mark phase
            for (int i = 0; i < 4; i++)
                currentPhase[4 + i] = currentPhase[12 + i] = Phase.I;
        }
        public void BleachWashingProtocolA(string strength)
        {
            (new Thread(() => { BleachWashingProtocolA_worker(strength); })).Start();
        }
        public void BleachWashingProtocolA_worker(string strength)
        {
            double start, end, bleach_volume, water_volume, bleach_time, water_time; // ml
            int cycle_count;
            //core.logMain("Bleach Washing Protocol A has started.");
            
            if (strength.ToLower() == "normal")
            {
                bleach_volume = bleachVolume;
                bleach_time = bleachTime;
                water_volume = waterVolume;
                water_time = waterTime;
            }
            else
            {
                bleach_volume = fBleachVolume;
                bleach_time = fBleachTime;
                water_volume = fWaterVolume;
                water_time = fWaterTime;
            }

            // Bleach cycle
            core.MakeBleachA_worker(bleach_volume * 1.011);
            core.logMain("[Bleach Washing Protocol A] IBA is filled with " + bleach_volume.ToString() + " ml bleach.");
            core.logMain("[Bleach Washing Protocol A] Bleach is getting mixed for " + (bleachMixTime * 2).ToString() + " seconds.");
            // Air mix the intermediate bottle
            core.AirMixIBA_worker(bleachMixTime);
            // wait bleach to freely mixed
            // Thread.Sleep(bleachMixTime * 1000);

            start = 1e9; end = 0; cycle_count = 0;
            while (true)
            {
                core.logMain("[Bleach Washing Protocol A] Proceeding with bleach cycle " + (++cycle_count).ToString() + ".");
                start = core.load_sensors.Read()[1];
                core.FillTubeAsByScale_worker(activeCultures,bleach_time);
                // end if last cycle of bleach
                end = core.load_sensors.Read()[1];
                if (Math.Abs(start - end) <= 50)
                {
                    // Evacuate
                    core.AltEvacuateTubeAs_worker(evacuationTime);
                    break;
                }

                // wait 10 sec before filling tubes to ensure the 
                // interior of the tubes are entirely bleached
                Thread.Sleep(10000);

                // Fill tubings with bleach
                core.AltKeepLevelA_worker(0.75);
                core.AltEvacuateTubeAs_worker(0.75);

                // Sterilization time
                Thread.Sleep(bleachCycleWaitTime*60*1000);
                // Evacuate
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.AltEvacuateTubeAs_worker(evacuationTime);
            }
            core.logMain("[Bleach Washing Protocol A] "+cycle_count.ToString() + " bleach cycles are completed.");

            // Safe-wash the IB base
            core.logMain("[Bleach Washing Protocol A] Washing IB base with water...");
            core.FillWaterIntoIBAUptoWeight_worker(200);
            start = core.load_sensors.Read()[1]; end = -200;
            while (Math.Abs(start - end) > 50)
            {
                start = core.load_sensors.Read()[1];
                //core.FillTubeAsByScale_worker(activeCultures, water_time);
                core.FillTubeAs_worker(wasteTime);
                Thread.Sleep(10000);
                core.AltEvacuateTubeAs_worker(evacuationTime * 1.5);
                end = core.load_sensors.Read()[1];
            }

            // Water cycle
            core.logMain("[Bleach Washing Protocol A] Proceeding with water cycles.");
            core.FillWaterIntoIBAUptoWeight_worker(water_volume);
            core.logMain("[Bleach Washing Protocol A] IBA is filled with " + water_volume.ToString() + " ml water.");
            // Air mix the intermediate bottle
            // core.AirMixIBA_worker(60);
            // wait water to freely dilute
            Thread.Sleep(waterMixTime * 1000);

            start = 1e9; end = 0; cycle_count = 0;
            while (Math.Abs(start - end) > 50)
            {
                core.logMain("[Bleach Washing Protocol A] Proceeding with water cycle " + (++cycle_count).ToString() + ".");
                start = core.load_sensors.Read()[1];
                core.FillTubeAsByScale_worker(activeCultures,water_time);
                //core.FillTubeAs(water_time);
                Thread.Sleep(waterCycleWaitTime * 60 * 1000);
                core.AltKeepLevelA_worker(core.keepingLevelTime);
                core.AltEvacuateTubeAs_worker(evacuationTime);
                end = core.load_sensors.Read()[1];
            }

            core.logMain("[Bleach Washing Protocol A] "+cycle_count.ToString() + " water cycles are completed.");

            core.logMain("Bleach Washing Protocol A is completed.");

        }
        public void BleachWashingProtocolB(string strength)
        {
            (new Thread(() => { BleachWashingProtocolB_worker(strength); })).Start();
        }
        public void BleachWashingProtocolB_worker(string strength)
        {
            double start, end, bleach_time, water_time, bleach_volume, water_volume;
            int  cycle_count;
            core.logMain("Bleach Washing Protocol B has started.");

            if (strength.ToLower() == "normal")
            {
                bleach_volume = bleachVolume;
                bleach_time = bleachTime;
                water_volume = waterVolume;
                water_time = waterTime;
            }
            else
            {
                bleach_volume = fBleachVolume;
                bleach_time = fBleachTime;
                water_volume = fWaterVolume;
                water_time = fWaterTime;
            }

            // Bleach cycle
            //core.FillBleachIntoIBBUptoWeight_worker(volume * 1.011); // 10%Bleach density = 1.011
            core.MakeBleachB_worker(bleach_volume * 1.011);
            core.logMain("[Bleach Washing Protocol B] IBB is filled with " + bleach_volume.ToString() + " ml bleach.");
            core.logMain("[Bleach Washing Protocol B] Bleach is getting mixed for " + (bleachMixTime * 2).ToString() + " seconds.");
            // Air mix the intermediate bottle
            core.AirMixIBB_worker(bleachMixTime);
            // wait bleach to freely mixed
            // Thread.Sleep(bleachMixTime * 1000);

            start = 1e9; end = 0; cycle_count = 0;
            while(true)
            {
                core.logMain("[Bleach Washing Protocol B] Proceeding with bleach cycle " + (++cycle_count).ToString() + ".");
                start = core.load_sensors.Read()[0];
                core.FillTubeBsByScale_worker(activeCultures,bleach_time);
                end = core.load_sensors.Read()[0];
                // end if last cycle of bleach
                if (Math.Abs(start - end) <= 50)
                {
                    // Evacuate
                    core.AltEvacuateTubeBs_worker(evacuationTime);
                    break;
                }

                // wait 10 sec before filling tubes to ensure the 
                // interior of the tubes are entirely bleached
                Thread.Sleep(10000);

                // Fill tubings with bleach
                core.AltKeepLevelB_worker(0.75);
                core.AltEvacuateTubeBs_worker(0.75);

                // Sterilization time
                Thread.Sleep(bleachCycleWaitTime*60*1000);
                // Evacuate
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.AltEvacuateTubeBs_worker(evacuationTime);

            }

            // Safe-wash the IB base
            core.logMain("[Bleach Washing Protocol B] Washing IB base with water...");
            core.FillWaterIntoIBBUptoWeight_worker(200);
            start = core.load_sensors.Read()[0]; end = -200;
            while (Math.Abs(start - end) > 50)
            {
                start = core.load_sensors.Read()[0];
                //core.FillTubeBsByScale_worker(activeCultures,water_time);
                core.FillTubeBs_worker(wasteTime);
                Thread.Sleep(10000);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.AltEvacuateTubeBs_worker(evacuationTime);
                end = core.load_sensors.Read()[0];
            }

            core.logMain(cycle_count.ToString() + " bleach cycles are completed.");

            // Water cycle
            core.logMain("[Bleach Washing Protocol B] Proceeding with water cycles.");
            core.FillWaterIntoIBBUptoWeight_worker(water_volume);
            core.logMain("[Bleach Washing Protocol B] IBB is filled with " + water_volume.ToString() + " ml water.");
            // Air mix the intermediate bottle
            //core.AirMixIBA_worker(60);
            // wait water to freely dilute
            Thread.Sleep(waterMixTime * 1000);

            start = 1e9; end = 0; cycle_count = 0;
            while (Math.Abs(start - end) > 50)
            {
                core.logMain("[Bleach Washing Protocol B] Proceeding with water cycle " + (++cycle_count).ToString() + ".");
                start = core.load_sensors.Read()[0];
                core.FillTubeBsByScale_worker(activeCultures,water_time);
                Thread.Sleep(waterCycleWaitTime*60*1000);
                core.AltKeepLevelB_worker(core.keepingLevelTime);
                core.AltEvacuateTubeBs_worker(evacuationTime);
                end = core.load_sensors.Read()[0];
            }
            core.logMain("[Bleach Washing Protocol B] " + cycle_count.ToString() + " water cycles are completed.");

        }

        public void WasteIBA()
        {
            (new Thread(() => { WasteIBA_worker(); })).Start();
        }
        public void WasteIBA_worker()
        {
            double start = 1e9, end = 0;
            while (Math.Abs(start - end) > 30)
            {
                core.logMain("Waste cycle for IBA is started." + "[" + Math.Abs(start - end).ToString() + "]");

                start = core.load_sensors.Read()[1];
                core.FillTubeAs_worker(wasteTime);
                Thread.Sleep(1000);
                core.AltEvacuateTubeAs_worker(evacuationTime);
                end = core.load_sensors.Read()[1];
            }

            core.logMain("Waste protocol for IBA is completed." + "[" + Math.Abs(start - end).ToString() + "]");
        }

        public void WasteIBB()
        {
            (new Thread(() => { WasteIBB_worker(); })).Start();
        }
        public void WasteIBB_worker()
        {
            double start = 1e9, end = 0;
            while (Math.Abs(start - end) > 30)
            {
                core.logMain("Waste cycle for IBB is started." + "[" + Math.Abs(start - end).ToString() + "]");

                start = core.load_sensors.Read()[0];
                core.FillTubeBs_worker(wasteTime);
                Thread.Sleep(1000);
                core.AltEvacuateTubeBs_worker(evacuationTime);
                end = core.load_sensors.Read()[0];
            }

            core.logMain("Waste protocol for IBB is completed." + "[" + Math.Abs(start - end).ToString() + "]");
        }

        public void depleteIBsFast(int cycle)
        {
            double filling_time=80;
            core.FillTubeAs(filling_time);

            for (int i = 0; i < cycle; i++)
            {
                core.AltEvacuateTubeAs(filling_time / 2);
                core.FillTubeBs_worker(filling_time);
                core.AltEvacuateTubeBs(filling_time / 2);
                core.FillTubeAs_worker(filling_time);
            }
        }

        // misc
        public void updateThresholds()
        {
            string[] cal = File.ReadAllLines(parameter_path + "thresholds.txt");
            int i = 0;
            foreach (string s in cal[0].Split(','))
                if (s != "")
                    ODThreshold[i++] = Single.Parse(s);
            i = 0;
            foreach (string s in cal[1].Split(','))
                if (s != "")
                    GlobalThreshold[i++] = Single.Parse(s);
        }

    }

    public class DoubleRef
    {
        public Double val { get; set; }
    }

    public class DoubleArrayRef
    {
        public Double[] values;

        public DoubleArrayRef(int size)
        {
            values = new Double[size];
        }
    }
}
