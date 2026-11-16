using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using AccountingService.API.Data;
using AccountingService.API.Models;
using AccountingService.API.Repositories;
using AccountingService.API.Services;
using Microsoft.EntityFrameworkCore;

internal static class GeneralVatSetupChecks
{
    public static async Task RunAsync()
    {
        var database = "GeneralVatTests_" + Guid.NewGuid().ToString("N");
        var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
        await using var context = new AccountingDbContext(
            new DbContextOptionsBuilder<AccountingDbContext>()
                .UseSqlServer(connection)
                .Options);

        var masterConnection = new SqlConnectionStringBuilder(connection)
        {
            InitialCatalog = "master"
        };
        await using var master = new SqlConnection(masterConnection.ConnectionString);
        await master.OpenAsync();
        await using var create = master.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{database}]";
        await create.ExecuteNonQueryAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA expenses");
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE expenses.Companies (Id int IDENTITY PRIMARY KEY, Name nvarchar(160) NOT NULL, Country nvarchar(2) NOT NULL, Currency nvarchar(3) NOT NULL)");

            foreach (var batch in Regex.Split(context.Database.GenerateCreateScript(),
                @"^GO\s*$", RegexOptions.Multiline))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await context.Database.ExecuteSqlRawAsync(batch);
                }
            }

            context.Companies.AddRange(
                new AccountingCompanyLookup { Name = "Oman first", Country = "OM", Currency = "OMR" },
                new AccountingCompanyLookup { Name = "Oman second", Country = "OM", Currency = "OMR" },
                new AccountingCompanyLookup { Name = "Dubai", Country = "AE", Currency = "AED" },
                new AccountingCompanyLookup { Name = "Turbhe", Country = "IN", Currency = "INR" });
            context.TaxTypes.AddRange(
                new TaxConfigurationType
                {
                    CountryCode = "AE",
                    Code = "VAT",
                    Name = "VAT",
                    Treatment = "UaeVat",
                    ComponentNamesJson = "[\"VAT\"]",
                    IsActive = true
                },
                new TaxConfigurationType
                {
                    CountryCode = "IN",
                    Code = "GST",
                    Name = "GST",
                    Treatment = "IndiaCgstSgst",
                    ComponentNamesJson = "[\"CGST\",\"SGST\"]",
                    IsActive = true
                });
            await context.SaveChangesAsync();

            var companies = await context.Companies.OrderBy(row => row.Id).ToListAsync();
            var first = companies[0];
            var second = companies[1];
            var accounts = new ChartOfAccountsSetupService(new AccountRepository(context));
            var setup = new GeneralVatSetupService(context, accounts);
            await setup.SetupAsync(first.Id, default);
            var originalIds = await context.Accounts
                .Where(row => row.CompanyId == first.Id)
                .OrderBy(row => row.Id)
                .Select(row => row.Id)
                .ToArrayAsync();
            await setup.SetupAsync(first.Id, default);
            var repeatedIds = await context.Accounts
                .Where(row => row.CompanyId == first.Id)
                .OrderBy(row => row.Id)
                .Select(row => row.Id)
                .ToArrayAsync();
            Check(originalIds.SequenceEqual(repeatedIds), "Repeated general VAT setup reuses every account");
            Check(await context.TaxTypes.CountAsync(row => row.CountryCode == "OM") == 1,
                "Repeated setup creates only one country VAT definition");
            Check(!await context.TaxRates.AnyAsync(), "Setup never invents or inserts tax rates");

            await setup.SetupAsync(second.Id, default);
            Check(await context.Accounts.CountAsync(row => row.CompanyId == second.Id &&
                (row.SystemKey == "INPUT_VAT" || row.SystemKey == "OUTPUT_VAT")) == 2,
                "A second Oman company receives its own VAT accounts");
            Check(await context.TaxTypes.CountAsync(row => row.CountryCode == "OM") == 1,
                "Companies share the persisted country definition");

            foreach (var company in companies.Skip(2))
            {
                try
                {
                    await setup.SetupAsync(company.Id, default);
                    throw new InvalidOperationException("Existing country treatment was replaced.");
                }
                catch (ValidationException)
                {
                    Check(!await context.Accounts.AnyAsync(row => row.CompanyId == company.Id),
                        "General VAT setup does not alter existing " + company.Country + " configuration");
                }
            }

            var vatAccount = await context.Accounts.SingleAsync(row =>
                row.CompanyId == first.Id && row.SystemKey == "OUTPUT_VAT");
            var date = new DateOnly(2026, 11, 10);
            context.JournalEntries.Add(new JournalEntry
            {
                CompanyId = first.Id,
                EntryNumber = "TEST-VAT",
                EntryDate = date,
                Currency = "OMR",
                Status = JournalEntryStatus.Posted,
                Lines = new List<JournalEntryLine>
                {
                    new JournalEntryLine
                    {
                        CompanyId = first.Id,
                        AccountId = vatAccount.Id,
                        LineNumber = 1,
                        CreditAmount = 7.5m,
                        BaseCreditAmount = 7.5m
                    }
                }
            });
            await context.SaveChangesAsync();
            var report = await new TaxReportService(context).GetAsync(first.Id, date, date, default);
            Check(report.Accounts.Single(row => row.SystemKey == "OUTPUT_VAT").PeriodCredit == 7.5m,
                "General VAT report reads the selected company's posted tax movement");
            var otherReport = await new TaxReportService(context).GetAsync(second.Id, date, date, default);
            Check(otherReport.Accounts.All(row => row.PeriodCredit == 0),
                "Another company's VAT report excludes that posting");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine("PASS: " + message);
    }
}
