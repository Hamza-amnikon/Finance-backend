using SparkFinance.Auditing;
using Microsoft.EntityFrameworkCore;

namespace DashboardService.API.Data
{
    public class DashboardDbContext : AuditedDbContext
    {
        public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options)
        {
        }
    }
}
