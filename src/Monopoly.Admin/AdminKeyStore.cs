using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Monopoly.Net;

namespace Monopoly.Admin
{
    // Секретный ключ администратора: %AppData%\Monopoly\admin.key, зашифрован Windows (DPAPI) под текущим пользователем —
    // файл бесполезен на другом ПК и под другой учётной записью. В репозиторий не попадает.
    public static class AdminKeyStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("monopoly-admin-key");

        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monopoly", "admin.key");

        // null — ключа на этом ПК нет или его нельзя прочитать.
        public static ECDsa? Load()
        {
            try
            {
                var secret = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                var key = ECDsa.Create();
                key.ImportPkcs8PrivateKey(secret, out _);
                CryptographicOperations.ZeroMemory(secret);
                return key;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                return null;
            }
        }

        // Создаёт ключ, только если его ещё нет: существующий не перезаписывается никогда.
        public static ECDsa LoadOrCreate()
        {
            if (Load() is { } existing)
                return existing;
            if (File.Exists(FilePath))
                throw new InvalidOperationException($"Файл ключа {FilePath} є, але не читається. Його не перезаписано.");

            var key = AdminAuth.CreateKey();
            var secret = key.ExportPkcs8PrivateKey();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, ProtectedData.Protect(secret, Entropy, DataProtectionScope.CurrentUser));
            CryptographicOperations.ZeroMemory(secret);
            return key;
        }
    }
}
