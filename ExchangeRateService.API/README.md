# Daily exchange-rate API

This service runs the daily worker and provides read-only endpoints for the existing Finance Exchange Rates page and other APIs. Only the worker writes daily snapshots; no create, update, delete or manual refresh-write endpoints exist.

## Setup

1. Execute `Database/AddDailyExchangeRates.sql` against the existing SparkFinance development database. It creates only `finance.DailyExchangeRates` and `finance.ExchangeRateChecks`; it does not change transaction rates or master tables. The script is safe to rerun.
2. Add ExchangeRateService.API as a startup project in Visual Studio, using the `https` profile. Ports: HTTPS 7441 and HTTP 7442. The development HTTPS certificate must be trusted normally; certificate validation is not disabled.
3. Restart DashboardService.API, BankingService.API and ExpenseService.API so they pick up the shared source configuration. Restart Vite for the new proxy route.
4. Visit Settings → Exchange Rates. Initial setup catches up immediately for the most recent scheduled daily check.

## Schedule and behavior

`DailyRateAgent:CheckHourUtc` defaults to 14 (19:30 Asia/Kolkata). The service checks for due work every minute. Completed days are remembered across restarts; failed/partial checks retry after `RetryMinutes` (default 60). Keep the service running for scheduled checks. Active currencies come from `sales.CurrencyMasters`; INR is always included.

The worker fetches Frankfurter rates for one currency unit in INR, validates them and persists their original publication dates. A successful check does not guarantee a newer provider rate. Failed currencies retain their previous snapshots and are identified on the page. Reads never present an overdue snapshot as a current conversion quote. Pair conversions use rates from the same daily batch and matching publication dates (except INR's identity rate). Historical dates before monitoring began use the central provider adapter on demand.

SQL application locking prevents concurrent workers from processing the same day simultaneously. No database recreation, automatic migration, transaction edits or secret API keys are involved.

## Endpoints

- `GET /api/exchange-rates/latest`: saved rates, publication dates, check status, last-check and next-check times.
- `GET /api/exchange-rates/rate/USD/INR`: current saved pair, in the provider-compatible response format.
- `GET /api/exchange-rates/rate/USD/INR?date=2026-10-01`: historical lookup.

Run `dotnet run --project ExchangeRateService.Tests` for isolated SQL integration checks. A uniquely named temporary database is created and removed; SparkFinance is never used for fixtures. `EXCHANGE_RATE_TEST_SERVER` optionally selects a test server.
