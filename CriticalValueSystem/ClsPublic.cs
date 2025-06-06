using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Collections;
using System.Security.Cryptography;
using System.IO;
using System.Configuration;
using System.Reflection;
using System.Xml;
using Oracle.ManagedDataAccess.Client;
using System.Windows;
using System.Windows.Forms;

namespace ZL_BJCAAllinterface
{
    public class ClsPublic
    {
        public static Boolean gblnAuto = false;   //是否开启自动签名
        public static int gintUserID = 0;         //人员ID
        public static string gstrCertSN = "";     //证书SN
        public static string gstrCertKey = "";    //记录原始证书SN，电子病历使用
        public static string gstrSignCert = "";   //当前使用的证书内容
        public static string gstrIDCard = "";      //操作员身份证号
        public static Boolean gblnReload = false;  //是否需要重新登录
        public static DateTime gdtExemptTime = System.DateTime.Now;     //免验证签名验证时间，用来判断免验证签名是否还有效
        public static string gstrBoxTitle = GetAppSetting("BoxTitle");  //弹框提示的标题
        
        //数据库连接
        public static OracleConnection gOraConn = new OracleConnection(GetAppSetting("Conn"));

        /// <summary>
        /// 获取配置文件中指定参数的值
        /// </summary>
        /// <param name="strKey">指定参数名称</param>
        /// <returns></returns>
        public static string GetAppSetting(string strKey)
        {
            //获取配置文件路径
            Configuration dllConfig = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            //获取配置文件内容
            AppSettingsSection settings = (AppSettingsSection)dllConfig.GetSection("appSettings");
            //获取指定key的值
            string strValue = settings.Settings[strKey].Value;
            return strValue;
        }

        /// <summary>
        /// 将指定PDF文件转换成Base64串
        /// </summary>
        /// <param name="strFile">PDF文件</param>
        /// <returns></returns>
        public static string PdfToBase64(string strFile)
        {
            //直接读取文件，异常返回空值
            try
            {
                // 读取PDF文件内容到字节数组
                byte[] fileBytes = File.ReadAllBytes(strFile);

                // 将字节数组转换为Base64字符串
                string base64String = Convert.ToBase64String(fileBytes);

                return base64String;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// 字符串BASE64编码
        /// </summary>
        /// <param name="strIn">被编码的字符串</param>
        /// <returns></returns>
        public static string StringToBase64(string strIn)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(strIn);
            string base64String = System.Convert.ToBase64String(bytes);
            return base64String;
        }

        /// <summary>
        /// 获取执行XML串中的节点值
        /// </summary>
        /// <param name="strXml">XML串</param>
        /// <param name="strNode">指定路径</param>
        /// <returns></returns>
        public static string GetXmlNode(string strXml,string strNode)
        {
            XmlDocument xd = new XmlDocument();
            try
            {
                xd.LoadXml(strXml);
                return xd.SelectNodes(strNode).Item(0).InnerText;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// 向系统的交易日志中写一条新的日志记录。
        /// </summary>
        /// <param name="strInfo">日志的内容。</param>
        /// <param name="intLever">日志级别</param>
        public static void WriteLog(string strInfo, Int16 intLever = 3)
        {
            try
            {
                string strPath = Nvl(GetAppSetting("LogPath"), "C:\\Appsoft\\Calog\\");
                if (!System.IO.Directory.Exists(strPath))
                {
                    System.IO.Directory.CreateDirectory(strPath);
                }
                if (intLever <= Convert.ToInt16(GetAppSetting("LogLever")))
                {
                    StreamWriter sw = new StreamWriter(strPath + "//" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", true);
                    sw.WriteLine("【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "】" + strInfo);
                    sw.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 判断关键字1是否为空，为空时返回关键字2，否则返回关键字1
        /// </summary>
        /// <param name="strKey1">关键字1</param>
        /// <param name="strKey2">关键字2</param>
        /// <returns></returns>
        public static string Nvl(string strKey1, string strKey2)
        {
            if (string.IsNullOrEmpty(strKey1))
            {
                return strKey2;
            }
            else
            {
                return strKey1;
            }
        }

        /// <summary>
        /// 时间格式初始化,格式为yyyy-MM-dd HH:mm:ss
        /// </summary>
        /// <param name="time">格式为yyyyMMddHHmmss</param>
        /// <returns></returns>
        public static string FormatTime(string time)
        {
            //MessageBox.Show("时间:" + time);
            string result;
            try
            {
                string year = time.Substring(0, 4);
                string month = time.Substring(4, 2);
                string day = time.Substring(6, 2);
                string hour = time.Substring(8, 2);
                string min = time.Substring(10, 2);
                string second = time.Substring(12, 2);
                result = string.Format("{0}-{1}-{2} {3}:{4}:{5}", year, month, day, hour, min, second);
            }
            catch (Exception ex)
            {
                throw (ex);
            }
            return result;
        }

        /// <summary>
        /// 计算时间差
        /// </summary>
        /// <param name="dateStart"></param>
        /// <param name="dateEnd"></param>
        /// <returns></returns>
        public static int DateDiff(DateTime dateStart, DateTime dateEnd)
        {
            DateTime start = Convert.ToDateTime(dateStart.ToShortDateString());
            DateTime end = Convert.ToDateTime(dateEnd.ToShortDateString());
            TimeSpan sp = end.Subtract(start);
            return sp.Days;
        }

        /// <summary>
        /// 32位MD5加密
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string MD5Encrypt32(string password)
        {
            string cl = password;
            string pwd = "";
            MD5 md5 = MD5.Create(); //实例化一个md5对像
                                    // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
            byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(cl));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符 
                pwd += s[i].ToString("X");
            }
            return pwd;
        }
    }
}
