using System;
using System.Security.Cryptography;
using System.Text;

namespace iVault.Api.Services
{
    public class EncryptionService : IEncryptionService
    {
        // Entropy adds an extra layer of protection unique to this app
        private readonly byte[] _entropy = Encoding.UTF8.GetBytes("iVault-Plus-ECM-2026-Security-Layer");

		private readonly byte[] _key; // Must be 32 bytes for AES-256
		private readonly byte[] _iv;  // Must be 16 bytes

		public EncryptionService(IConfiguration config)
		{
			// Get these from Environment Variables or appsettings.json
			_key = Encoding.UTF8.GetBytes(config["Encryption:Key"] ?? "12345678901234567890123456789012");
			_iv = Encoding.UTF8.GetBytes(config["Encryption:IV"] ?? "1234567890123456");
		}


        public string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText)) return plainText;

            try
            {
				using var aes = Aes.Create();
				aes.Key = _key;
				aes.IV = _iv;

				var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
				using var ms = new MemoryStream();
				using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
				using (var sw = new StreamWriter(cs))
				{
					sw.Write(plainText);
				}
				return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                // In a production ECM, log this with Serilog
                throw new CryptographicException("Encryption failed.", ex);
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText)) return cipherText;

            try 
			{
				var buffer = Convert.FromBase64String(cipherText);
				using var aes = Aes.Create();
				aes.Key = _key;
				aes.IV = _iv;

				var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
				using var ms = new MemoryStream(buffer);
				using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
				using var sr = new StreamReader(cs);
				return sr.ReadToEnd();
			}
			catch (Exception)
			{
				return "[Decryption Error]"; // Prevent the worker from crashing
			}
        }
    }
}