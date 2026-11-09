# SparkFinance backend

The backend contains 12 independently running ASP.NET Core APIs targeting .NET 10.
They use the existing SparkFinance database on `(localdb)\MSSQLLocalDB`.

## Run in Visual Studio

1. Open `SparkFinanceBackend.slnx` in the repository root.
2. Stop previous debugging sessions before rebuilding.
3. In the solution startup settings, select multiple startup projects and choose
   **Start** for the APIs you need. The full frontend uses all 12 APIs.
4. Use the `https` launch profile where available; ClientService uses `http`.

## Run one API from a terminal

From the repository root:

```powershell
dotnet build SparkFinanceBackend.slnx
dotnet run --project ClientService.API --launch-profile http --no-build
```

For other APIs, use their project name and `--launch-profile https` in separate
terminals. Press Ctrl+C to stop each API before rebuilding. Running `dotnet run`
in the repository root without `--project` does not select an API.

## Troubleshoot startup

- **MSB3021 / MSB3027 / file used by another process:** an earlier API instance
  is holding its build output open. Stop that instance (Visual Studio Stop
  Debugging or Ctrl+C in its terminal), then rebuild. Avoid launching the same
  API from both Visual Studio and a terminal.
- **Address already in use:** another instance already owns the configured port.
  Stop that instance before launching another copy.
- **Swagger opens but data requests fail:** check the LocalDB instance using
  `SqlLocalDB info MSSQLLocalDB`, then `SqlLocalDB start MSSQLLocalDB` if stopped.
  The tool may need its full path, for example
  `C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe`.
- If LocalDB reports **Stopped** while its SQL Server process is still running,
  the instance may have stale state. Close database clients and restart that
  instance normally before retrying. Do not delete or recreate the instance or
  its database files to address a startup problem.

Database migrations are separate from starting the APIs. Do not reset or migrate
the existing database merely to resolve process locks or LocalDB startup errors.

## Daily exchange-rate agent

`ExchangeRateService.API` provides the read-only Exchange Rates page and shared
rates for Dashboard, Banking and Expenses. Include it as a startup project using
its `https` profile (7441/7442). See [setup and verification](ExchangeRateService.API/README.md).
The default daily check is 19:30 India time (14:00 UTC), with hourly retries.
Daily checks require the API to be running; no external scheduler is installed.

## Organization profiles

Organization profiles use `SalesInvoiceService.API` through `/api/organization-profiles` for paginated lists, detail, create and update. Apply `SalesInvoiceService.API/Database/AddOrganizationProfiles.sql` to an existing deployment before enabling the page; the script only creates the table if absent. Restart the API after deploying the controller and restart Vite after changing proxy configuration.

The profile uses active currencies from `sales.CurrencyMasters`. An unchanged saved currency is preserved when it later becomes inactive. Profiles store general contact information and do not modify accounting companies, branch invoice details, or their tax registration numbers.
