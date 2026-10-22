using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Data
{
    public class BankingDbContext : DbContext
    {
        public BankingDbContext(
            DbContextOptions<BankingDbContext> options)
            : base(options)
        {
        }

        public DbSet<BankAccount> BankAccounts =>
            Set<BankAccount>();

        public DbSet<BankingCompanyLookup> Companies =>
            Set<BankingCompanyLookup>();

        public DbSet<BankingCurrencyLookup> Currencies =>
            Set<BankingCurrencyLookup>();

        public DbSet<BankTransaction> BankTransactions =>
            Set<BankTransaction>();

        public DbSet<BankTransfer> BankTransfers =>
            Set<BankTransfer>();

        public DbSet<BankReconciliation> BankReconciliations =>
            Set<BankReconciliation>();

        public DbSet<BankStatementLine> BankStatementLines =>
            Set<BankStatementLine>();

        public DbSet<PaymentBankingLink> PaymentBankingLinks => Set<PaymentBankingLink>();

        public DbSet<PaymentBankingSettings> PaymentBankingSettings => Set<PaymentBankingSettings>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PaymentBankingSettings>(entity =>
            {
                entity.ToTable("PaymentBankingSettings", "banking");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<PaymentBankingLink>(entity =>
            {
                entity.ToTable("PaymentBankingLinks", "banking");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Amount).HasPrecision(18, 4);
                entity.HasIndex(row => new { row.Source, row.SourcePaymentId }).IsUnique();
                entity.HasIndex(row => new { row.State, row.NextAttemptAtUtc });
                entity.HasOne<BankTransaction>().WithMany().HasForeignKey(row => row.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<BankTransaction>().WithMany().HasForeignKey(row => row.ReversalTransactionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.ToTable(
                    "BankAccounts",
                    "expenses");

                entity.HasKey(account => account.Id);

                entity.Property(account => account.OpeningBalance)
                    .HasPrecision(18, 4);

                entity.Property(account => account.Currency)
                    .HasMaxLength(3);

                entity.Property(account => account.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(account => account.CompanyId);
            });

            modelBuilder.Entity<BankingCompanyLookup>(entity =>
            {
                entity.HasNoKey();

                entity.ToView(
                    "Companies",
                    "expenses");
            });

            modelBuilder.Entity<BankingCurrencyLookup>(entity =>
            {
                entity.HasNoKey();

                entity.ToView(
                    "CurrencyMasters",
                    "sales");
            });

            modelBuilder.Entity<BankTransaction>(entity =>
            {
                entity.ToTable(
                    "BankTransactions",
                    "banking",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_BankTransaction_Amount",
                            "[Amount] > 0");

                        table.HasCheckConstraint(
                            "CK_BankTransaction_Direction",
                            "[Direction] IN (1, 2)");

                        table.HasCheckConstraint(
                            "CK_BankTransaction_Status",
                            "[Status] IN ('Draft', 'Posted', 'Cancelled')");

                        table.HasCheckConstraint(
                            "CK_BankTransaction_Currency",
                            "LEN([Currency]) = 3");

                        table.HasCheckConstraint(
                            "CK_BankTransaction_CurrencyDecimalPlaces",
                            "[CurrencyDecimalPlaces] BETWEEN 0 AND 4");
                    });

                entity.HasKey(transaction => transaction.Id);

                entity.Property(transaction => transaction.Amount)
                    .HasPrecision(18, 4);

                entity.Property(transaction => transaction.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(transaction => transaction.RequestId)
                    .IsUnique();

                entity.HasIndex(transaction => new
                {
                    transaction.BankAccountId,
                    transaction.TransactionDate
                });

                entity.HasIndex(transaction => new
                {
                    transaction.CompanyId,
                    transaction.TransactionDate
                });

                entity.HasOne(transaction => transaction.BankAccount)
                    .WithMany()
                    .HasForeignKey(transaction => new
                    {
                        transaction.BankAccountId,
                        transaction.CompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<BankTransaction>()
                    .WithMany()
                    .HasForeignKey(
                        transaction => transaction.ReversalOfTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(
                    transaction => transaction.ReversalOfTransactionId)
                    .IsUnique()
                    .HasFilter("[ReversalOfTransactionId] IS NOT NULL");
            });

            modelBuilder.Entity<BankTransfer>(entity =>
            {
                entity.ToTable(
                    "BankTransfers",
                    "banking",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_BankTransfer_Accounts",
                            "[SourceBankAccountId] <> [DestinationBankAccountId]");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_SourceAmount",
                            "[SourceAmount] > 0");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_SourceFee",
                            "[SourceFeeAmount] >= 0");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_DestinationAmount",
                            "[DestinationAmount] IS NULL OR [DestinationAmount] > 0");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_ExchangeRate",
                            "[EffectiveExchangeRate] IS NULL OR [EffectiveExchangeRate] > 0");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_Status",
                            "[Status] IN (0, 1, 2, 3)");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_Currencies",
                            "LEN([SourceCurrency]) = 3 AND LEN([DestinationCurrency]) = 3");

                        table.HasCheckConstraint(
                            "CK_BankTransfer_CurrencyDecimalPlaces",
                            "[SourceCurrencyDecimalPlaces] BETWEEN 0 AND 4 AND " +
                            "[DestinationCurrencyDecimalPlaces] BETWEEN 0 AND 4");
                    });

                entity.HasKey(transfer => transfer.Id);

                entity.Property(transfer => transfer.SourceAmount)
                    .HasPrecision(18, 4);

                entity.Property(transfer => transfer.SourceFeeAmount)
                    .HasPrecision(18, 4);

                entity.Property(transfer => transfer.DestinationAmount)
                    .HasPrecision(18, 4);

                entity.Property(transfer => transfer.EffectiveExchangeRate)
                    .HasPrecision(28, 12);

                entity.Property(transfer => transfer.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(transfer => transfer.RequestId)
                    .IsUnique();

                entity.HasIndex(transfer => new
                {
                    transfer.SourceBankAccountId,
                    transfer.TransferDate
                });

                entity.HasIndex(transfer => new
                {
                    transfer.DestinationBankAccountId,
                    transfer.TransferDate
                });

                entity.HasIndex(transfer => new
                {
                    transfer.SourceCompanyId,
                    transfer.TransferDate
                });

                entity.HasIndex(transfer => new
                {
                    transfer.DestinationCompanyId,
                    transfer.TransferDate
                });

                entity.HasIndex(transfer => new
                {
                    transfer.Status,
                    transfer.TransferDate
                });

                entity.HasOne(transfer => transfer.SourceBankAccount)
                    .WithMany()
                    .HasForeignKey(transfer => new
                    {
                        transfer.SourceBankAccountId,
                        transfer.SourceCompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(transfer => transfer.DestinationBankAccount)
                    .WithMany()
                    .HasForeignKey(transfer => new
                    {
                        transfer.DestinationBankAccountId,
                        transfer.DestinationCompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<BankTransaction>()
                    .WithMany()
                    .HasForeignKey(
                        transfer => transfer.SourceTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<BankTransaction>()
                    .WithMany()
                    .HasForeignKey(
                        transfer => transfer.SourceFeeTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<BankTransaction>()
                    .WithMany()
                    .HasForeignKey(
                        transfer => transfer.DestinationTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(transfer => transfer.SourceTransactionId)
                    .IsUnique()
                    .HasFilter("[SourceTransactionId] IS NOT NULL");

                entity.HasIndex(transfer => transfer.SourceFeeTransactionId)
                    .IsUnique()
                    .HasFilter("[SourceFeeTransactionId] IS NOT NULL");

                entity.HasIndex(transfer => transfer.DestinationTransactionId)
                    .IsUnique()
                    .HasFilter("[DestinationTransactionId] IS NOT NULL");
            });

            modelBuilder.Entity<BankReconciliation>(entity =>
            {
                entity.ToTable(
                    "BankReconciliations",
                    "banking",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_BankReconciliation_Period",
                            "[PeriodStart] <= [PeriodEnd]");

                        table.HasCheckConstraint(
                            "CK_BankReconciliation_Status",
                            "[Status] IN ('Draft', 'Completed')");

                        table.HasCheckConstraint(
                            "CK_BankReconciliation_Currency",
                            "LEN([Currency]) = 3");

                        table.HasCheckConstraint(
                            "CK_BankReconciliation_Precision",
                            "[CurrencyDecimalPlaces] BETWEEN 0 AND 4");

                        table.HasCheckConstraint(
                            "CK_BankReconciliation_Completion",
                            "([Status] = 'Draft' AND [CompletedDate] IS NULL) OR " +
                            "([Status] = 'Completed' AND [CompletedDate] IS NOT NULL)");
                    });

                entity.HasKey(reconciliation => reconciliation.Id);

                entity.Property(
                    reconciliation => reconciliation.StatementOpeningBalance)
                    .HasPrecision(18, 4);

                entity.Property(
                    reconciliation => reconciliation.StatementClosingBalance)
                    .HasPrecision(18, 4);

                entity.Property(reconciliation => reconciliation.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(reconciliation => reconciliation.RequestId)
                    .IsUnique();

                entity.HasIndex(reconciliation => new
                {
                    reconciliation.BankAccountId,
                    reconciliation.PeriodStart,
                    reconciliation.PeriodEnd
                })
                    .IsUnique();

                entity.HasOne(reconciliation => reconciliation.BankAccount)
                    .WithMany()
                    .HasForeignKey(reconciliation => new
                    {
                        reconciliation.BankAccountId,
                        reconciliation.CompanyId
                    })
                    .HasPrincipalKey(account => new
                    {
                        account.Id,
                        account.CompanyId
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BankStatementLine>(entity =>
            {
                entity.ToTable(
                    "BankStatementLines",
                    "banking",
                    table =>
                    {
                        table.HasCheckConstraint(
                            "CK_BankStatementLine_Amount",
                            "[Amount] > 0");

                        table.HasCheckConstraint(
                            "CK_BankStatementLine_Direction",
                            "[Direction] IN (1, 2)");

                        table.HasCheckConstraint(
                            "CK_BankStatementLine_LineNumber",
                            "[LineNumber] > 0");

                        table.HasCheckConstraint(
                            "CK_BankStatementLine_Match",
                            "([MatchedBankTransactionId] IS NULL AND [MatchedDate] IS NULL) OR " +
                            "([MatchedBankTransactionId] IS NOT NULL AND [MatchedDate] IS NOT NULL)");
                    });

                entity.HasKey(line => line.Id);

                entity.Property(line => line.Amount)
                    .HasPrecision(18, 4);

                entity.Property(line => line.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(line => new
                {
                    line.BankReconciliationId,
                    line.LineNumber
                })
                    .IsUnique();

                entity.HasIndex(line => line.MatchedBankTransactionId)
                    .IsUnique()
                    .HasFilter("[MatchedBankTransactionId] IS NOT NULL");

                entity.HasOne(line => line.BankReconciliation)
                    .WithMany(reconciliation => reconciliation.Lines)
                    .HasForeignKey(line => line.BankReconciliationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(line => line.MatchedBankTransaction)
                    .WithMany()
                    .HasForeignKey(line => line.MatchedBankTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}