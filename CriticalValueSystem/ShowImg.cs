using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json.Linq;
using ThoughtWorks.QRCode.Codec;
using System.Drawing.Drawing2D;

namespace ZL_BJCAAllinterface
{
    public partial class ShowImg : Form
    {
        public ShowImg()
        {
            InitializeComponent();
        }

        private static string mstrInfo = "";
        private static string mstrcode = "";
        private static int mintType = 0;
        private static string mstruserID = "";

        /// <summary>
        /// 展示二维码图片，获取扫码后的信息
        /// </summary>
        /// <param name="strauthCode">二维码内容</param>
        /// <param name="strcode">业务标识，激活用户时为激活码，开启自动签名时为 签名任务ID</param>
        /// <param name="intType">扫码类型，1、获取激活码，2、自动签名授权，3、获取签名结果</param>
        /// <param name="stropenID">用户标识</param>
        /// <param name="stropenID">用户标识</param>
        /// <returns></returns>
        public string GetQrCodeInfo(string strauthCode, string strcode, int intType, string struserID)
        {
            RenderQrCode(strauthCode);
            lblTime.Text = "150";
            lblCode.Text = "";
            if (intType == 1)
            { 
                lblCode.Text = "激活码：" + strcode;
            }
            mstrcode = strcode;
            mintType = intType;
            mstruserID = struserID;

            mstrInfo = "";

            tmrRun.Enabled = true;
            tmrTime.Enabled = true;
            this.ShowDialog() ;

            return mstrInfo;
        }

        /// <summary>
        /// 定时查询用户扫码结果，返回相关信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrRun_Tick(object sender, EventArgs e)
        {
            try
            {
                if (mintType == 1)
                {
                    //查询激活设备信息
                    if (!ClsCertFunc.GetUserDevice(mstruserID,out mstrInfo))
                    {
                        this.Dispose();
                        this.Close();
                        return;
                    }

                    if (!string.IsNullOrEmpty(mstrInfo))
                    {
                        this.Dispose();
                        this.Close();
                        return;
                    } 
                }
                else if (mintType == 2)
                {
                    if (!ClsCertFunc.GetSignResult(mstrcode, out mstrInfo))
                    {
                        MessageBox.Show(this, "开启自动签名失败，请重试！",ClsPublic.gstrBoxTitle,MessageBoxButtons.OK,MessageBoxIcon.Warning);
                        this.Dispose();
                        this.Close();
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(mstrInfo))
                    {
                       // mstrInfo = "1";
                        this.Dispose();
                        this.Close();
                        return;
                    }
                }
                else
                {
                    if (!ClsCertFunc.GetSignResult(mstrcode, out mstrInfo))
                    {
                        MessageBox.Show(this, "签名失败，请重试！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        this.Dispose();
                        this.Close();
                        return;
                    }

                    if (!string.IsNullOrEmpty(mstrInfo))
                    {
                        this.Dispose();
                        this.Close();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("程序运行异常，" + ex.Message,1);
                MessageBox.Show(this, "程序运行异常，" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                this.Dispose();
                this.Close();
            }
        }

        /// <summary>
        /// 倒计时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrTime_Tick(object sender, EventArgs e)
        {
            lblTime.Text = (Convert.ToInt16(lblTime.Text) - 1).ToString();
            if (lblTime.Text == "0")
            {
                mstrInfo = "";
                this.Dispose();
                this.Close();
            }
        }

        /// <summary>
        /// 生成二维码
        /// </summary>
        /// <param name="strQrCode"></param>
        private void RenderQrCode(string strQrCode)
        {
            QRCodeEncoder qrCodeEncoder = new QRCodeEncoder();
            qrCodeEncoder.QRCodeEncodeMode = QRCodeEncoder.ENCODE_MODE.BYTE;
            qrCodeEncoder.QRCodeVersion = 12;
            qrCodeEncoder.QRCodeScale = 3;

            picqrCode.Image = qrCodeEncoder.Encode(strQrCode, System.Text.Encoding.UTF8);
            //QRCodeGenerator.ECCLevel eccLevel = (QRCodeGenerator.ECCLevel)1;
            //using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            //{
            //    using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(strQrCode, eccLevel))
            //    {
            //        using (QRCode qrCode = new QRCode(qrCodeData))
            //        {
            //            Image img = qrCode.GetGraphic(20, Color.Black, Color.White, null, 0);
            //            Bitmap b = new Bitmap(picqrCode.Width, picqrCode.Height);
            //            Graphics g = Graphics.FromImage((System.Drawing.Image)b);
            //            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //            //绘制图像
            //            g.DrawImage(img, 0, 0, picqrCode.Width, picqrCode.Height);
            //            g.Dispose();
            //            picqrCode.Image = b;

            //        }
            //    }
            //}
        }

        private void ShowImg_Load(object sender, EventArgs e)
        {
            // 设置窗体显示在最上层
            IniFiles.SetWindowPos(this.Handle, -1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0080);
            // 设置本窗体为活动窗体
            IniFiles.SetActiveWindow(this.Handle);
            IniFiles.SetForegroundWindow(this.Handle);
            // 设置窗体置顶
            this.TopMost = true;
        }
    }
}