using BankingService.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-transactions/payment-sync")]
    public class PaymentBankingController : ControllerBase
    {
        private readonly BankingDbContext _context;

        public PaymentBankingController(BankingDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            var rows = _context.PaymentBankingLinks.AsNoTracking();
            var issues = await rows.Where(row => row.State == "NeedsReview" || (row.State == "Draft" && row.Message != ""))
                .OrderByDescending(row => row.UpdatedAtUtc)
                .Take(25)
                .Select(row => new { row.PaymentNumber, row.Source, row.Branch, row.Currency, row.Amount, row.State, row.Message, row.BankTransactionId })
                .ToListAsync(cancellationToken);
            return Ok(new { issues, enabledFromUtc = await _context.PaymentBankingSettings.Where(row => row.Id == 1).Select(row => row.SourceAfterUtc).SingleAsync(cancellationToken) });
        }
    }
}
