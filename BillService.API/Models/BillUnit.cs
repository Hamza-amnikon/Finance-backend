using System.ComponentModel.DataAnnotations;

namespace BillService.API.Models
{
    public enum BillUnit { Hour = 0, Day = 1, Month = 2, FixedFee = 3, Piece = 4 }
}
