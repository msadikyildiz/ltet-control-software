using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Graph = System.Windows.Forms.DataVisualization.Charting;
using MathNet.Numerics;
using System.IO;

namespace turbido1
{
    public partial class LaserCalibrator : Form
    {
        private int numChan = 16;

        List<List<double>> OD = new List<List<double>>();
        List<List<double>> Volt = new List<List<double>>();
        double[] p0, p1;

        public List<Graph.Chart> charts = new List<Graph.Chart>();

        private int currentSample;
        TurbidoCore core;

        public LaserCalibrator(ref TurbidoCore _core)
        {
            InitializeComponent();

            core = _core;
            currentSample = 0;
            p0 = new double[numChan];
            p1 = new double[numChan];

            // initalize data arrays
            for (int i = 0; i < numChan; i++)
            {
                OD.Add(new List<double>());
                Volt.Add(new List<double>());
            }

            create_a_chart(10, 10, 320, 180, "Culture 1A");
            create_a_chart(330, 10, 320, 180, "Culture 2A");
            create_a_chart(650, 10, 320, 180, "Culture 3A");
            create_a_chart(970, 10, 320, 180, "Culture 4A");
            create_a_chart(10, 370, 320, 180, "Culture 1B");
            create_a_chart(330, 370, 320, 180, "Culture 2B");
            create_a_chart(650, 370, 320, 180, "Culture 3B");
            create_a_chart(970, 370, 320, 180, "Culture 4B");
            create_a_chart(10, 190, 320, 180, "Culture 5A");
            create_a_chart(330, 190, 320, 180, "Culture 6A");
            create_a_chart(650, 190, 320, 180, "Culture 7A");
            create_a_chart(970, 190, 320, 180, "Culture 8A");
            create_a_chart(10, 550, 320, 180, "Culture 5B");
            create_a_chart(330, 550, 320, 180, "Culture 6B");
            create_a_chart(650, 550, 320, 180, "Culture 7B");
            create_a_chart(970, 550, 320, 180, "Culture 8B");

            //core.ReinitializeODReader.Stop();
            //core.ODReader.stopContinuousRead();
            core.ODReader = new MCC_AnalogInput(0);
        }

        private void LaserCalibrator_Load(object sender, EventArgs e)
        {
            this.ClientSize = new System.Drawing.Size(1290, 740);
        }

        private void addSampleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new Thread(() => {
                System.Media.SoundPlayer beep = new System.Media.SoundPlayer(@"C:\Users\Turbidostat\Desktop\turbido1\turbido1\numbers\beep.wav");
                System.Media.SoundPlayer gong = new System.Media.SoundPlayer(@"C:\Users\Turbidostat\Desktop\turbido1\turbido1\numbers\beep.wav");
            Double[] volts = new Double[numChan];
            for (int i = 1; i <= numChan; i++)
            {
                System.Media.SoundPlayer number = new System.Media.SoundPlayer(@"C:\Users\Turbidostat\Desktop\turbido1\turbido1\numbers\" + i.ToString() + ".wav");
                number.Play();
                Thread.Sleep(1000*Int32.Parse(textBoxTubeSwitchTime.Text));
                //beep.Play();
                //beep.Stop();
                core.relays[core.LEDPDSwitch[0]].TurnOn(core.LEDPDSwitch[1]);
                Thread.Sleep(2000);
                volts[i-1]=core.ODReader.StartSingleReadingWindow(5,"Volt")[i-1];
                core.relays[core.LEDPDSwitch[0]].TurnOff(core.LEDPDSwitch[1]);
                //gong.Play();
                //gong.Stop();
                Thread.Sleep(1000);

            }

            Double od_value=Single.Parse(ShowDialog("Enter OD: ", "Enter OD"));
            AddData(volts, od_value);
            currentSample++;
            })).Start();
        }

        delegate void AddDataCallback(Double[] volts, Double od_value);
        public void AddData(Double[] volts, Double od_value)
        {
            if (charts[0].InvokeRequired)
            {
                AddDataCallback d = new AddDataCallback(AddData);
                this.Invoke(d, new object[] { volts, od_value });
            }
            else
            {
                for (int i = 0; i < numChan; i++)
                {
                    OD[i].Add(od_value);
                    Volt[i].Add(volts[i]);
                    string istr = i.ToString();
                    if (od_value > 0.9 * charts[i].ChartAreas[istr].AxisY.Maximum)
                    {
                        charts[i].ChartAreas[istr].AxisY.Maximum = od_value * 2;
                        charts[i].ChartAreas[istr].AxisY.Interval = od_value / 10;
                    }
                    if (volts[i] > 0.9 * charts[i].ChartAreas[istr].AxisX.Maximum)
                    {
                        charts[i].ChartAreas[istr].AxisX.Maximum = volts[i] * 1.1;
                        charts[i].ChartAreas[istr].AxisX.Interval = volts[i] / 10;
                    }
                    charts[i].Series[istr].Points.DataBindXY(Volt[i], OD[i]);

                }
            }
        }

        public static string ShowDialog(string text, string caption)
        {
            Form prompt = new Form();
            prompt.Width = 500;
            prompt.Height = 150;
            prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
            prompt.Text = caption;
            prompt.StartPosition = FormStartPosition.CenterScreen;
            Label textLabel = new Label() { Left = 50, Top=20, Text=text };
            TextBox textBox = new TextBox() { Left = 50, Top=50, Width=400 };
            Button confirmation = new Button() { Text = "Ok", Left=350, Width=100, Top=70 };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            return textBox.Text;
        }

