namespace ZL_BJCAAllinterface
{
    partial class ShowImg
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.picqrCode = new System.Windows.Forms.PictureBox();
            this.tmrRun = new System.Windows.Forms.Timer(this.components);
            this.lblTime = new System.Windows.Forms.Label();
            this.tmrTime = new System.Windows.Forms.Timer(this.components);
            this.lblCode = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picqrCode)).BeginInit();
            this.SuspendLayout();
            // 
            // picqrCode
            // 
            this.picqrCode.Location = new System.Drawing.Point(36, 77);
            this.picqrCode.Margin = new System.Windows.Forms.Padding(4);
            this.picqrCode.Name = "picqrCode";
            this.picqrCode.Size = new System.Drawing.Size(300, 300);
            this.picqrCode.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picqrCode.TabIndex = 0;
            this.picqrCode.TabStop = false;
            // 
            // tmrRun
            // 
            this.tmrRun.Interval = 3000;
            this.tmrRun.Tick += new System.EventHandler(this.tmrRun_Tick);
            // 
            // lblTime
            // 
            this.lblTime.AutoSize = true;
            this.lblTime.Font = new System.Drawing.Font("宋体", 26.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTime.ForeColor = System.Drawing.Color.Red;
            this.lblTime.Location = new System.Drawing.Point(120, 9);
            this.lblTime.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTime.Name = "lblTime";
            this.lblTime.Size = new System.Drawing.Size(85, 44);
            this.lblTime.TabIndex = 1;
            this.lblTime.Text = "150";
            // 
            // tmrTime
            // 
            this.tmrTime.Interval = 1000;
            this.tmrTime.Tick += new System.EventHandler(this.tmrTime_Tick);
            // 
            // lblCode
            // 
            this.lblCode.AutoSize = true;
            this.lblCode.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCode.Location = new System.Drawing.Point(70, 381);
            this.lblCode.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCode.Name = "lblCode";
            this.lblCode.Size = new System.Drawing.Size(87, 25);
            this.lblCode.TabIndex = 2;
            this.lblCode.Text = "激活码";
            // 
            // ShowImg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(334, 379);
            this.Controls.Add(this.lblCode);
            this.Controls.Add(this.lblTime);
            this.Controls.Add(this.picqrCode);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShowImg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "请使用协同签名APP扫码";
            this.Load += new System.EventHandler(this.ShowImg_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picqrCode)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox picqrCode;
        private System.Windows.Forms.Timer tmrRun;
        private System.Windows.Forms.Label lblTime;
        private System.Windows.Forms.Timer tmrTime;
        private System.Windows.Forms.Label lblCode;
    }
}