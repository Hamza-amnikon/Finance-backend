using BankingService.API.Models;

namespace BankingService.API.Services
{
    public static class BankStatementAutoMatcher
    {
        public static bool IsCandidate(
            BankStatementLine line,
            BankTransaction transaction)
        {
            return
                !string.IsNullOrWhiteSpace(line.Reference) &&
                string.Equals(
                    line.Reference.Trim(),
                    transaction.Reference.Trim(),
                    StringComparison.OrdinalIgnoreCase) &&
                line.TransactionDate == transaction.TransactionDate &&
                line.Direction == transaction.Direction &&
                line.Amount == transaction.Amount;
        }

        public static int Match(
            IReadOnlyCollection<BankStatementLine> lines,
            IReadOnlyCollection<BankTransaction> transactions)
        {
            var unmatched = lines
                .Where(line => !line.MatchedBankTransactionId.HasValue)
                .ToList();

            var matches = unmatched
                .Select(line => new
                {
                    Line = line,
                    Candidates = transactions
                        .Where(transaction => IsCandidate(line, transaction))
                        .ToList()
                })
                .ToList();

            var count = 0;

            foreach (var match in matches)
            {
                if (match.Candidates.Count != 1)
                {
                    continue;
                }

                var transaction = match.Candidates[0];

                var competingLines = matches.Count(other =>
                    other.Candidates.Any(candidate =>
                        candidate.Id == transaction.Id));

                if (competingLines != 1)
                {
                    continue;
                }

                match.Line.MatchedBankTransactionId = transaction.Id;
                match.Line.MatchedDate = DateTime.UtcNow;
                count++;
            }

            return count;
        }
    }
}