        private void create_a_chart(int x, int y, int lx, int ly, string title)
        {
            const int MaxX = 5;

            charts.Add(new Graph.Chart());
            int index = charts.Count - 1;
            string name = index.ToString();

            charts[index].Location = new System.Drawing.Point(x, y);
            charts[index].Size = new System.Drawing.Size(lx, ly);

            charts[index].ChartAreas.Add(name);
            charts[index].Titles.Add(new Graph.Title(title));

            charts[index].ChartAreas[name].AxisX.Minimum = 0;
            charts[index].ChartAreas[name].AxisX.Maximum = MaxX;
            charts[index].ChartAreas[name].AxisX.Interval = 1;
            charts[index].ChartAreas[name].AxisX.LabelStyle.Format = "N";
            charts[index].ChartAreas[name].AxisX.MajorGrid.Enabled = false;// LineColor = Color.Black;
            
            //charts[index].ChartAreas[name].AxisX.MajorGrid.LineDashStyle = Graph.ChartDashStyle.Dot;

            charts[index].ChartAreas[name].AxisY.Minimum = 0.01;
            charts[index].ChartAreas[name].AxisY.Maximum = 1;
            charts[index].ChartAreas[name].AxisY.Interval = 0.2;
            charts[index].ChartAreas[name].AxisY.MajorGrid.Enabled = false;
            //charts[index].ChartAreas[name].AxisX.IsLogarithmic = true;
            charts[index].ChartAreas[name].AxisX.LogarithmBase = Math.Exp(1);
            charts[index].ChartAreas[name].AxisY.LabelStyle.Format = "0.000";

            //charts[index].ChartAreas[name].AxisY.MajorGrid.LineColor = Color.Black;
            //charts[index].ChartAreas[name].AxisY.MajorGrid.LineDashStyle = Graph.ChartDashStyle.Dot;

            // Set automatic zooming
            charts[index].ChartAreas[name].AxisX.ScaleView.Zoomable = true;
            charts[index].ChartAreas[name].AxisY.ScaleView.Zoomable = true;

            // Set automatic scrolling 
            charts[index].ChartAreas[name].CursorX.AutoScroll = true;
            charts[index].ChartAreas[name].CursorY.AutoScroll = true;

            // Allow user selection for Zoom
            charts[index].ChartAreas[name].CursorX.IsUserSelectionEnabled = true;
            charts[index].ChartAreas[name].CursorY.IsUserSelectionEnabled = true;

            charts[index].ChartAreas[name].BackColor = Color.White;

            charts[index].Series.Add(name);
            charts[index].Series[name].ChartType = Graph.SeriesChartType.Point;
            charts[index].Series[name].MarkerStyle = Graph.MarkerStyle.Circle;
            charts[index].Series[name].MarkerSize = 10;
            charts[index].Series[name].Color = Color.Black;
            charts[index].Series[name].BorderWidth = 2;

            charts[index].Series.Add("fit");
            charts[index].Series["fit"].ChartType = Graph.SeriesChartType.FastLine;
            charts[index].Series["fit"].Color = Color.Red;
            charts[index].Series["fit"].BorderWidth = 2;

            Controls.Add(this.charts[index]);
        }

        private void fitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<List<double>> fitOD = new List<List<double>>();

            // data points
            for (int i = 0; i < numChan; i++)
            {
                fitOD.Add(new List<double>());
                var xdata = new double[Volt[i].Count];
                var ydata = new double[OD[i].Count];
                for (int j = 0; j < Volt[i].Count; j++)
                {
                    xdata[j] = Math.Log(Volt[i][j]);
                    ydata[j] = (OD[i][j]);
                }

                double[] p = Fit.Polynomial(xdata, ydata, 1);
                p0[i] = p[0]; p1[i] = p[1];

                for (int j = 0; j < Volt[i].Count; j++)
                    fitOD[i].Add((Math.Log(Volt[i][j])*p1[i])+p0[i]);
                charts[i].Series["fit"].Points.DataBindXY(Volt[i], fitOD[i]);
            }
        }

        private void saveCalibrationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DateTime saveNow = DateTime.Now;
            string path = @"C:\Users\Turbidostat\Desktop\turbido_data\calibrations\";
            string filename = "LaserCalibration-" + saveNow.ToString("yyyy-MMM-dd-HH-mm-ss")+".csv";
            string csv_line;
            
            // exponential fit made for final version
            fitToolStripMenuItem_Click(sender, e);

            using (StreamWriter file = new StreamWriter(path+filename))
            {
                string p0str = "", p1str = "";
                for (int i = 0; i < numChan; i++)
                {
                    p0str += p0[i].ToString() + "\t";
                    p1str += p1[i].ToString() + "\t";
                }
                file.WriteLine(p0str);
                file.WriteLine(p1str);

                for (int i = 0; i < Volt[0].Count; i++)
                {
                    csv_line = "";
                    for (int j = 0; j < numChan; j++)
                    {
                        csv_line += Volt[j][i].ToString() + "\t";
                    }
                    csv_line += OD[0][i].ToString();
                    file.WriteLine(csv_line);
                }
            }
        }

        private void textBoxTubeSwitchTime_TextChanged(object sender, EventArgs e)
        {

        }

        private void addDummySampleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new Thread(() =>
            {
                core.relays[core.LEDPDSwitch[0]].TurnOn(core.LEDPDSwitch[1]);
                Double[] volts = (core.ODReader.StartSingleReadingWindow(1));
                core.relays[core.LEDPDSwitch[0]].TurnOff(core.LEDPDSwitch[1]);
                float od_value = Single.Parse(ShowDialog("Enter OD: ", "Enter OD"));
                AddData(volts, od_value);
                currentSample++;
            })).Start();
        }


    }
}
