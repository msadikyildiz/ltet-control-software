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
using Graph = System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.DataVisualization.Charting;

namespace turbido1
{
    public partial class ODMonitor : Form
    {
        private int numChan = 16;

        List<List<float>> ODplot = new List<List<float>>();
        List<float> Timeplot = new List<float>();

        public List<Graph.Chart> charts = new List<Graph.Chart>();

        public ODMonitor()
        {
            InitializeComponent();
            this.ControlBox = false;
            this.ClientSize = new System.Drawing.Size(1290, 740);
            
            // initalize data arrays
            for (int i = 0; i < numChan; i++)
                ODplot.Add(new List<float>());

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
        }

        private void ODMonitor_Load(object sender, EventArgs e)
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
            charts[index].ChartAreas[name].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            charts[index].ChartAreas[name].AxisX.LabelStyle.Format = "N";
            charts[index].ChartAreas[name].AxisX.MajorGrid.Enabled = false;// LineColor = Color.Black;
            //charts[index].ChartAreas[name].AxisX.MajorGrid.LineDashStyle = Graph.ChartDashStyle.Dot;

            charts[index].ChartAreas[name].AxisY.Minimum = 0;
            charts[index].ChartAreas[name].AxisY.Maximum = 0.01;
            charts[index].ChartAreas[name].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            charts[index].ChartAreas[name].AxisY.LabelStyle.Format = "0.000";
            charts[index].ChartAreas[name].AxisY.MajorGrid.Enabled = false;
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
            charts[index].ChartAreas[name].CursorX.Interval = 0.001;
            charts[index].ChartAreas[name].CursorY.Interval = 0.001;
            charts[index].ChartAreas[name].BackColor = Color.White;

            charts[index].Series.Add(name);
            charts[index].Series[name].ChartType = Graph.SeriesChartType.Line;
            charts[index].Series[name].Color = Color.Black;
            charts[index].Series[name].BorderWidth = 2;

            Controls.Add(this.charts[index]);
            //for (double x = 0.1; x < MaxX; x += 0.1)
            //{
            //    charts[index].Series["MyFunc"].Points.AddXY(x, Math.Sin(x) / x);
            //}

            //charts[index].Series["OD"].LegendText = "OD";
            //charts[index].Legends.Add("ODLegend");
            //charts[index].Legends["ODLegend"].BorderColor = Color.Tomato;
        }

        public void AddData(float time, Double[] od)
        {
            double refresh_limit = 1*60*60;

            // check if data is okay
            for (int i = 0; i < 16; i++)
            {
                if (od[i] == 0)
                    od[i] = (Single)(0.00001);
                if (od[i] >= 1)
                    od[i] = 1;
                if (od[i] <= -0.02)
                    od[i] = -0.02;
                if (Double.IsNaN(od[i]) || time<1)
                    return;
            }

            // move to next plotting frame
            if (Timeplot.Count > refresh_limit || Timeplot.Count == 0)
            {
                Timeplot.RemoveRange(0, Timeplot.Count);
                for (int i = 0; i < numChan; i++) ODplot[i].RemoveRange(0, ODplot[i].Count);

                // rescale axis limits
                for (int i = 0; i < numChan; i++) if (charts[i].IsHandleCreated)
                    {
                        string istr = i.ToString();
                        charts[i].Invoke((Action)(() =>
                        {
                            charts[i].ChartAreas[istr].AxisY.Maximum = od[i] + Math.Abs(0.1 * od[i]);
                            charts[i].ChartAreas[istr].AxisY.Minimum = od[i] - Math.Abs(0.1 * od[i]);
                            charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
                            charts[i].ChartAreas[istr].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;

                            charts[i].ChartAreas[istr].AxisX.Maximum = Math.Min(time * 2, time + (refresh_limit / 5));
                            charts[i].ChartAreas[istr].AxisX.Minimum = time - 0.01;
                            charts[i].Series[istr].Points.DataBindXY(Timeplot, ODplot[i]);
                        }));
                    }
            }

                Timeplot.Add(time);
                for (int i = 0; i < numChan; i++)
                    ODplot[i].Add((float)od[i]);

                for (int i = 0; i < numChan; i++)
                {
                    string istr = i.ToString();
                    if (charts[i].IsHandleCreated)
                        charts[i].Invoke((Action)(() =>
                        {
                            if (time > 0.9 * charts[i].ChartAreas[istr].AxisX.Maximum)
                            {
                                charts[i].ChartAreas[istr].AxisX.Maximum = Math.Min(time * 2, time + (refresh_limit / 5)); ;
                                charts[i].ChartAreas[istr].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
                            }
                            if (od[i] >= charts[i].ChartAreas[istr].AxisY.Maximum)
                            {
                                charts[i].ChartAreas[istr].AxisY.Maximum = od[i] + 0.1 * Math.Abs(od[i]);
                                charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
                            }
                            if (od[i] <= charts[i].ChartAreas[istr].AxisY.Minimum)
                            {
                                charts[i].ChartAreas[istr].AxisY.Minimum = od[i] - 0.1 * Math.Abs(od[i]);
                                charts[i].ChartAreas[istr].AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
                            }
                            try
                            {
                                charts[i].Series[istr].Points.DataBindXY(Timeplot, ODplot[i]);
                            }
                            catch(Exception e)
                            {
                                //// log
                            }
                        }));
                }
            }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // ODMonitor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(804, 782);
            this.Name = "ODMonitor";
            this.Text = "ODMonitor";
            this.Load += new System.EventHandler(this.ODMonitor_Load);
            this.ResumeLayout(false);
        }

    }
}
