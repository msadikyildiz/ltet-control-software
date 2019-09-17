using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Graph = System.Windows.Forms.DataVisualization.Charting;

namespace turbido1
{
    public partial class ScaleMonitor : Form
    {
        private int numChan = 2;

        //List<List<double>> Weight = new List<List<double>>();
        //List<List<double>> Time = new List<List<double>>();

        List<List<float>> Weightplot = new List<List<float>>();
        List<float> Timeplot = new List<float>();

        public List<Graph.Chart> charts = new List<Graph.Chart>();

        public ScaleMonitor()
        {
            InitializeComponent();
            this.ControlBox = false;
            this.ClientSize = new System.Drawing.Size(820, 420);
            
            // initalize data arrays
            for (int i = 0; i < numChan; i++)
            {
                //Weight.Add(new List<double>());
                //Time.Add(new List<double>());
                Weightplot.Add(new List<float>());
            }

            create_a_chart(410, 10, 400, 400, "Intermediate Bottle B");
            create_a_chart(10, 10, 400, 400, "Intermediate Bottle A");
        }

        private void ScaleMonitor_Load(object sender, EventArgs e)
        {

        }

        private void create_a_chart(int x, int y, int lx, int ly, string title)
        {
            const int MaxX = 20;

            charts.Add(new Graph.Chart());
            int index = charts.Count - 1;
            string name = index.ToString();

            charts[index].Location = new System.Drawing.Point(x, y);
            charts[index].Size = new System.Drawing.Size(lx, ly);

            charts[index].ChartAreas.Add(name);
            charts[index].Titles.Add(new Graph.Title(title));

            charts[index].ChartAreas[name].AxisX.Minimum = 0;
            charts[index].ChartAreas[name].AxisX.Maximum = MaxX;
            charts[index].ChartAreas[name].AxisX.IntervalType = DateTimeIntervalType.Auto;
            //charts[index].ChartAreas[name].AxisX.Interval = 1;
            charts[index].ChartAreas[name].AxisX.LabelStyle.Format = "N2";
            charts[index].ChartAreas[name].AxisX.MajorGrid.Enabled = false;// LineColor = Color.Black;
            //charts[index].ChartAreas[name].AxisX.MajorGrid.LineDashStyle = Graph.ChartDashStyle.Dot;

            charts[index].ChartAreas[name].AxisY.Minimum = -100;
            charts[index].ChartAreas[name].AxisY.Maximum = 1000;
            charts[index].ChartAreas[name].AxisY.IntervalType = DateTimeIntervalType.Auto;
            //charts[index].ChartAreas[name].AxisY.Interval = 0.2;
            charts[index].ChartAreas[name].AxisY.LabelStyle.Format = "N0";
            charts[index].ChartAreas[name].AxisY.MajorGrid.Enabled = false;
            //charts[index].ChartAreas[name].AxisY.MajorGrid.LineColor = Color.Black;
            //charts[index].ChartAreas[name].AxisY.MajorGrid.LineDashStyle = Graph.ChartDashStyle.Dot;

            // Allow user
            //charts[index].ChartAreas[name].CursorX.IsUserEnabled = true;
            //charts[index].ChartAreas[name].CursorY.IsUserEnabled = true;
            // Allow user selection for Zoom
            charts[index].ChartAreas[name].CursorX.IsUserSelectionEnabled = true;
            charts[index].ChartAreas[name].CursorY.IsUserSelectionEnabled = true;
            charts[index].ChartAreas[name].CursorX.Interval = 0.001;
            //charts[index].ChartAreas[name].CursorX.IntervalType = DateTimeIntervalType.Auto;
            charts[index].ChartAreas[name].CursorY.Interval = 0.001;
            //charts[index].ChartAreas[name].CursorY.IntervalType = DateTimeIntervalType.Auto;
            // Set automatic zooming
            charts[index].ChartAreas[name].AxisX.ScaleView.Zoomable = true;
            charts[index].ChartAreas[name].AxisY.ScaleView.Zoomable = true;

            // Set automatic scrolling 
            //charts[index].ChartAreas[name].CursorX.AutoScroll = true;
            //charts[index].ChartAreas[name].CursorY.AutoScroll = true;

 

            charts[index].ChartAreas[name].BackColor = Color.White;

            charts[index].Series.Add(name);
            charts[index].Series[name].ChartType = Graph.SeriesChartType.Line;
            charts[index].Series[name].Color = Color.Black;
            charts[index].Series[name].BorderWidth = 2;

            Controls.Add(this.charts[index]);
        }

        public void AddData(float time, Double[] scale)
        {
            double refresh_limit = 1*60*60;

            // move to next plotting frame
            if (Timeplot.Count > refresh_limit || Timeplot.Count == 0)
            {
                Timeplot.RemoveRange(0, Timeplot.Count);
                for (int i = 0; i < numChan; i++) Weightplot[i].RemoveRange(0, Weightplot[i].Count);

                // rescale axis limits
                 if (charts[0].IsHandleCreated)
                        charts[0].Invoke((Action)(() =>
                        {
                            for (int i = 0; i < numChan; i++)
                            {
                                string istr = i.ToString();
                                charts[i].ChartAreas[istr].AxisY.Maximum = scale[i] + Math.Abs(0.1 * scale[i]);
                                charts[i].ChartAreas[istr].AxisY.Minimum = scale[i] - Math.Abs(0.1 * scale[i]);
                                charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
                                charts[i].ChartAreas[istr].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;

                                charts[i].ChartAreas[istr].AxisX.Maximum = Math.Min(time * 2, time + (refresh_limit / 5));
                                charts[i].ChartAreas[istr].AxisX.Minimum = time - 0.01;
                                charts[i].Series[istr].Points.DataBindXY(Timeplot, Weightplot[i]);
                            }
                        }));
            }


            Timeplot.Add(time);
            for (int i = 0; i < numChan; i++)
                Weightplot[i].Add((float)scale[i]);

            if (charts[0].IsHandleCreated)
                charts[0].Invoke((Action)(() =>
                {
                    for (int i = 0; i < numChan; i++)
                    {
                        string istr = i.ToString();
                        if (time > charts[i].ChartAreas[istr].AxisX.Maximum)
                        {
                            charts[i].ChartAreas[istr].AxisX.Maximum = Math.Min(time*2, time+(refresh_limit/5));
                            charts[i].ChartAreas[istr].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
                        }
                        if (scale[i] > charts[i].ChartAreas[istr].AxisY.Maximum)
                        {
                            charts[i].ChartAreas[istr].AxisY.Maximum = scale[i] * 1.1;
                            charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount; 

                        }
                        if (scale[i] <= charts[i].ChartAreas[istr].AxisY.Minimum)
                        {
                            charts[i].ChartAreas[istr].AxisY.Minimum= scale[i] - 0.1*Math.Abs(scale[i]);
                            charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount; 

                        }
                       // try
                       // {
                            charts[i].Series[istr].Points.DataBindXY(Timeplot, Weightplot[i]);
                       // }
                       // catch { }
                    }
                }));
        }
    }
}
