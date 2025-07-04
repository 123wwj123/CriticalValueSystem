//using GetKeyPicLib;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ZL_BJCAAllinterface
{
    /// <summary>
    /// 读取证书信息
    /// </summary>
    public class ClsCertFunc
    {
        //局部变量
        //CA系统相关参数
        static string mstrUrl = ClsPublic.GetAppSetting("Url");  //服务地址
        static string mstrAppID = ClsPublic.GetAppSetting("AppID");  //客户端ID
        static string mstrKey = ClsPublic.GetAppSetting("Key");        //秘钥
        static Int32 minttimeRegion = Convert.ToInt32(ClsPublic.GetAppSetting("TimeRegion"));
        static Int16 mintexpiryDate = Convert.ToInt16(ClsPublic.GetAppSetting("ExpiryDate"));

        static string mstrHead = "{\"version\":\"1.0\",\"appId\":\"" + mstrAppID + "\",\"signAlgo\":\"HMAC-SHA256\"}";

        //// 创建DSVS对象
        //public static BJCA_SVS_CLIENTCOMLib.BJCASVSEngine svs = new BJCA_SVS_CLIENTCOMLib.BJCASVSEngine();
        //// 创建TS对象
        //public static BJCA_TS_CLIENTCOMLib.BJCATSEngine ts = new BJCA_TS_CLIENTCOMLib.BJCATSEngine();
        ////创建签名对象
        //public static XTXAppCOMLib.XTXApp xtxapp = new XTXAppCOMLib.XTXApp();

        //首次提示标识
        public static bool blnShow = false;

        //// 创建获取签章图片对象         
        //public static GetPic pic = new GetPic();

        #region APP证书业务
        /// <summary>
        /// 查询操作员APP证书信息
        /// </summary>
        /// <param name="IDCard">操作员身份证号，必填</param>
        /// <param name="CertCN">使用者姓名</param>
        /// <param name="CertDN">证书主题</param>
        /// <param name="CertSN">证书唯一序列号</param>
        /// <param name="EncCert">加密证书内容</param>
        /// <returns></returns>
        public static Boolean QueryUserInfo(string IDCard,out string CertCN,out string CertDN,out string CertSN,out string EncCert)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            CertCN = "";
            CertDN = "";
            CertSN = "";
            EncCert = "";

            try
            {
                ClsPublic.WriteLog("根据身份证号查询当前医护人员的CA证书信息", 3);
                objJson = JObject.Parse(mstrHead);
                //objJson.Add("idNumber", IDCard);  //身份证号
                //objJson.Add("idType", "SF");  //身份证类型
                objJson.Add("userId", IDCard);  //userId
                objJson.Add("signature", GetOutSign(objJson));  //签名

                //查询证书信息
                ClsPublic.WriteLog("查询CA证书信息服务地址：" + mstrUrl + "queryUserInfo  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "queryUserInfo", false);
                ClsPublic.WriteLog("查询CA证书信息返回结果", 3);
                ClsPublic.WriteLog("查询CA证书出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("CA证书信息查询成功，拼接需要的信息", 3);
                    //签章信息： 主键|姓名|部门|证件类型|证件号码|手机|固定电话|邮件|工号|人脸ID|签章ID|用户ID|状态|创建时间
                    EncCert = objJson["data"]["id"].Value<string>() + "|" + objJson["data"]["username"].Value<string>() + "|" + objJson["data"]["department"].Value<string>() + "|" +
                                objJson["data"]["idType"].Value<string>() + "|" + objJson["data"]["idCard"].Value<string>() + "|" + objJson["data"]["mobilePhone"].Value<string>() + " |" + "|" +
                                objJson["data"]["email"].Value<string>() + "|" + objJson["data"]["jobNumber"].Value<string>() + "|" + objJson["data"]["faceId"].Value<string>() + "|" + objJson["data"]["signpicId"].Value<string>() + "|" +
                                objJson["data"]["msspId"].Value<string>() + "|" + objJson["data"]["status"].Value<string>() + "|" + objJson["data"]["createTime"].Value<string>();
                    ClsPublic.WriteLog("拼接的CA证书签章信息：" + EncCert, 4);
                    CertCN = EncCert.Split('|')[8];
                    CertDN = EncCert.Split('|')[1];
                    CertSN = EncCert.Split('|')[11];
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("证书信息查询失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("查询当前人员CA注册信息失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("证书信息查询异常。" + ex.Message, 1);
                MessageBox.Show("查询当前人员CA注册信息异常，" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }
        
        /// <summary>
        /// 产生激活码
        /// </summary>
        /// <param name="CertSN">用户ID</param>
        /// <param name="AuthCode">二维码，用于手机扫码激活</param>
        /// <param name="Code">激活码，用户手机输入激活</param>
        /// <returns></returns>
        public static Boolean GetAuthCode(string CertSN, out string AuthCode, out string Code)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            AuthCode = "";
            Code = "";

            try
            {
                ClsPublic.WriteLog("获取激活验证码，激活用户手机APP端", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", CertSN);   //用户ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                //获取激活二维码
                ClsPublic.WriteLog("获取激活码服务地址；" + mstrUrl + "getAuthCode  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "getAuthCode", false);
                ClsPublic.WriteLog("获取激活码返回结果", 3);
                ClsPublic.WriteLog("获取激活码码出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("激活码获取成功，展码给操作员扫码验证", 3);
                    AuthCode = objJson["data"]["authCode"].Value<string>();
                    Code = objJson["data"]["code"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("生成激活码失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("生成激活码失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("生成激活码异常。" + ex.Message, 1);
                MessageBox.Show("生成激活码异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 获取激活设备信息
        /// </summary>
        /// <param name="UserID">用户ID</param>
        /// <param name="DeviceInfo">返回的设备信息，设备ID|设备名称|设备注册时间</param>
        /// <returns></returns>
        public static Boolean GetUserDevice(string UserID,out string DeviceInfo)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            DeviceInfo = "";

            try
            {
                ClsPublic.WriteLog("查询激活设备信息",3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", UserID);   //用户ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("获取激活设备信息服务地址；" + mstrUrl + "getUserDevice  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "getUserDevice", false);
                ClsPublic.WriteLog("获取激活设备信息成功", 3);
                ClsPublic.WriteLog("获取激活设备信息出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    DeviceInfo = objJson["data"]["deviceInfos"][0]["deviceId"].Value<string>();  //设备ID
                    DeviceInfo += "|" + objJson["data"]["deviceInfos"][0]["deviceName"].Value<string>(); //设备名称
                    DeviceInfo += "|" + objJson["data"]["deviceInfos"][0]["reqCertTime"].Value<string>();//设备注册时间
                    blnResult = true;
                }
                else if (objJson["status"].Value<string>() != "89003049")
                {
                    ClsPublic.WriteLog("获取激活设备信息失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("获取激活设备信息失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    ClsPublic.WriteLog("设备正在激活，请稍等......", 3);
                    blnResult = true;
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("获取激活设备信息异常。" + ex.Message, 1);
                MessageBox.Show("获取激活设备信息异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 查询用户证书内容
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="deviceId">设备ID</param>
        /// <param name="SignCert">证书内容</param>
        /// <returns></returns>
        public static Boolean QueryCert(string userId, string deviceId, out string SignCert)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            SignCert = "";

            try
            {
                ClsPublic.WriteLog("查询用户证书内容", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", userId);   //用户ID
                objJson.Add("deviceId", deviceId);   //设备ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("获取证书内容地址：" + mstrUrl + "queryCert  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "queryCert", false);
                ClsPublic.WriteLog("获取证书返回结果", 3);
                ClsPublic.WriteLog("获取证书内容出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("证书内容获取成功", 3);
                    SignCert = objJson["data"]["certInfos"][0]["cert"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("证书内容获失败，" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("获取签名证书信息失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("证书内容获异常，" + ex.Message, 1);
                MessageBox.Show("获取签名证书信息异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 查询证书签名图片
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="SignPic">图片内容BASE64串</param>
        /// <returns></returns>
        public static Boolean QueryImage(string userId, out string SignPic)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            SignPic = "";

            try
            {
                ClsPublic.WriteLog("获取当前医护人员签名图片信息", 3);
                //MessageBox.Show("请在【协同签名】APP中修改手写签名，如已经修改完成，请点击【确定】按钮！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", userId);   //用户ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("获取签名图片服务地址：" + mstrUrl + "queryImage 入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "queryImage", false);
                ClsPublic.WriteLog("获取签名图片返回结果", 3);
                ClsPublic.WriteLog("获取签名图片出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("获取签名图片成功", 3);
                    SignPic = objJson["data"]["image"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("获取签名图片失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("获取签名印章图片失败。", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("获取证书签名图片异常，" + ex.Message, 1);
                MessageBox.Show("获取证书签名图片异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 开启APP证书时效签
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="signDataId">签名任务ID</param>
        /// <param name="qrCode">签名二维码</param>
        /// <returns></returns>
        public static Boolean StartAutoSign(string userId, out string signDataId, out string qrCode)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            signDataId = "";
            qrCode = "";

            try
            {
                ClsPublic.WriteLog("开启时效签",3);
                objJson = JObject.Parse(mstrHead);
                //objJson.Add("userId", userId);   //用户ID
                objJson.Add("timeRegion", minttimeRegion);   //自动签名的过期时间，单位为秒 （最长86400秒，即24小时）
                objJson.Add("requireQrCode", "N");   //用户ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                //开启时效签
                ClsPublic.WriteLog("开启时效签服务地址：" + mstrUrl + "startAutoSign  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "startAutoSign", false);
                ClsPublic.WriteLog("开启时效签返回结果", 3);
                ClsPublic.WriteLog("开启时效签出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("开启时效签成功", 3);
                    qrCode = objJson["data"]["qrCode"].Value<string>();
                    signDataId = objJson["data"]["signDataId"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("开启时效签失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("开启时效签失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) 
            {
                ClsPublic.WriteLog("开启时效签异常。" + ex.Message, 1);
                MessageBox.Show("开启时效签异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 时效签名
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="title">标题</param>
        /// <param name="strSource">签名原文</param>
        /// <param name="SignResult">签名结果</param>
        /// <param name="SignCert">签名使用的证书内容</param>
        /// <returns></returns>
        public static Boolean AutoSign(string userId,string title,string strSource,out string SignResult,out string SignCert)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            SignResult = "";
            SignCert = "";

            try
            {
                ClsPublic.WriteLog("进行时效签名", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", userId);   //用户ID
                objJson.Add("title", title);   //标题
                objJson.Add("dataType", "DATA");   //原文类型（DATA：原文，HASH：hash 数据 WEB_SEAL：网页签章）
                objJson.Add("algo", "SM3withSM2");   //签名算法(SM3withSM2)
                objJson.Add("data", Convert.ToBase64String(Encoding.UTF8.GetBytes(strSource)));   //待签数据（ 必须是  base64 编码）
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("时效签名服务地址：" + mstrUrl + "autoSign  请求入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "autoSign", false);
                ClsPublic.WriteLog("CA服务器返回签名结果", 3);
                ClsPublic.WriteLog("签名结果出参：" + strInfo, 4);

                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("时效签名成功", 3);
                    SignResult = objJson["data"]["signResult"].Value<string>();
                    SignCert = objJson["data"]["signCert"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("时效签名失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("时效签名失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("时效签名异常。" + ex.Message, 1);
                MessageBox.Show("时效签名异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 添加签名任务，需要APP端确认签名
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="title">标题</param>
        /// <param name="strSource">签名原文</param>
        /// <param name="signDataId">签名任务ID</param>
        /// <param name="qrCode">任务二维码</param>
        /// <returns></returns>
        public static Boolean AddSignJob(string userId,string title,string strSource,out string signDataId,out string qrCode)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            signDataId = "";
            qrCode = "";

            try
            {
                ClsPublic.WriteLog("添加签名任务", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("userId", userId);   //用户ID
                objJson.Add("title", title);   //标题
                objJson.Add("dataType", "DATA");   //原文类型（DATA：原文，HASH：hash 数据 WEB_SEAL：网页签章）
                objJson.Add("algo", "SM3withSM2");   //签名算法(SM3withSM2)
                objJson.Add("expiryDate", mintexpiryDate);   //签名任务有效期（截止时间，单位为 分钟）， 不能大于 4320 分钟，即3 天有效期（该字段为空则默认有效期为1 天）
                objJson.Add("data", Convert.ToBase64String(Encoding.UTF8.GetBytes(strSource)));   //待签数据（ 必须是  base64 编码）
                objJson.Add("requireQrCode", "N");   //是否返回二维码（ 只能传入 Y 、 N  两种类型, , 如果 传Y，会将二维码图片 base64 编码后返回）
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("签名服务地址：" + mstrUrl + "addSignJob  入参：" + objJson.ToString(), 4);

                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "addSignJob", false);
                ClsPublic.WriteLog("CA服务器返回签名任务请求结果", 3);
                ClsPublic.WriteLog("签名请求出参：" + strInfo, 4);

                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("添加签名任务成功", 3);
                    qrCode = objJson["data"]["qrCode"].Value<string>();
                    signDataId = objJson["data"]["signDataId"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("添加签名任务失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("添加签名任务失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("添加签名任务异常。" + ex.Message, 1);
                MessageBox.Show("添加签名任务异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return blnResult;
        }

        /// <summary>
        /// 获取签名结果
        /// </summary>
        /// <param name="signDataId">签名任务ID</param>
        /// <param name="signResult">签名结果BASE64串[;]签名使用的证书内容BASE64串</param>
        /// <returns></returns>
        public static Boolean GetSignResult(string signDataId, out string signResult)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            signResult = "";

            try
            {
                ClsPublic.WriteLog("查询签名结果",3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("signDataId", signDataId);   //签名任务ID
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("获取签名结果服务地址；" + mstrUrl + "getSignResult   入参：" + objJson.ToString(),4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "getSignResult", false);
                ClsPublic.WriteLog("获取签名结果成功", 3);
                ClsPublic.WriteLog("获取签名结果出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    if (objJson["data"]["jobStatus"].Value<string>() == "FINISH")
                    {
                        //signResult = objJson["data"]["signResult"].Value<string>();
                        //signResult += "[;]" + objJson["data"]["signCert"].Value<string>();
                        signResult = objJson["data"]["msspId"].Value<string>();
                        blnResult = true;
                    }
                    else if (objJson["data"]["jobStatus"].Value<string>() == "UNSIGN")
                    {
                        ClsPublic.WriteLog("等待用户签名确认......",3);
                        blnResult = true;
                    }
                    else
                    {
                        ClsPublic.WriteLog("获取签名结果成功，签名失败。" + objJson["message"].Value<string>(), 1);
                        MessageBox.Show("获取签名结果成功，签名失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    ClsPublic.WriteLog("获取签名结果失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("获取签名结果失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("获取签名结果异常。" + ex.Message, 1);
                MessageBox.Show("获取签名结果异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 生成时间戳
        /// </summary>
        /// <param name="strSource">签名原文</param>
        /// <param name="TimeStampCode">时间戳内容</param>
        /// <returns></returns>
        public static Boolean CreateAndGetTssInfo(string strSource,out string TimeStampCode)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            TimeStampCode = "";

            try
            {
                ClsPublic.WriteLog("产生签名时间戳", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("oriData", Convert.ToBase64String(Encoding.UTF8.GetBytes(strSource)));   //签名原文
                objJson.Add("attachCert", "true");   //最 终 产 生 的 时 间 戳 是 否 带 证 书
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("时间戳服务地址：" + mstrUrl + "createAndGetTssInfo  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "createAndGetTssInfo", false);
                ClsPublic.WriteLog("服务器返回任务结果", 3);
                ClsPublic.WriteLog("任务请求出参：" + strInfo, 4);

                objJson = JObject.Parse(strInfo);

                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("生成签名时间戳成功", 3);
                    TimeStampCode = objJson["data"]["tsResp"].Value<string>();
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("生成时间戳失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("生成时间戳失败，" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("生成时间戳异常。" + ex.Message, 1);
                MessageBox.Show("生成时间戳异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 解析时间戳
        /// </summary>
        /// <param name="tsResp">时间戳原文</param>
        /// <param name="TimeStamp">时间</param>
        /// <returns></returns>
        public static Boolean GetTSInfo(string tsResp,out string TimeStamp)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            TimeStamp = "";

            try
            {
                ClsPublic.WriteLog("获取时间戳内容", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("tsResp", tsResp); //时间戳内容
                objJson.Add("type", "1");         //解析时间戳类型（type = 1 返回时间，type＝2 返回原文的 hash 值，type＝3 返回证书，type = 4返回服务器证书序列号）
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("获取时间戳内容服务地址：" + mstrUrl + "getTSInfo  入参：" + objJson.ToString(), 4);

                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "getTSInfo", false);
                ClsPublic.WriteLog("获取时间戳内容出参：" + strInfo, 4);
                objJson = JObject.Parse(strInfo);

                if (objJson["status"].Value<string>() == "200")
                {
                    TimeStamp = objJson["data"]["tsInfo"].Value<string>();
                    ClsPublic.WriteLog("获取时间戳内容成功，时间戳内容：" + TimeStamp, 3);
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("获取时间戳内容失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("获取时间戳内容失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("获取时间戳内容异常。" + ex.Message, 1);
                MessageBox.Show("获取时间戳内容异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="strSource">签名原文</param>
        /// <param name="signValue">签名结果</param>
        /// <param name="cert">签名证书</param>
        /// <returns></returns>
        public static Boolean VerifySign(string strSource, string signValue,string cert)
        {
            JObject objJson;
            string strInfo;
            Boolean blnResult = false;

            try
            {
                ClsPublic.WriteLog("进行CA签名验签", 3);
                objJson = JObject.Parse(mstrHead);
                objJson.Add("signAlg", "SM3withSM2");   //签名算法应为 SM3withSM2
                objJson.Add("plain", Convert.ToBase64String(Encoding.UTF8.GetBytes(strSource)));   //签名原文数据base64
                objJson.Add("signValue", signValue);   //签名值 base64
                objJson.Add("cert", cert);   //证书
                objJson.Add("signature", GetOutSign(objJson));  //签名

                ClsPublic.WriteLog("CA验签服务地址：" + mstrUrl + "verifySign  入参：" + objJson.ToString(), 4);
                strInfo = HttpService.Post(objJson.ToString(), mstrUrl + "verifySign", false);
                ClsPublic.WriteLog("CA验签返回结果", 3);
                ClsPublic.WriteLog("CA验签出参：" + strInfo, 4);

                objJson = JObject.Parse(strInfo);
                if (objJson["status"].Value<string>() == "200")
                {
                    ClsPublic.WriteLog("CA验签成功", 3);
                    MessageBox.Show("验证签名成功，该电子签名数据有效！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    blnResult = true;
                }
                else
                {
                    ClsPublic.WriteLog("CA验签失败。" + objJson["message"].Value<string>(), 1);
                    MessageBox.Show("验证签名失败。" + objJson["message"].Value<string>(), ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ClsPublic.WriteLog("CA验签异常。" + ex.Message, 1);
                MessageBox.Show("CA验签异常。" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return blnResult;
        }

        /// <summary>
        /// 获取报文的签名值
        /// 签名方法：第一步：设所有发送或者接收到的数据为集合 M，将集合 M 内非空参数值的参数按照参数
        ///                   名 ASCII 码 从 小 到 大 排 序 （ 字 典 序 ） ， 使 用 URL 键 值 对 的 格 式 （ 即
        ///                    key1 = value1 & key2 = value2…）拼接成字符串 stringA。
        ///           第二步：对 stringA 对进行 HMAC 运算，得到 hmac 的值后再做 Base64 编码，最后得到
        ///                   签名值 signature。
        /// </summary>
        /// <param name="strIn"></param>
        /// <returns></returns>
        public static string GetOutSign(JObject strIn)
        {
            //定义变量
            string strSign = "";
            //加载Json，遍历Json，只取参数值不为空
            ArrayList aryItem = new ArrayList();

            foreach (JProperty jPro in strIn.Properties())
            {
                if (!string.IsNullOrEmpty(jPro.Value.ToString()) && jPro.Name != "signature")
                    aryItem.Add(jPro.Name);
            }

            //按照参数名排序，并进行MD5加密
            aryItem.Sort();
            for (int intX = 0; intX < aryItem.Count; intX++)
            {
                strSign = strSign + aryItem[intX].ToString() + "=" + strIn[aryItem[intX].ToString()].Value<string>() + "&";
            }
            using (HMACSHA256 mac = new HMACSHA256(Encoding.UTF8.GetBytes(mstrKey)))
            {
                byte[] hash = mac.ComputeHash(Encoding.UTF8.GetBytes(strSign.Substring(0, strSign.Length - 1)));
                strSign = Convert.ToBase64String(hash);
            }
            return strSign;
        }
        #endregion

        #region KEY证书业务

        /// <summary>
        /// 获取KEY客户端证书信息
        /// </summary>
        /// <param name="IDCard">身份证号</param>
        /// <param name="CertCN">使用者姓名</param>
        /// <param name="CertDN"></param>
        /// <param name="CertSN">证书序列号</param>
        /// <param name="SignCert">签名证书内容</param>
        /// <param name="EncCert">加密证书内容</param>
        /// <param name="SignPic">签名图片</param>
        /// <returns></returns>
        //public static bool GetKeyInfo(string IDCard,ref string CertCN, ref string CertDN, ref string CertSN, ref string SignCert, ref string EncCert, ref string SignPic)
        //{
        //    string strCertSN;
        //    //获取插入的KEY中和当前人员身份证号一致的key
        //    //字典，保存插入usbKey用户名和证书唯一标识
        //    ClsPublic.WriteLog("获取插入的KEY证书信息", 3);
        //    Dictionary<string, string> dic = new Dictionary<string, string>();
        //    try
        //    {
        //        //获取所有key信息
        //        ClsPublic.WriteLog("获取KEY列表", 3);
        //        string strUserList = xtxapp.SOF_GetUserList(); //宿迁人民四(测试)||999000100089956/6001201312021788&&&宿迁人民二(测试)||999000100089948/6002201309019595&&&
        //        ClsPublic.WriteLog("列表信息：" + strUserList, 4);
        //        if (string.IsNullOrEmpty(strUserList))
        //        {
        //            //没有插入KEY
        //            return false;
        //        }

        //        string[] strTmpList = strUserList.Split(new String[] { "||", "&&&" }, StringSplitOptions.RemoveEmptyEntries);
        //        int lenth = strTmpList.Length;
        //        for (int i = 0; i < lenth; i += 2)
        //        {
        //            /*将插入key的用户的证书唯一标识进行保存*/
        //            //KEY-用户名 Value-证书唯一标识
        //            dic.Add(strTmpList[i], strTmpList[i + 1]);
        //        }
        //        ClsPublic.WriteLog("逐个检查KEY信息是否匹配", 3);
        //        DataTable dtCombin = new DataTable();
        //        dtCombin.Columns.Add("USER", typeof(String));
        //        dtCombin.Columns.Add("ID", typeof(String));
        //        foreach (string strUser in dic.Keys)
        //        {
        //            ClsPublic.WriteLog("获取KEY证书信息", 3);
        //            SignCert = xtxapp.SOF_ExportUserCert(dic[strUser]);//导出用户证书
        //            ClsPublic.WriteLog("证书内容：" + SignCert, 4);
        //            //获取注册证书的身份证号
        //            string id = xtxapp.SOF_GetCertInfoByOid(SignCert, "2.16.840.1.113732.2");
        //            ClsPublic.WriteLog("证书ID：" + id, 4);
        //            if (id == null || id == "")
        //            {
        //                id = xtxapp.SOF_GetCertInfoByOid(SignCert, "1.2.156.112562.2.1.1.1");
        //            }
        //            if (id.Substring(0, 2) == "SF")
        //                id = id.Substring(2);
        //            if (IDCard == id)
        //            {
        //                DataRow dr = dtCombin.NewRow();
        //                dr["USER"] = strUser;
        //                dr["ID"] = dic[strUser];
        //                dtCombin.Rows.Add(dr);
        //            }
        //        }
        //        ClsPublic.WriteLog("证书信息获取完毕，校验KEY", 3);
        //        if (dtCombin.Rows.Count > 1)
        //        {
        //            SelectUser userForm = new SelectUser
        //            {
        //                Dt = dtCombin
        //            };
        //            if (userForm.ShowDialog() == DialogResult.OK)
        //            {
        //                EncCert = dic[userForm.User];
        //                CertCN = userForm.User;
        //            }
        //            else
        //            {
        //                return false;
        //            }
        //        }
        //        else if (dtCombin.Rows.Count == 1)
        //        {
        //            EncCert = dtCombin.Rows[0][1].ToString();
        //            CertCN = dtCombin.Rows[0][0].ToString();
        //        }
        //        else
        //        {
        //            ClsPublic.WriteLog("没有获取到当前操作员的KEY证书信息",1);
        //            MessageBox.Show("请检查您的证书是否已经插入，没有检测到您的KEY证书！",ClsPublic.gstrBoxTitle,MessageBoxButtons.OK,MessageBoxIcon.Warning);
        //            return false;
        //        }

        //        SignCert = xtxapp.SOF_ExportUserCert(EncCert);

        //        ClsPublic.WriteLog("获取CertSN", 3);
        //        strCertSN = xtxapp.SOF_GetCertInfo(SignCert, 2);
        //        ClsPublic.WriteLog("CertSN：" + strCertSN, 4);
        //        ClsPublic.WriteLog("获取CertDN", 3);
        //        //CertDN = xtxapp.SOF_GetCertInfo(SignCert, 33);   证书主题对证书使用没有影响，直接使用 唯一序列号 作为 证书主题
        //        CertDN = strCertSN;
        //        ClsPublic.WriteLog("CertDN：" + CertDN, 4);
        //        //判断该证书信息是否和操作员在用的证书一致，如果不一致，更新患者证书
        //        if (!string.IsNullOrEmpty(CertSN) && !CertSN.Equals(strCertSN))
        //        {
        //            //操作员证书已经更新，更新操作员证书信息，并提示操作员重新登录系统
        //            Cert_Update(0, ClsPublic.gintUserID, CertDN, strCertSN, SignCert, EncCert);
        //            return false;
        //        }
        //        CertSN = strCertSN;
        //        SignPic = pic.GetPic(EncCert);
        //        ClsPublic.WriteLog("获取KEY证书信息成功", 3);
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        ClsPublic.WriteLog("获取KEY证书信息程序异常。" + ex.Message, 1);
        //        MessageBox.Show("获取Key信息失败，" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        return false;
        //    }
        //}

        /// <summary>
        /// KEY证书登录验证
        /// </summary>
        /// <param name="strCertID"></param>
        /// <param name="strCert"></param>
        /// <returns></returns>
        //public static bool GetCertLogin(string strCertID, string strCert)
        //{
        //    bool result = false;
        //    //1)（BJCA_SVS_ClientCOM组件）HIS系统调用CA接口，获取随机数、服务器证书，并通过服务器证书对随机数进行签名；
        //    string random = svs.GenRandom(16); //获取随机数
        //    string serverCert = svs.GetServerCertificate(); //获取服务器证书
        //    string serverSign = svs.SignData(random); //服务端对随机数签名

        //    DateTime HISSystemTime;
        //    bool blnRet = xtxapp.SOF_VerifySignedData(serverCert, random, serverSign); //客户端验证服务端签名

        //    ClsPublic.WriteLog("KEY证书登录验证",3);
        //    try
        //    {
        //        HISSystemTime = DateTime.Now;

        //        if (!blnRet)
        //        {
        //            ClsPublic.WriteLog("KEY证书登录失败", 1);
        //            MessageBox.Show("服务端签名验证失败！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            return result;
        //        }
        //        //验证证书是否过期
        //        string strDate = xtxapp.SOF_GetCertInfo(strCert, 12);
        //        strDate = ClsPublic.FormatTime(strDate);
        //        if (!string.IsNullOrEmpty(strDate))
        //        {
        //            //验证客户端证书有效期剩余天数
        //            int intDay = ClsPublic.DateDiff(HISSystemTime, Convert.ToDateTime(strDate));
        //            if (intDay <= 30 && intDay > 0 && !blnShow)
        //            {
        //                MessageBox.Show("您的证书还有" + intDay + "天过期。", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        //                blnShow = true;
        //            }
        //            else if (intDay <= 0)
        //            {
        //                ClsPublic.WriteLog("KEY证书已过期", 1);
        //                MessageBox.Show("您的证书已过期 " + System.Math.Abs(intDay) + " 天。", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //                return result;
        //            }

        //            //验证证书是否过期
        //            string strSignVal = xtxapp.SOF_SignData(strCertID, random);//'客户端随机数签名
        //            int intRetSign = svs.VerifySignedData(strCert, random, strSignVal);// 服务端验证客户端签名
        //            int intRetVal = svs.ValidateAndSaveCertificate(strCert);//服务端验证客户端证书有效性并保存证书
        //            if (!(intRetSign == 0 && (intRetVal == 0 || intRetVal == 1)))
        //            {
        //                ClsPublic.WriteLog("KEY证书登录验证失败", 1);
        //                MessageBox.Show("客户端证书验失败！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                return result;
        //            }
        //            result = true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ClsPublic.WriteLog("KEY证书登录验证异常。" + ex.Message, 1);
        //        MessageBox.Show("校验Key介质失败，" + ex.Message, ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //    return result;
        //}
        #endregion

        #region HIS系统证书函数

        /// <summary>
        /// 人员APP证书已更新，需要更新HIS中的证书信息。更新后提示操作员重新登录
        /// </summary>
        /// <param name="intCertType"></param>
        /// <param name="intUserID"></param>
        /// <param name="CertDN"></param>
        /// <param name="CertSN"></param>
        /// <param name="SignCert"></param>
        /// <param name="EncCert"></param>
        /// <returns></returns>
        public static bool Cert_Update(int intCertType,int intUserID,string CertDN,string CertSN, string SignCert, string EncCert)
        {
            Boolean blnResult = true;
            //标记系统需要重新登录
            ClsPublic.gblnReload = true;
            ClsPublic.WriteLog("更新操作员证书信息",3);
            try
            {
                if (ClsPublic.gOraConn.State == ConnectionState.Closed)
                    ClsPublic.gOraConn.Open();
            }
            catch (Exception)
            {
                MessageBox.Show("打开HIS连接失败，请重试！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            OracleCommand cmd = ClsPublic.gOraConn.CreateCommand();

            try
            {
                cmd.CommandText = "Call Zl_人员证书记录_Insert(" + intUserID + ",'" + CertDN + "','" + CertSN + "','" + SignCert + "','" + EncCert + "',Null,0," + intCertType + ")";
                cmd.ExecuteNonQuery();

                MessageBox.Show("您的证书已经更新，需要重新登录系统后才能正常签名！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                blnResult = false;
                ClsPublic.WriteLog("更新人员证书失败。" + ex.Message, 1);
                MessageBox.Show("您的证书已经更新，但是更新证书到HIS系统失败，请重新登录后重试！", ClsPublic.gstrBoxTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            cmd.Dispose();
            ClsPublic.gOraConn.Close();
            return blnResult;
        }
        #endregion
    }
}
