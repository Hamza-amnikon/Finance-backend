using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AccountingService.API.Controllers
{
    [ApiController]
    [Route("api/tax-settings/vat-setup")]
    [ServiceFilter(typeof(TaxSettingsAccessFilter))]
    public class GeneralVatSetupController : ControllerBase
    {
        private readonly GeneralVatSetupService _service;

        public GeneralVatSetupController(GeneralVatSetupService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Setup(
            GeneralVatSetupRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await _service.SetupAsync(
                    request.CompanyId,
                    cancellationToken);

                return Ok(new
                {
                    message = "General VAT and company accounts are ready. " +
                        "Create a tax rate with the appropriate effective dates."
                });
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                return Conflict(new
                {
                    message = "Another setup is being saved. Refresh and retry."
                });
            }
        }
    }
}
