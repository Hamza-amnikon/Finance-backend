using BankingService.API.Data;
using BankingService.API.DTOs;
using BankingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Controllers
{
    [ApiController]
    [Route("api/bank-transactions/payment-sync")]
    public class PaymentBankingController : ControllerBase
    {
        private readonly BankingDbContext _context;

        private readonly PaymentBankingService _service;

        public PaymentBankingController(
            BankingDbContext context,
            PaymentBankingService service)
        {
            _context = context;
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(
            CancellationToken cancellationToken)
        {
            var issues =
                await _context.PaymentBankingLinks
                    .AsNoTracking()
                    .Where(row =>
                        row.State == "NeedsReview" ||
                        row.AccountingState == "NeedsReview" ||
                        (
                            row.State == "Draft" &&
                            row.Message != ""
                        ) ||
                        (
                            (
                                row.State == "Posted" ||
                                row.State == "NoBank"
                            ) &&
                            row.AccountingJournalEntryId == null &&
                            (
                                row.AccountingState == "" ||
                                row.AccountingState == "Pending"
                            )
                        ))
                    .OrderByDescending(row =>
                        row.UpdatedAtUtc)
                    .Take(25)
                    .Select(row => new
                    {
                        row.PaymentNumber,
                        row.Source,
                        row.Branch,
                        row.Currency,
                        row.Amount,
                        row.State,
                        row.Message,
                        row.BankTransactionId,
                        row.AccountingState,
                        row.AccountingMessage,
                        row.AccountingJournalEntryId
                    })
                    .ToListAsync(cancellationToken);

            var enabledFromUtc =
                await _context.PaymentBankingSettings
                    .Where(row => row.Id == 1)
                    .Select(row => row.SourceAfterUtc)
                    .SingleAsync(cancellationToken);

            return Ok(new
            {
                issues,
                enabledFromUtc
            });
        }

        [HttpPost("received/{paymentId:int}/resolve")]
        public async Task<IActionResult> ResolveReceivedAsync(
            int paymentId,
            ResolveReceivedPaymentDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                await _service.ResolveReceivedAsync(
                    paymentId,
                    dto,
                    cancellationToken);

                var result =
                    await _context.PaymentBankingLinks
                        .AsNoTracking()
                        .Where(row =>
                            row.Source == "Received" &&
                            row.SourcePaymentId == paymentId)
                        .Select(row => new
                        {
                            row.PaymentNumber,
                            row.State,
                            row.Message,
                            row.BankTransactionId,
                            row.AccountingState,
                            row.AccountingMessage,
                            row.AccountingJournalEntryId
                        })
                        .SingleAsync(cancellationToken);

                return Ok(result);
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new
                {
                    message = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
        }
    }
}
