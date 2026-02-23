using System;
using System.Security.Cryptography;
using System.Text;

namespace AccommodationSystem.Services
{
    /// <summary>
    /// Windows DPAPI を使用した機密情報の暗号化
    /// </summary>
    public static class EncryptionService
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AccommodationSystem_v1");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            try
            {
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return plainText; // フォールバック
            }
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;
            try
            {
                var data = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return encryptedText; // 旧データ（平文）はそのまま返す
            }
        }
    }
}
