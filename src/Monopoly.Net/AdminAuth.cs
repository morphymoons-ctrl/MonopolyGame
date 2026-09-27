using System.Security.Cryptography;
using System.Text;

namespace Monopoly.Net
{
    // Подпись команд администратора (RULES.md, §14): ECDSA P-256.
    // Секретный ключ есть только на ПК автора (Monopoly.Admin хранит его зашифрованным), в игру встроен открытый.
    // Код открыт, но без секретного ключа подписать команду нельзя.
    public static class AdminAuth
    {
        // Открытый ключ автора игры (SubjectPublicKeyInfo, base64). Новый ключ — Monopoly.Admin.exe --new-key.
        public const string OwnerPublicKey =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEWdloyg7d2uQliuFUsHDZLTMNReVC+CGSxmOplbi+JcKM75QzG6nz/KidYTjnmpjQaUA2KyXk32nzo3Phm4GoyA==";

        // Вход: подпись одноразового числа хоста. Команда: подпись номера и текста команды в рамках этого входа —
        // перехваченную подпись нельзя повторить ни в другой партии, ни в другом подключении, ни второй раз.
        public static byte[] LoginData(Guid gameId, byte[] nonce) =>
            Encoding.UTF8.GetBytes($"monopoly-admin-login|{gameId:N}|{Convert.ToBase64String(nonce)}");

        public static byte[] CommandData(Guid gameId, byte[] nonce, long sequence, string actionJson) =>
            Encoding.UTF8.GetBytes($"monopoly-admin-command|{gameId:N}|{Convert.ToBase64String(nonce)}|{sequence}|{actionJson}");

        public static byte[] Sign(ECDsa key, byte[] data) => key.SignData(data, HashAlgorithmName.SHA256);

        public static bool Verify(string publicKey, byte[] data, byte[]? signature)
        {
            if (string.IsNullOrEmpty(publicKey) || signature is null)
                return false;
            try
            {
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                return key.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                return false;
            }
        }

        public static string ExportPublicKey(ECDsa key) => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        public static ECDsa CreateKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }
}
