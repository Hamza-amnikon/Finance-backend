using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Models
{
    public class Receipt
    {
        [Key]
        public int ExpenseId { get; set; }

        public Expense Expense { get; set; } = null!;

        [MaxLength(200)]
        public string FileName { get; set; } = "";

        [MaxLength(80)]
        public string ContentType { get; set; } = "";

        public byte[] Content { get; set; } = [];
    }
}
