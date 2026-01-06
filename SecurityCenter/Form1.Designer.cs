namespace SecurityCenter
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pbMap = new PictureBox();
            lstEvents = new ListBox();
            splitContainer1 = new SplitContainer();
            ((System.ComponentModel.ISupportInitialize)pbMap).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // pbMap
            // 
            pbMap.BorderStyle = BorderStyle.FixedSingle;
            pbMap.Dock = DockStyle.Fill;
            pbMap.Location = new Point(0, 0);
            pbMap.Name = "pbMap";
            pbMap.Size = new Size(547, 450);
            pbMap.SizeMode = PictureBoxSizeMode.Zoom;
            pbMap.TabIndex = 0;
            pbMap.TabStop = false;
            pbMap.Click += pbMap_Click;
            // 
            // lstEvents
            // 
            lstEvents.Dock = DockStyle.Fill;
            lstEvents.FormattingEnabled = true;
            lstEvents.ItemHeight = 15;
            lstEvents.Location = new Point(0, 0);
            lstEvents.Name = "lstEvents";
            lstEvents.Size = new Size(249, 450);
            lstEvents.TabIndex = 1;
            lstEvents.SelectedIndexChanged += lstEvents_SelectedIndexChanged;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(pbMap);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(lstEvents);
            splitContainer1.Size = new Size(800, 450);
            splitContainer1.SplitterDistance = 547;
            splitContainer1.TabIndex = 2;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(splitContainer1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)pbMap).EndInit();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private PictureBox pbMap;
        private ListBox lstEvents;
        private SplitContainer splitContainer1;
    }
}
