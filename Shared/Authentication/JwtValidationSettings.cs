namespace SparkFinance.Authentication
{
    public sealed class JwtValidationSettings
    {
        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string PublicKey { get; set; } = string.Empty;

        public int ClockSkewSeconds { get; set; }

        public string CookieName { get; set; } = string.Empty;
    }
}
