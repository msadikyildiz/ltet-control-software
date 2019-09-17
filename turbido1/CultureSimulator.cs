using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

namespace turbido1
{
    public class CultureSimulator
    {
        public TurbidoCore core { get; set; }
        public TurbidoAlgorithmLibrary library;
        public Stopwatch simWatch = new Stopwatch();
        System.Timers.Timer GrowthTimer;
        public DoubleArrayRef lastOD;
        public DoubleRef lastTime;
        public Double initialOD = 0.18;       // OD

        public int numberOfCultures = 16;
        public char culture_set = 'A';
        private int dataPointPeriod = 2;    // secs
        private int cultureVolume = 7;      // ml
        private Double dilutionRate = 1;     // ml/sec

        public CultureSimulator(TurbidoAlgorithmLibrary _library)
        {
            library = _library;
            lastTime = new DoubleRef();
            lastOD = new DoubleArrayRef(numberOfCultures);
            GrowthTimer = new System.Timers.Timer(dataPointPeriod * 1000);
            GrowthTimer.Elapsed += this.GrowthTick;

            var rng = new CryptoRandomSource();
            // initialize cultures
            for (int i = 0; i < 4; i++)
            {
                if (culture_set == 'A')
                {
                    lastOD.values[i] = rng.NextDouble() * initialOD;
                    lastOD.values[8 + i] = rng.NextDouble() * initialOD;
                    lastOD.values[4 + i] = lastOD.values[12 + i] = 0;
                }
                else
                {
                    lastOD.values[4 + i] = rng.NextDouble() * initialOD;
                    lastOD.values[12 + i] = rng.NextDouble() * initialOD;
                    lastOD.values[i] = lastOD.values[8 + i] = 0;

                }
            }

        }

        public void StartExponentialGrowthSimulation()
        {
            simWatch.Start();
            GrowthTimer.Start();
        }

        private void GrowthTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            float[] nextOD = new float[numberOfCultures];
            Double instaGrowthRate;
            for (int i = 0; i < numberOfCultures; i++)
            {
                instaGrowthRate = Normal.Sample(library.assumedGrowthRate, 2);
                lastOD.values[i] *= Math.Pow(2, (Double)dataPointPeriod / (60 * instaGrowthRate));
            }
            lastTime.val = (simWatch.ElapsedMilliseconds / 1000.0);
        }


