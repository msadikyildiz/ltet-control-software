using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;


namespace turbido1
{
    public partial class TurbidostatControlPanel : Form
    {
        TurbidoCore core;
        TurbidoAlgorithmLibrary library;
        ODMonitor od_monitor = new ODMonitor();
        ScaleMonitor scale_monitor = new ScaleMonitor();

        // scale calibration
        float scaleLowRaw, scaleLowSet, scaleHighRaw, scaleHighSet;

        // Control Panel GUI
        double safety_waiting = 0.5;
        DateTime logMainLastMessageTime;
        List<string> logMainLastMessages = new List<string>();
        List<LaserCalibrationFile> laserCalibrationDataSource;
        BindingList<LaserCalibrationFile> laserBindingList = new BindingList<LaserCalibrationFile>();
        BindingSource laserBSource = new BindingSource();

        public TurbidostatControlPanel(ref TurbidoCore core_)
        {
            InitializeComponent();
            this.ControlBox = false;
            timerSystem.Start();
            core = core_;
            library = new TurbidoAlgorithmLibrary(ref core);
            core.library = library;
            core.controlPanel = this;
            library.StartODVoltageScaleDataSaver();
            od_monitor.Hide();
            scale_monitor.Hide();
            core.assignMonitors(ref od_monitor, ref scale_monitor);
            core.MainLog.CollectionChanged += updateLog;

            textBoxParameterTransferPeriod.Text = library.TransferCyclePeriod.ToString();
        }

        
        delegate void updateLogCallback(object sender, NotifyCollectionChangedEventArgs e);
        public void updateLog(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                DateTime now = DateTime.Now;
                if (textBoxLogMain.InvokeRequired)
                {
                    updateLogCallback d = new updateLogCallback(updateLog);
                    this.Invoke(d, new object[] { sender, e });
                }
                else
                {
                    textBoxLogMain.AppendText(core.MainLog[core.MainLog.Count - 1].time.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        + " " + core.MainLog[core.MainLog.Count - 1].message + "\n");
                }
            }
            catch
            {
                // skip logging if it interfere with other GUI operations
            }
        }

        delegate string SelectedLaserCalibrationPathD();
        public string SelectedLaserCalibrationPath()
        {
            if (comboBoxSelectLaserCalibration.InvokeRequired)
            {
                SelectedLaserCalibrationPathD d = new SelectedLaserCalibrationPathD(SelectedLaserCalibrationPath);
                return this.Invoke(d, new object[] { }).ToString();
            }
            else
            {
                return ((LaserCalibrationFile)comboBoxSelectLaserCalibration.SelectedItem).Path.ToString();
            }
        }

