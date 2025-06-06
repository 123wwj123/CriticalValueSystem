using System;
using System.Windows.Forms;

namespace CriticalValueSystem
{
    public partial class DownloadProgressForm : Form
    {
        public DownloadProgressForm()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int percentage)
        {
            if (this.InvokeRequired) // 检查是否需要跨线程调用
            {
                // 如果是，则使用 BeginInvoke 异步地在UI线程上执行此方法
                this.BeginInvoke(new Action(() => UpdateProgress(percentage)));
                return;
            }

            if (percentage < 0) percentage = 0; // 进度条最小值通常为0
            if (percentage > 100) percentage = 100; // 进度条最大值通常为100

            progressBarDownload.Value = percentage;
            labelPercentage.Text = $"{percentage}%";
        }

        public void SetStatus(string statusMessage)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetStatus(statusMessage)));
                return;
            }
            labelStatus.Text = statusMessage;
        }

         //可选: 如果需要，可以添加一个允许关闭的方法或取消按钮的逻辑
         public void AllowClose()
         {
             if (this.InvokeRequired)
             {
                 this.BeginInvoke(new Action(AllowClose));
                 return;
             }
             this.ControlBox = true; // 允许显示关闭按钮
         }
    }
}