using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class AuthSecurityTests
    {
        [Fact]
        public void BCrypt_HashAndVerify_PasswordMatches()
        {
            var rawPassword = "StrongSecretPassword123!";
            var hash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

            Assert.NotNull(hash);
            Assert.StartsWith("$2", hash);
            Assert.True(BCrypt.Net.BCrypt.Verify(rawPassword, hash));
            Assert.False(BCrypt.Net.BCrypt.Verify("WrongPassword456!", hash));
        }

        [Fact]
        public void AesGcm_EncryptionAndDecryption_RoundtripSucceeds()
        {
            var masterKeyString = "SuperSecretMasterKeyForVault2026";
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(masterKeyString));

            var originalText = "MySensitiveBankPin_9876";
            var plainBytes = Encoding.UTF8.GetBytes(originalText);

            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipher = new byte[plainBytes.Length];

            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plainBytes, cipher, tag);
            }

            Assert.NotEqual(plainBytes, cipher);

            var decryptedBytes = new byte[cipher.Length];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(nonce, cipher, tag, decryptedBytes);
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedBytes);
            Assert.Equal(originalText, decryptedText);
        }
    }
}

