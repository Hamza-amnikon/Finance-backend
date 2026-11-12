using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SparkFinance.Http;

namespace SalesInvoiceService.API.Controllers
{
    [ApiController]
    [Route("api/organization-profiles")]
    public class OrganizationProfilesController : ControllerBase
    {
        private readonly SalesInvoiceDbContext _context;

        public OrganizationProfilesController(SalesInvoiceDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new
                {
                    message = "Choose a valid page and page size between 1 and 100."
                });
            }

            var rows = _context.OrganizationProfiles.AsNoTracking();
            var total = await rows.CountAsync(cancellationToken);
            var data = await rows.OrderBy(row => row.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                data,
                total,
                page,
                pageSize
            });
        }

        [HttpGet("{id:int:min(1)}")]
        public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        {
            var profile = await _context.OrganizationProfiles.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPost]
        public Task<IActionResult> Create(OrganizationProfileDto dto, CancellationToken cancellationToken)
        {
            return Save(null, dto, cancellationToken);
        }

        [HttpPut("{id:int:min(1)}")]
        public Task<IActionResult> Update(int id, OrganizationProfileDto dto, CancellationToken cancellationToken)
        {
            return Save(id, dto, cancellationToken);
        }

        [HttpDelete("{id:int:min(1)}")]
        public async Task<IActionResult> Delete(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _context.OrganizationProfiles
                    .SingleOrDefaultAsync(
                        row => row.Id == id,
                        cancellationToken);

                if (profile == null)
                {
                    return NotFound(new
                    {
                        message = "Organization profile was not found. Refresh the list."
                    });
                }

                _context.OrganizationProfiles.Remove(profile);
                await _context.SaveChangesAsync(cancellationToken);

                return NoContent();
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "This organization profile changed. Refresh the list and try again."
                });
            }
        }

        private async Task<IActionResult> Save(
            int? id,
            OrganizationProfileDto dto,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new
                {
                    message = "Organization name and business email are required."
                });
            }

            var profile = id.HasValue
                ? await _context.OrganizationProfiles.SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
                : new OrganizationProfile();

            if (profile == null)
            {
                return NotFound();
            }

            var currency = dto.Currency.Trim().ToUpperInvariant();
            var active = await _context.CurrencyMasters.AsNoTracking()
                .AnyAsync(row => row.CurrencyCode == currency && row.IsActive, cancellationToken);

            if (!active && (!id.HasValue || profile.Currency != currency))
            {
                return BadRequest(new
                {
                    message = "Select an active currency from the currency master."
                });
            }

            profile.Name = dto.Name.Trim();
            profile.Email = dto.Email.Trim();
            profile.Phone = dto.Phone?.Trim() ?? "";
            profile.Currency = currency;
            profile.Address = dto.Address?.Trim() ?? "";
            profile.Notes = dto.Notes?.Trim() ?? "";

            if (!id.HasValue)
            {
                _context.OrganizationProfiles.Add(profile);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(profile);
        }
    }
}
