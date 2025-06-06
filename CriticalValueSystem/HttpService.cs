using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ZL_BJCAAllinterface
{
    /// <summary>
    /// http连接基础类，负责底层的http通信
    /// </summary>
    public class HttpService
    {

        public static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            //直接确认，否则打不开    
            return true;
        }

        public static string Post(string req_data, string url, bool isUseCert)
        {
            System.GC.Collect();//垃圾回收，回收没有正常关闭的http连接

            string result = "";//返回结果

            HttpWebRequest request = null;
            HttpWebResponse response = null;
            Stream reqStream = null;

            //ClsPublic.WriteLog("【" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "】调用功能：" + url + "\r" + "入参：" + req_data);

            try
            {
                //设置最大连接数
                ServicePointManager.DefaultConnectionLimit = 200;
                //设置https验证方式
                if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    ServicePointManager.ServerCertificateValidationCallback =
                            new RemoteCertificateValidationCallback(CheckValidationResult);
                }

                /***************************************************************
                * 下面设置HttpWebRequest的相关属性
                * ************************************************************/
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                request = (HttpWebRequest)WebRequest.Create(url);

                request.Method = "POST";
                request.Timeout = 30000;

                //设置代理服务器
                //WebProxy proxy = new WebProxy();                          //定义一个网关对象
                //proxy.Address = new Uri(WxPayConfig.PROXY_URL);              //网关服务器端口:端口
                //request.Proxy = proxy;

                //设置POST的数据类型和长度
                request.ContentType = "application/json; charset=UTF-8";
                byte[] data = System.Text.Encoding.UTF8.GetBytes(req_data);
                request.ContentLength = data.Length;

                //是否使用证书
                //if (isUseCert)
                //{
                //    X509Certificate2 cert = new X509Certificate2(clsFunc.GetAppSetting("SLZF_ROOTCA_PATH"),clsFunc.GetAppSetting("MCH_ID"));
                //    request.ClientCertificates.Add(cert);
                //    clsFunc.WriteLog("交易说明：" + "Post used cert");

                //}

                //request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";
                //设置只支持tls1.2
                

                //往服务器写入数据
                reqStream = request.GetRequestStream();
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();

                //获取服务端返回
                response = (HttpWebResponse)request.GetResponse();

                //获取服务端返回数据
                StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                result = sr.ReadToEnd().Trim();
                sr.Close();
                //ClsPublic.WriteLog("【" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")  + "】出参：" + result);
            }
            catch (System.Threading.ThreadAbortException)
            {
                System.Threading.Thread.ResetAbort();
            }
            catch (WebException e)
            {
                return "{\"statusCode\":\"-1\",\"\":\"" + e.Message + "\"}";
                //if (e.Status == WebExceptionStatus.ProtocolError)
                //{
                //}
            }
            catch (Exception e)
            {
                return "{\"statusCode\":\"-1\",\"\":\"" + e.Message + "\"}";
            }
            finally
            {
                //关闭连接和流
                response?.Close();
                request?.Abort();
            }
            return result;
        }

    }
}
