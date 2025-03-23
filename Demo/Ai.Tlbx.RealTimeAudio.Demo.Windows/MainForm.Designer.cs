namespace Ai.Tlbx.RealTimeAudio.Demo.Windows
{
    partial class MainForm
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
            btnTestMic = new Button();
            btnStart = new Button();
            btnInterrupt = new Button();
            btnEnd = new Button();
            lblTitle = new Label();
            lblStatus = new Label();
            txtTranscription = new TextBox();
            SuspendLayout();
            
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
            lblTitle.Location = new Point(12, 9);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(265, 25);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Windows Real-Time Audio Demo";
            
            // btnTestMic
            // 
            btnTestMic.Location = new Point(12, 49);
            btnTestMic.Name = "btnTestMic";
            btnTestMic.Size = new Size(120, 30);
            btnTestMic.TabIndex = 1;
            btnTestMic.Text = "Test Microphone";
            btnTestMic.UseVisualStyleBackColor = true;
            btnTestMic.Click += btnTestMic_Click;
            
            // btnStart
            // 
            btnStart.Location = new Point(138, 49);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(120, 30);
            btnStart.TabIndex = 2;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            
            // btnInterrupt
            // 
            btnInterrupt.Location = new Point(264, 49);
            btnInterrupt.Name = "btnInterrupt";
            btnInterrupt.Size = new Size(120, 30);
            btnInterrupt.TabIndex = 3;
            btnInterrupt.Text = "Interrupt";
            btnInterrupt.UseVisualStyleBackColor = true;
            btnInterrupt.Click += btnInterrupt_Click;
            
            // btnEnd
            // 
            btnEnd.Location = new Point(390, 49);
            btnEnd.Name = "btnEnd";
            btnEnd.Size = new Size(120, 30);
            btnEnd.TabIndex = 4;
            btnEnd.Text = "End";
            btnEnd.UseVisualStyleBackColor = true;
            btnEnd.Click += btnEnd_Click;
            
            // txtTranscription
            // 
            txtTranscription.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtTranscription.Location = new Point(12, 94);
            txtTranscription.Multiline = true;
            txtTranscription.Name = "txtTranscription";
            txtTranscription.ReadOnly = true;
            txtTranscription.ScrollBars = ScrollBars.Vertical;
            txtTranscription.Size = new Size(776, 305);
            txtTranscription.TabIndex = 5;
            
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Italic, GraphicsUnit.Point);
            lblStatus.Location = new Point(12, 419);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(273, 15);
            lblStatus.TabIndex = 6;
            lblStatus.Text = "Ready. Click \"Test Microphone\" to verify your audio input device.";
            
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblStatus);
            Controls.Add(txtTranscription);
            Controls.Add(btnEnd);
            Controls.Add(btnInterrupt);
            Controls.Add(btnStart);
            Controls.Add(btnTestMic);
            Controls.Add(lblTitle);
            Name = "MainForm";
            Text = "Windows Real-Time Audio Demo";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnTestMic;
        private Button btnStart;
        private Button btnInterrupt;
        private Button btnEnd;
        private Label lblTitle;
        private Label lblStatus;
        private TextBox txtTranscription;
    }
}
