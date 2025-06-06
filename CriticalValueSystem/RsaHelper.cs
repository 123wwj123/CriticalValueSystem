using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

public static class RsaHelper
{
    private static string _publicKey;

    public static string PublicKey
    {
        get
        {
            if (string.IsNullOrEmpty(_publicKey))
            {
                _publicKey = ConfigurationManager.AppSettings["RsaPublicKey"]?
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Replace(" ", "");

                if (string.IsNullOrEmpty(_publicKey))
                {
                    throw new ApplicationException("RSA公钥未配置或配置无效");
                }
            }
            return _publicKey;
        }
    }

    public static string Encrypt(string plainText)
    {
        try
        {
            // 使用BouncyCastle库解析PEM格式公钥
            var publicKeyParam = (RsaKeyParameters)PublicKeyFactory.CreateKey(
                Convert.FromBase64String(PublicKey));

            // 转换为.NET的RSAParameters
            var rsaParams = new RSAParameters
            {
                Modulus = publicKeyParam.Modulus.ToByteArrayUnsigned(),
                Exponent = publicKeyParam.Exponent.ToByteArrayUnsigned()
            };

            // 使用.NET RSA加密
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(rsaParams);
                var encryptedData = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), false);
                return Convert.ToBase64String(encryptedData);
            }
        }
        catch (Exception ex)
        {
            throw new CryptographicException("RSA加密失败", ex);
        }
    }
}