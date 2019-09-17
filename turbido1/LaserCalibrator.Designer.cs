namespace turbido1
{
    partial class LaserCalibrator
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.addSampleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveCalibrationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textBoxTubeSwitchTime = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.addDummySampleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addSampleToolStripMenuItem,
            this.fitToolStripMenuItem,
            this.saveCalibrationToolStripMenuItem,
            this.addDummySampleToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1614, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // addSampleToolStripMenuItem
            // 
            this.addSampleToolStripMenuItem.Name = "addSampleToolStripMenuItem";
            this.addSampleToolStripMenuItem.Size = new System.Drawing.Size(83, 20);
            this.addSampleToolStripMenuItem.Text = "Add Sample";
            this.addSampleToolStripMenuItem.Click += new System.EventHandler(this.addSampleToolStripMenuItem_Click);
            // 
            // fitToolStripMenuItem
            // 
            this.fitToolStripMenuItem.Name = "fitToolStripMenuItem";
            this.fitToolStripMenuItem.Size = new System.Drawing.Size(32, 20);
            this.fitToolStripMenuItem.Text = "Fit";
            this.fitToolStripMenuItem.Click += new System.EventHandler(this.fitToolStripMenuItem_Click);
            // 
            // saveCalibrationToolStripMenuItem
            // 
            this.saveCalibrationToolStripMenuItem.Name = "saveCalibrationToolStripMenuItem";
            this.saveCalibrationToolStripMenuItem.Size = new System.Drawing.Size(104, 20);
            this.saveCalibrationToolStripMenuItem.Text = "Save Calibration";
            this.saveCalibrationToolStripMenuItem.Click += new System.EventHandler(this.saveCalibrationToolStripMenuItem_Click);
            // 
            // textBoxTubeSwitchTime
            // 
            this.textBoxTubeSwitchTime.Location = new System.Drawing.Point(508, 4);
            this.textBoxTubeSwitchTime.Name = "textBoxTubeSwitchTime";
            this.textBoxTubeSwitchTime.Size = new System.Drawing.Size(28, 20);
            this.textBoxTubeSwitchTime.TabIndex = 1;
            this.textBoxTubeSwitchTime.Text = "5";
            this.textBoxTubeSwitchTime.TextChanged += new System.EventHandler(this.textBoxTubeSwitchTime_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(401, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(101, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Tube switching time";
            // 
            // addDummySampleToolStripMenuItem
            // 
            this.addDummySampleToolStripMenuItem.Name = "addDummySampleToolStripMenuItem";
            this.addDummySampleToolStripMenuItem.Size = new System.Drawing.Size(129, 20);
            this.addDummySampleToolStripMenuItem.Text = "Add Dummy Sample";
            this.addDummySampleToolStripMenuItem.Click += new System.EventHandler(this.addDummySampleToolStripMenuItem_Click);
            // 
            // LaserCalibrator
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1614, 832);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBoxTubeSwitchTime);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "LaserCalibrator";
            this.Text = "Laser Calibrator";
            this.Load += new System.EventHandler(this.LaserCalibrator_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem addSampleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveCalibrationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fitToolStripMenuItem;
        private System.Windows.Forms.TextBox textBoxTubeSwitchTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolStripMenuItem addDummySampleToolStripMenuItem;
    }
}