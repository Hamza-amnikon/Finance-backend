using System.ComponentModel.DataAnnotations;

namespace BillService.API.Models
{
    public enum BillStatus { Draft = 0, Issued = 1, Cancelled = 2 }
}