        public void DiluteTubeAWithFeedback(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeAWithFeedback_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeAWithFeedback_worker(List<int> cultureIDs)
        {
            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.currentPhase[cID] = TurbidoAlgorithmLibrary.Phase.K1;
                else
                    library.currentPhase[4 + cID] = TurbidoAlgorithmLibrary.Phase.K1;
            // pressurize IB
            // core.relays[core.AirValveToIBA[0]].TurnOn(core.AirValveToIBA[1]);
            // keep level
            // core.KeepLevelA_worker(core.keepingLevelTime);
            Thread.Sleep(core.keepingLevelTime * 1000);
            // wait for pressure stability if necessary
            if (core.keepingLevelTime < core.depressurizationTime)
                Thread.Sleep((core.depressurizationTime - core.keepingLevelTime) * 1000);
            library.currentScalePhase[1] = TurbidoAlgorithmLibrary.Phase.F;
            library.scalePhaseTubingIDs[1].Add(new List<int>());
            // Start dilution
            foreach (int cID in cultureIDs)
            {
                library.scalePhaseTubingIDs[1][library.scalePhaseTubingIDs[1].Count - 1].Add(cID);
                //core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOn(core.ASetIsolationValveRelayIDs[cID, 1]);
            }
            core.logMain("Dilutions are started.");
            // Update phase D
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.currentPhase[cID] = TurbidoAlgorithmLibrary.Phase.D1;
                else
                    library.currentPhase[4 + cID] = TurbidoAlgorithmLibrary.Phase.D1;

            // Calculate waiting order
            List<float> waitTimes = new List<float>();
            waitTimes.Add(0);
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    waitTimes.Add(library.dilTimes[cID][library.dilTimes[cID].Count - 1]);
                else
                    waitTimes.Add(library.dilTimes[4 + cID][library.dilTimes[4 + cID].Count - 1]);
            cultureIDs.Insert(0, -1);
            var waitTimesSorted = waitTimes.Select((x, i) => new KeyValuePair<float, int>(x, cultureIDs[i])).OrderBy(x => x.Key).ToList();
            cultureIDs.Remove(-1);

            List<float> B = waitTimesSorted.Select(x => x.Key).ToList();
            List<int> idx = waitTimesSorted.Select(x => x.Value).ToList();
            int wi = 1; float wt;
            // Stop dilution
            for (int i = 1; i < waitTimes.Count; i++)
            {
                // Select right culture ID
                int cID = idx[i];
                wt = (B[wi] - B[wi - 1]); wi++;
                core.logMain("Culture A" + (cID + 1).ToString() + " waitin for " + wt.ToString() + " s to stop.");
                if (cID >= 4) cID += 4;
                Thread.Sleep((Int32)(wt * 1000));
                lastOD.values[cID] *= cultureVolume / (cultureVolume + dilutionRate * B[wi - 1]);
                //core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOff(core.ASetIsolationValveRelayIDs[cID, 1]);
            }
            library.currentScalePhase[0] = TurbidoAlgorithmLibrary.Phase.I;
            // update overThr flag
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.tOverThrFlag[cID] = false;
                else
                    library.tOverThrFlag[4 + cID] = false;
            // depressurize IB
            //core.relays[core.AirValveToIBA[0]].TurnOff(core.AirValveToIBA[1]);
            // release main thread
            library.mainThreadOnWait = false;
        }

        public void DiluteTubeBWithFeedback(List<int> cultureIDs)
        {
            (new Thread(() => { DiluteTubeBWithFeedback_worker(cultureIDs); })).Start();
        }
        public void DiluteTubeBWithFeedback_worker(List<int> cultureIDs)
        {
            // update phase K
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.currentPhase[4 + cID] = TurbidoAlgorithmLibrary.Phase.K1;
                else
                    library.currentPhase[8 + cID] = TurbidoAlgorithmLibrary.Phase.K1;
            // pressurize IB
            // core.relays[core.AirValveToIBA[0]].TurnOn(core.AirValveToIBA[1]);
            // keep level
            // core.KeepLevelA_worker(core.keepingLevelTime);
            Thread.Sleep(core.keepingLevelTime * 1000);
            // wait for pressure stability if necessary
            if (core.keepingLevelTime < core.depressurizationTime)
                Thread.Sleep((core.depressurizationTime - core.keepingLevelTime) * 1000);
            library.currentScalePhase[0] = TurbidoAlgorithmLibrary.Phase.F;
            library.scalePhaseTubingIDs[0].Add(new List<int>());
            // Start dilution
            foreach (int cID in cultureIDs)
            {
                library.scalePhaseTubingIDs[1][library.scalePhaseTubingIDs[1].Count - 1].Add(cID);
                //core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOn(core.ASetIsolationValveRelayIDs[cID, 1]);
            }
            core.logMain("Dilutions are started.");
            // Update phase D
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.currentPhase[4 + cID] = TurbidoAlgorithmLibrary.Phase.D1;
                else
                    library.currentPhase[8 + cID] = TurbidoAlgorithmLibrary.Phase.D1;

            // Calculate waiting order
            List<float> waitTimes = new List<float>();
            waitTimes.Add(0);
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    waitTimes.Add(library.dilTimes[4 + cID][library.dilTimes[4 + cID].Count - 1]);
                else
                    waitTimes.Add(library.dilTimes[8 + cID][library.dilTimes[8 + cID].Count - 1]);

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
                cID += 4;
                if (cID >= 4) cID += 4;
                Thread.Sleep((Int32)(wt * 1000));
                lastOD.values[cID] *= cultureVolume / (cultureVolume + dilutionRate * B[wi - 1]);
                //core.relays[core.ASetIsolationValveRelayIDs[cID, 0]].TurnOff(core.ASetIsolationValveRelayIDs[cID, 1]);
            }
            library.currentScalePhase[0] = TurbidoAlgorithmLibrary.Phase.I;
            // update overThr flag
            foreach (int cID in cultureIDs)
                if (cID < 4)
                    library.tOverThrFlag[4 + cID] = false;
                else
                    library.tOverThrFlag[8 + cID] = false;
            // depressurize IB
            //core.relays[core.AirValveToIBA[0]].TurnOff(core.AirValveToIBA[1]);
            // release main thread
            library.mainThreadOnWait = false;
        }


        public int DataPointPeriod
        {
            get { return dataPointPeriod; }
            set
            { 
                dataPointPeriod = value;
                GrowthTimer.Interval = dataPointPeriod; 
            }
        }
    }
}