        private void TurbidostatControlPanel_Load(object sender, EventArgs e)
        {
            // populate laser calibration combobox
            laserBSource.DataSource = laserBindingList;
            comboBoxSelectLaserCalibration.DataSource = laserBSource;

            laserCalibrationDataSource = new List<LaserCalibrationFile>();
            string[] files = Directory.GetFiles(@"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\", "LaserCalibration*.csv");
            for (int i = 0; i < files.Length;i++)
                laserBindingList.Add(new LaserCalibrationFile() { Name = Path.GetFileName(files[i]), Path = files[i] });

            this.comboBoxSelectLaserCalibration.DisplayMember = "Name";
            this.comboBoxSelectLaserCalibration.ValueMember = "Path";

            this.textBoxMediaAirationPeriod.Text = library.MediaAirationCyclePeriod.ToString();
            this.textBoxParameterTransferPeriod.Text = library.TransferCyclePeriod.ToString();
        }

        private void TurbidostatControlPanel_FormClosed(object sender, FormClosedEventArgs e)
        {

        }


        // GUI Methods

        delegate void logMainCallback(string text);
        public void logMain(string message)
        {
            DateTime now = DateTime.Now;
            if (textBoxLogMain.InvokeRequired)
            {
                logMainCallback d = new logMainCallback(logMain);
                this.Invoke(d, new object[] { message });
            }
            else
            {
                if ((logMainLastMessageTime.ToString("yyyy-MM-ddTHH:mm:ssZ") != now.ToString("yyyy-MM-ddTHH:mm:ssZ")))
                {
                    textBoxLogMain.AppendText(now.ToString("yyyy-MM-ddTHH:mm:ssZ") + " " + message + "\n");
                    logMainLastMessageTime = now;
                    logMainLastMessages.Clear();
                    logMainLastMessages.Add(message);
                }
                else if (!logMainLastMessages.Contains(message))
                {
                    textBoxLogMain.AppendText(now.ToString("yyyy-MM-ddTHH:mm:ssZ") + " " + message + "\n");
                    logMainLastMessages.Add(message);
                }
            }
        }
        // end of GUI Methods

        private void buttonWashTubeA_Click(object sender, EventArgs e)
        {
            (new Thread(() =>
            {
                if (textBoxWashTubeACultureID.Text == "1:8")
                {
                    for (int j = 0; j < Int32.Parse(textBoxWashTubeACycle.Text); j++)
                    {
                        core.WashTubeAs_worker(Double.Parse(textBoxWashTubeATime.Text), 1);
                    }
                }
                else
                {
                    for (int j = 0; j < Int32.Parse(textBoxWashTubeACycle.Text); j++)
                    {
                        if (textBoxWashTubeACultureID.Text.Contains(":"))
                        {
                            string[] ss = textBoxWashTubeACultureID.Text.Split(':');
                            for (int i = Int32.Parse(ss[0]); i < (Int32.Parse(ss[1])); i++)
                                (new Thread(() => { core.WashTubeA_worker(i - 1, Double.Parse(textBoxWashTubeATime.Text)); })).Start();
                            core.WashTubeA_worker(Int32.Parse(ss[1]) - 1, Double.Parse(textBoxWashTubeATime.Text));
                        }
                        else
                            core.WashTubeA_worker(Int32.Parse(textBoxWashTubeACultureID.Text) - 1, Double.Parse(textBoxWashTubeATime.Text));
                    }
                }
            })).Start();
        }

        private void buttonWashTubeB_Click(object sender, EventArgs e)
        {
           (new Thread(() =>
           {
                if (textBoxWashTubeBCultureID.Text == "1:8")
                {
                    for (int j = 0; j < Int32.Parse(textBoxWashTubeBCycle.Text); j++)
                    {
                        core.WashTubeBs_worker(Double.Parse(textBoxWashTubeBTime.Text), 1);
                    }
                }
                else
                {
                    for (int j = 0; j < Int32.Parse(textBoxWashTubeBCycle.Text); j++)
                    {
                        if (textBoxWashTubeBCultureID.Text.Contains(":"))
                        {
                            string[] ss = textBoxWashTubeBCultureID.Text.Split(':');
                            for (int i = Int32.Parse(ss[0]); i < (Int32.Parse(ss[1])); i++)
                                (new Thread(() => { core.WashTubeB_worker(i - 1, Double.Parse(textBoxWashTubeBTime.Text)); })).Start();
                            core.WashTubeB_worker(Int32.Parse(ss[1]) - 1, Double.Parse(textBoxWashTubeBTime.Text));
                        }
                        else
                            core.WashTubeB(Int32.Parse(textBoxWashTubeBCultureID.Text) - 1, Double.Parse(textBoxWashTubeBTime.Text));
                    }
                }
            })).Start();
        }

        private void buttonMediaIntoIntBottle_Click(object sender, EventArgs e)
        {
            core.relaybox1.TurnOn(22); // air to media bottle
            core.relaybox1.TurnOn(1); // turn media valve
        }

        private void buttonAllPumpsOff_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 1; i <= 24; i++)
                {
                    core.relaybox1.TurnOff(i);
                    core.relaybox2.TurnOff(i);
                    core.relaybox3.TurnOff(i);
                }
                 logMain("All valves are off.");
            }
            catch 
            {
                logMain("ALERT! Cannot turn off the valves.");
            }
            // update toggle buttons in the GUI
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void timerLoadSensors_Tick(object sender, EventArgs e)
        {

        }

        private void buttonStartReading_Click(object sender, EventArgs e)
        {
           
        }

        private void buttonStopReading_Click(object sender, EventArgs e)
        {

        }

        private void timerUpdateGUI_Tick(object sender, EventArgs e)
        {
        
        }

       
        private void buttonManualCommandOn_Click(object sender, EventArgs e)
        {
            int relaybox_number = Int32.Parse(textBoxManualCommandRelaybox.Text);
            int channel = Int32.Parse(textBoxManualCommandChannel.Text);
            core.relays[relaybox_number - 1].TurnOn(channel);
            core.logMain("Relaybox " + relaybox_number.ToString() + " relay " + channel.ToString() + " is on.");
        }

        private void buttonManualCommandOff_Click(object sender, EventArgs e)
        {
            int relaybox_number = Int32.Parse(textBoxManualCommandRelaybox.Text);
            int channel = Int32.Parse(textBoxManualCommandChannel.Text);
            core.relays[relaybox_number - 1].TurnOff(channel);
            core.logMain("Relaybox " + relaybox_number.ToString() + " relay " + channel.ToString() + " is off.");
        }

        private void buttonTransferAtoB_Click(object sender, EventArgs e)
        {
            (new Thread(() =>
            {
                List<int> cults = new List<int>();

                if (textBoxTransferAtoBCultureIDs.Text.Contains(":"))
                {
                    string[] ss = textBoxTransferAtoBCultureIDs.Text.Split(':');
                    for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                        cults.Add(i - 1);
                }
                else
                    cults.Add(Int32.Parse(textBoxTransferAtoBCultureIDs.Text)-1);
                        
                core.TransferAtoBSelect(cults, Double.Parse(textBoxTransferAtoBTime.Text));
            })).Start();
        }

        private void buttonTransferBtoA_Click(object sender, EventArgs e)
        {
            (new Thread(() =>
            {
                List<int> cults = new List<int>();

                if (textBoxTransferBtoACultureIDs.Text.Contains(":"))
                {
                    string[] ss = textBoxTransferBtoACultureIDs.Text.Split(':');
                    for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                        cults.Add(i - 1);
                }
                else
                    cults.Add(Int32.Parse(textBoxTransferBtoACultureIDs.Text)-1);

                core.TransferBtoASelect(cults, Double.Parse(textBoxTransferBtoATime.Text));

            })).Start();
        }

        private void textBoxTransferBtoATime_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonFillTubeA_Click(object sender, EventArgs e)
        {
            if (textBoxFillTubeACultureID.Text.Contains(":"))
            {
                string[] ss = textBoxFillTubeACultureID.Text.Split(':');
                List<int> tubes = new List<int>();
                for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                    tubes.Add(i - 1);
                core.FillTubeAsByScale(tubes, Double.Parse(textBoxFillTubeATime.Text));
            }
            else
            {
                List<int> selected = new List<int>();
                selected.Add(Int32.Parse(textBoxFillTubeACultureID.Text) - 1);
                core.FillTubeAsByScale(selected, Double.Parse(textBoxFillTubeATime.Text));
                //core.FillTubeA(Int32.Parse(textBoxFillTubeACultureID.Text) - 1, Double.Parse(textBoxFillTubeATime.Text));
            }
        }

        private void buttonFillTubeB_Click(object sender, EventArgs e)
        {
            if (textBoxFillTubeBCultureID.Text.Contains(":"))
            {
                string[] ss = textBoxFillTubeBCultureID.Text.Split(':');
                List<int> tubes = new List<int>();
                for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                    tubes.Add(i - 1);
                core.FillTubeBsByScale(tubes, Double.Parse(textBoxFillTubeBTime.Text));
            }
            else
            {
                List<int> selected=new List<int>();
                selected.Add(Int32.Parse(textBoxFillTubeBCultureID.Text) - 1);
                core.FillTubeBsByScale(selected, Double.Parse(textBoxFillTubeBTime.Text));
                //core.FillTubeB(Int32.Parse(textBoxFillTubeBCultureID.Text) - 1, Double.Parse(textBoxFillTubeBTime.Text));   
            }
        }

        private void buttonEvacuateTubeA_Click(object sender, EventArgs e)
        {
            if (textBoxEvacuateTubeACultureID.Text.Contains(":"))
            {
                string[] ss = textBoxEvacuateTubeACultureID.Text.Split(':');
                if (ss[0] == "1" && ss[1] == "8")
                    core.AltEvacuateTubeAs(Double.Parse(textBoxEvacuateTubeATime.Text));
                else
                {
                    for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                        core.EvacuateTubeA(i - 1, Double.Parse(textBoxEvacuateTubeATime.Text));
                }
            }
            else
                core.EvacuateTubeA(Int32.Parse(textBoxEvacuateTubeACultureID.Text) - 1, Double.Parse(textBoxEvacuateTubeATime.Text));
        }

        private void buttonEvacuateTubeB_Click(object sender, EventArgs e)
        {
            if (textBoxEvacuateTubeBCultureID.Text.Contains(":"))
            {
                string[] ss = textBoxEvacuateTubeBCultureID.Text.Split(':');
                if (ss[0] == "1" && ss[1] == "8")
                    core.AltEvacuateTubeBs(Double.Parse(textBoxEvacuateTubeBTime.Text));
                else
                {
                    for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                        core.EvacuateTubeB(i - 1, Double.Parse(textBoxEvacuateTubeBTime.Text));
                }
            }
            else
                core.EvacuateTubeB(Int32.Parse(textBoxEvacuateTubeBCultureID.Text) - 1, Double.Parse(textBoxEvacuateTubeBTime.Text));
        }

        private void buttonKeepLevelTubeA_Click(object sender, EventArgs e)
        {
            core.AltKeepLevelA(Double.Parse(textBoxKeepLevelTubeATime.Text));
        }

        private void buttonKeepLevelTubeB_Click(object sender, EventArgs e)
        {
            core.AltKeepLevelB(Double.Parse(textBoxKeepLevelTubeBTime.Text));
        }

        private void buttonFillMediaIntoIBA_Click(object sender, EventArgs e)
        {
            core.FillMediaIntoIBA(Double.Parse(textBoxFillMediaIntoIBATime.Text));
        }

        private void buttonFillMediaIntoIBB_Click(object sender, EventArgs e)
        {
            core.FillMediaIntoIBB(Double.Parse(textBoxFillMediaIntoIBATime.Text));
        }

        private void buttonFillBleachIntoIBATime_Click(object sender, EventArgs e)
        {
            core.FillBleachIntoIBA(Double.Parse(textBoxFillBleachIntoIBATime.Text));
        }

        private void buttonFillBleachIntoIBB_Click(object sender, EventArgs e)
        {
            core.FillBleachIntoIBB(Double.Parse(textBoxFillBleachIntoIBATime.Text));
        }

        private void buttonFillWaterIntoIBA_Click(object sender, EventArgs e)
        {
            core.FillWaterIntoIBA(Double.Parse(textBoxFillWaterIntoIBATime.Text));
        }

        private void buttonWaterIntoIBB_Click(object sender, EventArgs e)
        {
            core.FillWaterIntoIBB(Double.Parse(textBoxFillWaterIntoIBATime.Text));
        }

        private void checkBoxCalibrateScaleA_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxCalibrateScaleA.CheckState == CheckState.Checked)
            {
                textBoxScaleCalLow.Enabled = true;
                buttonScaleCalSetLow.Enabled = true;
                checkBoxCalibrateScaleB.CheckState = CheckState.Unchecked;
                checkBoxCalibrateScaleB.Enabled = false;
            }
            else 
            {
                textBoxScaleCalLow.Enabled = false;
                textBoxScaleCalHigh.Enabled = false;
                buttonScaleCalSetHigh.Enabled = false;
                buttonScaleCalSetLow.Enabled = false;
                checkBoxCalibrateScaleB.Enabled = true;
            }
        }

        private void checkBoxCalibrateScaleB_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxCalibrateScaleB.CheckState == CheckState.Checked)
            {
                textBoxScaleCalLow.Enabled = true;
                buttonScaleCalSetLow.Enabled = true;
                checkBoxCalibrateScaleA.CheckState = CheckState.Unchecked;
                checkBoxCalibrateScaleA.Enabled = false;
            }
            else
            {
                textBoxScaleCalLow.Enabled = false;
                textBoxScaleCalHigh.Enabled = false;
                buttonScaleCalSetHigh.Enabled = false;
                buttonScaleCalSetLow.Enabled = false;
                checkBoxCalibrateScaleA.Enabled = true;
            }
        }

        private void buttonScaleCalSetLow_Click(object sender, EventArgs e)
        {
            float[] raw_value = new float[2];

            scaleLowSet = Single.Parse(textBoxScaleCalLow.Text);
            raw_value = core.load_sensors.ReadRaw();
            if (checkBoxCalibrateScaleA.CheckState == CheckState.Checked)
                scaleLowRaw = raw_value[1];
            else
                scaleLowRaw = raw_value[0];

            textBoxScaleCalHigh.Enabled = true;
            buttonScaleCalSetHigh.Enabled = true;
        }

        private void buttonScaleCalSetHigh_Click(object sender, EventArgs e)
        {
            float[] raw_value = new float[2];
            raw_value = core.load_sensors.ReadRaw();
            scaleHighSet = Single.Parse(textBoxScaleCalHigh.Text);

            if (checkBoxCalibrateScaleA.CheckState == CheckState.Checked)
            {
                scaleHighRaw = raw_value[1];
                core.load_sensors.recalibrateScale(1, scaleLowRaw, scaleLowSet, scaleHighRaw, scaleHighSet);
                checkBoxCalibrateScaleA.CheckState = CheckState.Unchecked;
            }
            else
            {
                scaleHighRaw = raw_value[0];
                core.load_sensors.recalibrateScale(0, scaleLowRaw, scaleLowSet, scaleHighRaw, scaleHighSet);
                checkBoxCalibrateScaleB.CheckState = CheckState.Unchecked;
            }

        }

        private void buttonMakeNewCalibration_Click(object sender, EventArgs e)
        {
            LaserCalibrator lasercal = new LaserCalibrator(ref core);
            lasercal.Show();
        }

        private void checkBoxODMonitor_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxODMonitor.CheckState == CheckState.Checked)
                od_monitor.Show();
            else
                od_monitor.Hide();
        }

        private void checkBoxScaleMonitor_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxScaleMonitor.CheckState == CheckState.Checked)
                scale_monitor.Show();
            else
                scale_monitor.Hide();
        }

        private void comboBoxSelectLaserCalibration_SelectedIndexChanged(object sender, EventArgs e)
        {
            string path = ((LaserCalibrationFile)comboBoxSelectLaserCalibration.SelectedItem).Path.ToString();
            string[] lascal = File.ReadAllLines(path);
            float[] p1 = new float[core.ODReader.HighChan + 1], p0 = new float[core.ODReader.HighChan + 1];
            int i=0;
            foreach (string s in lascal[0].Split('\t'))
                if(s!="")
                p0[i++] = Single.Parse(s);
            i = 0;
            foreach (string s in lascal[1].Split('\t'))
                if (s != "")
                p1[i++] = Single.Parse(s);
            core.ODReader.updateCalibrationVectors(p1, p0, path);
        }

        private void buttonMakeLBInIBA_Click(object sender, EventArgs e)
        {
            core.MakeLBA(Double.Parse(textBoxMakeLBInIBAGrams.Text));
        }


        private void buttonMakeLBInIBB_Click(object sender, EventArgs e)
        {
            core.MakeLBB(Double.Parse(textBoxMakeLBInIBAGrams.Text));
        }

        public void EmergencyExit()
        {
            for (int i = 1; i <= 24; i++)
            {
                core.relaybox1.TurnOff(i);
                core.relaybox2.TurnOff(i);
            }

            //logMain("[EMERGENCY EXIT] All valves are off.");
        }

        private void TurbidostatControlPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            EmergencyExit();
        }

        private void groupBoxTurbidostatParameters_Enter(object sender, EventArgs e)
        {

        }

        private void buttonQuickWasteIBA_Click(object sender, EventArgs e)
        {
            library.WasteIBA();
        }

        private void buttonQuickWasteIBB_Click(object sender, EventArgs e)
        {
            library.WasteIBB();
        }

        private void buttonMainWashingProtocolA_Click(object sender, EventArgs e)
        {
            library.MainWashingProtocolA(comboBoxMainWashingProtocolModeA.SelectedItem.ToString());
        }

        private void buttonStartTurbidostatCycler_Click(object sender, EventArgs e)
        {
            library.StartAutomatedTurbidostat();
        }

        private void buttonStopTurbidostatCycler_Click(object sender, EventArgs e)
        {
            library.StopTurbidostat();
        }

        private void buttonBleachWashingProtocolB_Click(object sender, EventArgs e)
        {
            library.BleachWashingProtocolB("normal");
        }

        private void buttonBleachWashingProtocolA_Click(object sender, EventArgs e)
        {
            library.BleachWashingProtocolA("normal");
        }

        private void buttonMainWashingProtocolB_Click(object sender, EventArgs e)
        {
            library.MainWashingProtocolB(comboBoxMainWashingProtocolModeB.SelectedItem.ToString());
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void buttonDiluteTubeA_Click(object sender, EventArgs e)
        {
            List <int> over_thr = new List<int>();
            if (textBoxDiluteTubeACultureID.Text.Contains(":"))
            {
                string[] ss = textBoxDiluteTubeACultureID.Text.Split(':');
                for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                    over_thr.Add(i-1);
                core.DiluteTubeA(over_thr, Double.Parse(textBoxDiluteTubeATime.Text));
            }
            else
            {
                over_thr.Add(Int32.Parse(textBoxDiluteTubeACultureID.Text) - 1);
                core.DiluteTubeA(over_thr, Double.Parse(textBoxDiluteTubeATime.Text));
            }
        }

        private void buttonDiluteTubeB_Click(object sender, EventArgs e)
        {
            List<int> over_thr = new List<int>();
            if (textBoxDiluteTubeBCultureID.Text.Contains(":"))
            {
                string[] ss = textBoxDiluteTubeBCultureID.Text.Split(':');
                for (int i = Int32.Parse(ss[0]); i <= (Int32.Parse(ss[1])); i++)
                    over_thr.Add(i - 1);
                core.DiluteTubeB(over_thr, Double.Parse(textBoxDiluteTubeBTime.Text));
            }
            else
            {
                over_thr.Add(Int32.Parse(textBoxDiluteTubeBCultureID.Text) - 1);
                core.DiluteTubeB(over_thr, Double.Parse(textBoxDiluteTubeBTime.Text));
            }
        }

        private void buttonMakeBleachIBA_Click(object sender, EventArgs e)
        {
            core.MakeBleachA(Double.Parse(textBoxMakeBleachIBAGrams.Text));
        }

        private void buttonMakeBleachIBB_Click(object sender, EventArgs e)
        {
            core.MakeBleachB(Double.Parse(textBoxMakeBleachIBAGrams.Text));
        }

        private void buttonReadBlank_Click(object sender, EventArgs e)
        {
            // Turn on LED/PDs, and measure OD
            core.relays[core.LEDPDSwitch[0]].TurnOn(core.LEDPDSwitch[1]);
            core.ODReader.readBlank();
            core.relays[core.LEDPDSwitch[0]].TurnOff(core.LEDPDSwitch[1]);


            LaserCalibrationFile autocal = new LaserCalibrationFile() { 
                Name = Path.GetFileName(core.ODReader.currentCalibrationDatafile), 
                Path = core.ODReader.currentCalibrationDatafile };
            laserBindingList.Add(autocal);
            comboBoxSelectLaserCalibration.SelectedItem = autocal;
        }


        private void buttonReadBlankA_Click(object sender, EventArgs e)
        {
            // Turn on LED/PDs, and measure OD
            core.relays[core.LEDPDSwitch[0]].TurnOn(core.LEDPDSwitch[1]);
            core.ODReader.readBlankA();
            core.relays[core.LEDPDSwitch[0]].TurnOff(core.LEDPDSwitch[1]);


            LaserCalibrationFile autocal = new LaserCalibrationFile()
            {
                Name = Path.GetFileName(core.ODReader.currentCalibrationDatafile),
                Path = core.ODReader.currentCalibrationDatafile
            };
            laserBindingList.Add(autocal);
            comboBoxSelectLaserCalibration.SelectedItem = autocal;
        }

        private void buttonReadBlankB_Click(object sender, EventArgs e)
        {
            // Turn on LED/PDs, and measure OD
            core.relays[core.LEDPDSwitch[0]].TurnOn(core.LEDPDSwitch[1]);
            core.ODReader.readBlankB();
            core.relays[core.LEDPDSwitch[0]].TurnOff(core.LEDPDSwitch[1]);


            LaserCalibrationFile autocal = new LaserCalibrationFile()
            {
                Name = Path.GetFileName(core.ODReader.currentCalibrationDatafile),
                Path = core.ODReader.currentCalibrationDatafile
            };
            laserBindingList.Add(autocal);
            comboBoxSelectLaserCalibration.SelectedItem = autocal;
        }



        private void buttonDepleteIBsFast_Click(object sender, EventArgs e)
        {
            library.depleteIBsFast(50);
        }

        private void buttonDebubbleTubeA_Click(object sender, EventArgs e)
        {
            core.DebubbleTubeAs(5);
        }

        private void buttonDebubbleTubeB_Click(object sender, EventArgs e)
        {
            core.DebubbleTubeBs(5);
        }

        private void buttonAddDrug1IntoIBA_Click(object sender, EventArgs e)
        {
            core.AddDrug1IntoIBA(Double.Parse(textBoxAddDrug1IntoIBAGrams.Text));
        }

        private void buttonAddDrug1IntoIBB_Click(object sender, EventArgs e)
        {
            core.AddDrug1IntoIBB(Double.Parse(textBoxAddDrug1IntoIBAGrams.Text));
        }

        private void buttonAddDrug2IntoIBA_Click(object sender, EventArgs e)
        {
            core.AddDrug2IntoIBA(Double.Parse(textBoxAddDrug2IntoIBAGrams.Text));
        }

        private void buttonAddDrug2IntoIBB_Click(object sender, EventArgs e)
        {
            core.AddDrug2IntoIBB(Double.Parse(textBoxAddDrug2IntoIBAGrams.Text));
        }

        private void buttonAddIPTGIntoIBA_Click(object sender, EventArgs e)
        {
            core.AddIPTGIntoIBA(Double.Parse(textBoxAddIPTGIntoIBAGrams.Text));
        }

        private void buttonAddIPTGIntoIBB_Click(object sender, EventArgs e)
        {
            core.AddIPTGIntoIBB(Double.Parse(textBoxAddIPTGIntoIBAGrams.Text));
        }

        private void timerSystem_Tick(object sender, EventArgs e)
        {
            this.labelSystemTime.Text = DateTime.Now.ToLongTimeString();
        }

        private void buttonSetODA_Click(object sender, EventArgs e)
        {
            core.setODA(Double.Parse(textBoxSetOD.Text), Int32.Parse(textBoxSetODCultureID.Text));
        }

        private void buttonSetODB_Click(object sender, EventArgs e)
        {
            core.setODB(Double.Parse(textBoxSetOD.Text), Int32.Parse(textBoxSetODCultureID.Text));
        }

        private void radioButtonStartingCultureA_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonStartingCultureA.Checked == true)
                library.culture_set = 'A';
        }

        private void radioButtonStartingCultureB_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonStartingCultureB.Checked == true)
                library.culture_set = 'B';
        }

        private void buttonRefreshODMonitor_Click(object sender, EventArgs e)
        {
            od_monitor.Close();
            od_monitor = new ODMonitor();
            core.assignMonitors(ref od_monitor, ref scale_monitor);
            od_monitor.Show();
        }

        private void buttonWasteIBA_Click(object sender, EventArgs e)
        {
            library.WasteIBA();
        }

        private void buttonWasteIBB_Click(object sender, EventArgs e)
        {
            library.WasteIBB();
        }

        private void textBoxParameterTransferPeriod_TextChanged(object sender, EventArgs e)
        {
            library.TransferCyclePeriod = Double.Parse(textBoxParameterTransferPeriod.Text);
        }

        private void textBoxMediaAirationPeriod_TextChanged(object sender, EventArgs e)
        {
            library.MediaAirationCyclePeriod = Double.Parse(textBoxMediaAirationPeriod.Text);
        }

        private void buttonFillTubeAsByScale_Click(object sender, EventArgs e)
        {
            core.FillTubeAsByScale(6);
        }

        private void buttonFillTubeAFast_Click(object sender, EventArgs e)
        {
            core.FillTubeAs(Double.Parse(textBoxFillTubeATime.Text));
        }

        private void buttonFillTubeBFast_Click(object sender, EventArgs e)
        {
            core.FillTubeBs(Double.Parse(textBoxFillTubeBTime.Text));
        }

        private void checkBoxODReader_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxODReader.CheckState == CheckState.Checked)
            {
                core.DataCollector.Start();
                core.ReinitializeODReader.Start();
            }
            else
            {
                core.DataCollector.Stop();
                core.ReinitializeODReader.Stop();
            }
        }

    }

    public class LaserCalibrationFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

}
;