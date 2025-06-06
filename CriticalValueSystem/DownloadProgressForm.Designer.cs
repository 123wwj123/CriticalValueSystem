namespace CriticalValueSystem
{
    partial class DownloadProgressForm
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
            this.progressBarDownload = new System.Windows.Forms.ProgressBar();
            this.labelPercentage = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // progressBarDownload
            // 
            this.progressBarDownload.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBarDownload.Location = new System.Drawing.Point(0, 0);
            this.progressBarDownload.Name = "progressBarDownload";
            this.progressBarDownload.Size = new System.Drawing.Size(534, 23);
            this.progressBarDownload.TabIndex = 0;
            // 
            // labelPercentage
            // 
            this.labelPercentage.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelPercentage.Location = new System.Drawing.Point(0, 23);
            this.labelPercentage.Name = "labelPercentage";
            this.labelPercentage.Size = new System.Drawing.Size(534, 23);
            this.labelPercentage.TabIndex = 1;
            this.labelPercentage.Text = "0%";
            this.labelPercentage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelStatus.Location = new System.Drawing.Point(0, 46);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(83, 12);
            this.labelStatus.TabIndex = 2;
            this.labelStatus.Text = "正在初始化...";
            // 
            // DownloadProgressForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(534, 64);
            this.ControlBox = false;
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.labelPercentage);
            this.Controls.Add(this.progressBarDownload);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "DownloadProgressForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "正在下载更新";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBarDownload;
        private System.Windows.Forms.Label labelPercentage;
        private System.Windows.Forms.Label labelStatus;
    }
}