namespace CriticalValueSystem
{
    partial class Form1
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
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.lstCriticalValues = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // lstCriticalValues
            // 
            this.lstCriticalValues.FormattingEnabled = true;
            this.lstCriticalValues.ItemHeight = 12;
            this.lstCriticalValues.Location = new System.Drawing.Point(1, 0);
            this.lstCriticalValues.Name = "lstCriticalValues";
            this.lstCriticalValues.Size = new System.Drawing.Size(757, 520);
            this.lstCriticalValues.TabIndex = 2;
            this.lstCriticalValues.DoubleClick += new System.EventHandler(this.lstCriticalValues_DoubleClick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(759, 522);
            this.Controls.Add(this.lstCriticalValues);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "危急值客户端";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox lstCriticalValues;
    }
}

