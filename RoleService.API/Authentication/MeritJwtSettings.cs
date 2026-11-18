using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace RoleService.API.Authentication
{
    public class MeritJwtSettings
    {
        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string PrivateKey { get; set; } = string.Empty;

        public int ExpiryMinutes { get; set; } = 30;

        public RsaSecurityKey? CreateSigningKey()
        {
            if (string.IsNullOrWhiteSpace(Issuer) ||
                string.IsNullOrWhiteSpace(Audience) ||
                string.IsNullOrWhiteSpace(PrivateKey) ||
                ExpiryMinutes < 1 || ExpiryMinutes > 480)
            {
                return null;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(PrivateKey);

                if (rsa.KeySize < 2048)
                {
                    return null;
                }

                return new RsaSecurityKey(rsa.ExportParameters(true))
                {
                    KeyId = Base64UrlEncoder.Encode(
                        SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))
                };
            }
            catch (Exception exception) when (
                exception is CryptographicException or ArgumentException)
            {
                return null;
            }
        }
    }
}
