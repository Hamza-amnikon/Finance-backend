using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SparkFinance.Http
{
    public static class DevelopmentServiceStartup
    {
        public static void RunWithDevelopmentInstanceGuard(this WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.Run();
                return;
            }

            var serviceName = app.Environment.ApplicationName;
            var identity = serviceName + "|" +
                Path.GetFullPath(app.Environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            using var instance = new Mutex(false, "MERIT.Development.Service." + key);
            var acquired = false;

            try
            {
                try
                {
                    acquired = instance.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    app.Logger.LogWarning(
                        "{Service} is already running for this checkout. Stop the existing instance before starting it in Visual Studio. No second host was started.",
                        app.Environment.ApplicationName);
                    return;
                }

                try
                {
                    app.Run();
                }
                catch (Exception exception) when (IsAddressAlreadyInUse(exception))
                {
                    // Run disposes its service provider when startup fails.
                    // Write directly rather than resolving a logger from that disposed provider.
                    Console.Error.WriteLine(
                        serviceName + " could not start because a configured address is already in use. " +
                        "Stop the existing service in Visual Studio or its terminal, then start one instance. " +
                        "Check Properties/launchSettings.json for the configured addresses.");
                    Environment.ExitCode = 1;
                }
            }
            finally
            {
                if (acquired)
                {
                    instance.ReleaseMutex();
                }
                app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        public static bool IsAddressAlreadyInUse(Exception exception)
        {
            if (exception is AddressInUseException ||
                exception is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
            {
                return true;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.InnerExceptions.Any(IsAddressAlreadyInUse);
            }

            return exception.InnerException != null && IsAddressAlreadyInUse(exception.InnerException);
        }
    }
}
