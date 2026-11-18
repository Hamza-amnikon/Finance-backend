using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SparkFinance.Authentication
{
    public static class JwtValidationParameters
    {
        public static TokenValidationParameters Create(
            JwtValidationSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(settings.Issuer))
            {
                failures.Add("Configure the JWT issuer.");
            }

            if (string.IsNullOrWhiteSpace(settings.Audience))
            {
                failures.Add("Configure the JWT audience.");
            }

            if (settings.ClockSkewSeconds is < 0 or > 300)
            {
                failures.Add("JWT clock skew must be between zero and 300 seconds.");
            }

            if ((settings.CookieName ?? string.Empty).Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character != '.' && character != '-' && character != '_'))
            {
                failures.Add("Configure a valid JWT cookie name or leave it empty for bearer-only validation.");
            }

            RsaSecurityKey? publicKey = null;

            try
            {
                var pem = settings.PublicKey?.Trim() ?? string.Empty;

                if (!pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) ||
                    !pem.EndsWith("-----END PUBLIC KEY-----", StringComparison.Ordinal))
                {
                    failures.Add("Configure an RSA public key in SubjectPublicKeyInfo PEM format. Private keys are not accepted.");
                }
                else
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(pem);

                    if (rsa.KeySize < 2048)
                    {
                        failures.Add("The JWT RSA public key must contain at least 2048 bits.");
                    }
                    else
                    {
                        publicKey = new RsaSecurityKey(rsa.ExportParameters(false))
                        {
                            KeyId = Base64UrlEncoder.Encode(
                                SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))
                        };
                    }
                }
            }
            catch (Exception exception) when (
                exception is CryptographicException or ArgumentException)
            {
                failures.Add("The configured JWT public key is invalid.");
            }

            if (failures.Count > 0)
            {
                throw new OptionsValidationException(
                    nameof(JwtValidationSettings),
                    typeof(JwtValidationSettings),
                    failures);
            }

            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = settings.Issuer,
                ValidAudience = settings.Audience,
                IssuerSigningKey = publicKey,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds),
                NameClaimType = "name",
                RoleClaimType = "role"
            };
        }
    }
}
