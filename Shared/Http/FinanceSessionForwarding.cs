using Microsoft.AspNetCore.Http;

namespace SparkFinance.Http
{
    internal static class FinanceSessionForwarding
    {
        public static void AttachTo(HttpRequestMessage outgoing, HttpRequest? incoming)
        {
            if (incoming == null)
            {
                return;
            }

            // Use only for requests to a configured, trusted Finance service.
            foreach (var header in new[] { "Authorization", "Cookie", "X-Finance-Authentication" })
            {
                if (incoming.Headers.TryGetValue(header, out var value))
                {
                    outgoing.Headers.TryAddWithoutValidation(header, value.ToArray());
                }
            }
        }
    }
}
