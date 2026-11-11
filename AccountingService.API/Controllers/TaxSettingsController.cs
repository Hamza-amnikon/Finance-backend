using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/tax-settings")]
    public class TaxSettingsController : ControllerBase
    {
        private readonly AccountingDbContext _context;

        public TaxSettingsController(AccountingDbContext context)
        {
            _context = context;
        }

        private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This tax rate changed. Refresh before editing again." });
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
            {
                return Conflict(new { message = "This company already has that tax code and start date." });
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                return Conflict(new { message = "Another tax setting is being saved. Refresh and retry." });
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                return Problem(statusCode: 503, title: "Tax Settings database setup is pending. Apply AddTaxSettings.sql.");
            }
        }

        [HttpGet("lookups")]
        public Task<IActionResult> Lookups(CancellationToken cancellationToken) => Handle(async () =>
        {
            var companies = await _context.Companies.AsNoTracking()
                .OrderBy(row => row.Name).Select(row => new { row.Id, row.Name, row.Country, row.Currency })
                .ToListAsync(cancellationToken);
            var types = await _context.TaxTypes.AsNoTracking().OrderBy(row => row.Name).ToListAsync(cancellationToken);

            return Ok(new
            {
                companies,
                types = types.Select(row => new
                {
                    row.Id, row.Code, row.Name, row.CountryCode, row.Treatment, row.IsActive,
                    components = JsonSerializer.Deserialize<string[]>(row.ComponentNamesJson)
                })
            });
        });

        [HttpGet]
        public Task<IActionResult> List([FromQuery] int companyId, CancellationToken cancellationToken) => Handle(async () =>
        {
            if (!await _context.Companies.AnyAsync(row => row.Id == companyId, cancellationToken))
            {
                throw new ValidationException("Select an existing company.");
            }

            var rows = await _context.TaxRates.AsNoTracking().Where(row => row.CompanyId == companyId)
                .OrderBy(row => row.Code).ThenByDescending(row => row.EffectiveFrom).ToListAsync(cancellationToken);
            return Ok(rows.Select(ToResponse));
        });

        private static object ToResponse(TaxConfigurationRate row)
        {
            return new
            {
                row.Id, row.CompanyId, row.TaxTypeId, row.Code, row.Name, row.Rate,
                row.EffectiveFrom, row.EffectiveTo, row.IsActive, row.RowVersion,
                components = JsonSerializer.Deserialize<List<TaxComponentDto>>(row.ComponentsJson)
            };
        }

        [HttpPost]
        public Task<IActionResult> Create(TaxConfigurationDto dto, CancellationToken cancellationToken)
        {
            return Save(null, dto, cancellationToken);
        }

        [HttpPut("{id:int:min(1)}")]
        public Task<IActionResult> Update(int id, TaxConfigurationDto dto, CancellationToken cancellationToken)
        {
            return Save(id, dto, cancellationToken);
        }

        private Task<IActionResult> Save(int? id, TaxConfigurationDto dto, CancellationToken cancellationToken) => Handle(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            var company = await _context.Companies.AsNoTracking().SingleOrDefaultAsync(row => row.Id == dto.CompanyId, cancellationToken);
            var type = await _context.TaxTypes.AsNoTracking().SingleOrDefaultAsync(row => row.Id == dto.TaxTypeId, cancellationToken);
            var row = id.HasValue
                ? await _context.TaxRates.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                : new TaxConfigurationRate();

            if (row == null)
            {
                return NotFound();
            }

            if (company == null || type == null ||
                !string.Equals(company.Country.Trim(), type.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Select a tax type for the selected company's country.");
            }

            if (id.HasValue && row.CompanyId != dto.CompanyId)
            {
                throw new ValidationException("A tax rate cannot be moved to another company.");
            }

            if (!type.IsActive && (!id.HasValue || row.TaxTypeId != type.Id || dto.IsActive))
            {
                throw new ValidationException("Select an active tax type.");
            }

            if (dto.EffectiveFrom == default || (dto.EffectiveTo.HasValue && dto.EffectiveTo < dto.EffectiveFrom))
            {
                throw new ValidationException("Choose a valid effective date range.");
            }

            var names = JsonSerializer.Deserialize<string[]>(type.ComponentNamesJson)!;
            if (dto.Components.Count != names.Length || dto.Components.Select(item => item.Name).Distinct().Count() != names.Length ||
                names.Any(name => !dto.Components.Any(item => item.Name == name)) ||
                dto.Components.Any(item => item.Rate < 0 || item.Rate > 100 || decimal.Round(item.Rate, 4) != item.Rate) ||
                dto.Components.Sum(item => item.Rate) > 100)
            {
                throw new ValidationException("Enter each configured tax component once, using rates between 0 and 100 with at most four decimal places; their total cannot exceed 100%.");
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                throw new ValidationException("Tax name is required.");
            }

            var code = dto.Code.Trim().ToUpperInvariant();
            var end = dto.EffectiveTo ?? DateOnly.MaxValue;
            if (await _context.TaxRates.AnyAsync(item => item.CompanyId == dto.CompanyId && item.Code == code &&
                (!id.HasValue || item.Id != id.Value) && item.EffectiveFrom <= end &&
                (item.EffectiveTo == null || item.EffectiveTo >= dto.EffectiveFrom), cancellationToken))
            {
                throw new ValidationException("This tax code already has a version covering these dates. Use non-overlapping effective dates.");
            }

            if (id.HasValue)
            {
                if (string.IsNullOrWhiteSpace(dto.RowVersion))
                {
                    throw new ValidationException("Refresh this tax rate before saving.");
                }

                byte[] version;
                try
                {
                    version = Convert.FromBase64String(dto.RowVersion);
                }
                catch (FormatException)
                {
                    throw new ValidationException("Refresh this tax rate before saving.");
                }

                if (!row.RowVersion.SequenceEqual(version))
                {
                    return Conflict(new { message = "This tax rate changed. Refresh before editing again." });
                }
            }

            row.CompanyId = dto.CompanyId;
            row.TaxTypeId = dto.TaxTypeId;
            row.Code = code;
            row.Name = dto.Name.Trim();
            row.Rate = dto.Components.Sum(item => item.Rate);
            row.ComponentsJson = JsonSerializer.Serialize(dto.Components);
            row.EffectiveFrom = dto.EffectiveFrom;
            row.EffectiveTo = dto.EffectiveTo;
            row.IsActive = dto.IsActive;
            if (!id.HasValue)
            {
                _context.TaxRates.Add(row);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(ToResponse(row));
        });
    }
}
