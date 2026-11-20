# Development startup

BankingService uses RunWithDevelopmentInstanceGuard. In Development, one host per service/checkout is allowed. A second host exits before binding ports and logs a clear instruction. A configured port occupied by an older binary or another application is reported without an unhandled app.Run exception, with exit code 1. Other startup failures still propagate. Production startup is unchanged.

The guard does not terminate processes or choose a different port. Use one owner: Visual Studio or a terminal. Stop the running debugger before rebuilding, then start BankingService once using its existing HTTPS profile. Stop terminal-launched services before starting the same service in Visual Studio.

Verification: dotnet run --project DevelopmentStartup.Tests. The test creates only temporary localhost hosts and does not access Finance databases.
