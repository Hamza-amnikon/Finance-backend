using ClientService.API.Controllers;
using ClientService.API.Services;
using Microsoft.AspNetCore.Mvc;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + description);
    }
    checks++;
    Console.WriteLine("PASS: " + description);
}
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var service = new LocationStub
{
    Load = token => Task.FromCanceled<List<string>>(token)
};
var controller = new CountryMasterController(service);
Check(await controller.GetCountries(cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled country lookups are handled");
Check(await controller.GetStates("Test country", cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled state lookups are handled");
Check(await controller.GetCities("Test country", "Test state", cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled city lookups are handled");
var rows = new List<string> { "Test result" };
service.Load = token => Task.FromResult(rows);
Check(await controller.GetCountries(default) is OkObjectResult countries && ReferenceEquals(countries.Value, rows),
    "Successful lookups retain their original data");
Check(await controller.GetCities("Selected country", "Selected state", default) is OkObjectResult &&
    service.Country == "Selected country" && service.State == "Selected state",
    "City lookups retain their selected country and state filters");
service.Load = token => Task.FromException<List<string>>(new TaskCanceledException());
try
{
    await controller.GetCountries(default);
    throw new Exception("FAIL: Unexpected cancellation was hidden");
}
catch (TaskCanceledException)
{
    Check(true, "Unexpected service cancellations remain visible");
}
service.Load = token => Task.FromException<List<string>>(new InvalidOperationException("Database unavailable"));
try
{
    await controller.GetCountries(default);
    throw new Exception("FAIL: Database failure was hidden");
}
catch (InvalidOperationException)
{
    Check(true, "Unrelated failures are not returned as empty country lists");
}
Console.WriteLine($"All {checks} location cancellation checks passed.");

sealed class LocationStub : ICountryMasterService
{
    public Func<CancellationToken, Task<List<string>>> Load { get; set; } = token => Task.FromResult(new List<string>());
    public string? Country { get; private set; }
    public string? State { get; private set; }

    public Task<List<string>> GetCountriesAsync(CancellationToken token)
    {
        return Load(token);
    }

    public Task<List<string>> GetStatesAsync(string country, CancellationToken token)
    {
        Country = country;
        return Load(token);
    }

    public Task<List<string>> GetCitiesAsync(string country, string? state, CancellationToken token)
    {
        Country = country;
        State = state;
        return Load(token);
    }
}
