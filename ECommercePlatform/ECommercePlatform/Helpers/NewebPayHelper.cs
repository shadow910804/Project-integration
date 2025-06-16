using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Collections.Generic;

namespace ECommercePlatform.Helpers
{
    public class NewebPayHelper
    {
        public string MerchantID { get; set; } = "MS3448209";
        public string HashKey { get; set; } = "8NnPzWnFzvK0M4iiq1r8H9Vb8RvgL7yW";
        public string HashIV { get; set; } = "lzB3qWZy8dkA4Dgz";
        public string RespondURL { get; set; } = "https://yourdomain.com/Payment/Notify";
        public string ReturnURL { get; set; } = "https://yourdomain.com/Order/Confirm";

        public string EncryptAES(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(HashKey);
            aes.IV = Encoding.UTF8.GetBytes(HashIV);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return ByteArrayToHex(encrypted);
        }

        public string SHA256Hash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public string ByteArrayToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "").ToLower();

        public string BuildTradeInfo(Dictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            foreach (var pair in dict)
                sb.Append($"{pair.Key}={HttpUtility.UrlEncode(pair.Value)}&");
            return sb.ToString().TrimEnd('&');
        }

        public (string TradeInfo, string TradeSha) GeneratePaymentData(Dictionary<string, string> tradeData)
        {
            var raw = BuildTradeInfo(tradeData);
            var encrypted = EncryptAES(raw);
            var shaSource = $"HashKey={HashKey}&{encrypted}&HashIV={HashIV}";
            var sha = SHA256Hash(shaSource);
            return (encrypted, sha);
        }
    }
}
