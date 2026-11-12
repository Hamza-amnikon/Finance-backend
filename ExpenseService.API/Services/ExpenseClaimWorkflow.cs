using System.ComponentModel.DataAnnotations;

namespace ExpenseService.API.Services
{
    public static class ExpenseClaimWorkflow
    {
        public static bool IsOwner(string owner, string actor) =>
            string.Equals(owner.Trim(), actor.Trim(), StringComparison.OrdinalIgnoreCase);

        public static void Validate(string current, string next, string owner, string actor, bool admin, string reason)
        {
            if (string.IsNullOrWhiteSpace(actor))
            {
                throw new UnauthorizedAccessException("Select your working identity first.");
            }

            var owns = IsOwner(owner, actor);
            var allowed = (current, next) switch
            {
                ("Draft", "Submitted") or ("Draft", "Cancelled") => owns || admin,
                ("Rejected", "Draft") => owns || admin,
                ("Submitted", "Approved") => admin && !owns,
                ("Submitted", "Rejected") => admin,
                ("Approved", "Paid") => admin,
                _ => false
            };
            if (!allowed)
            {
                throw new InvalidOperationException("This status change is not allowed. A different administrator must approve the claim.");
            }

            if (next == "Rejected" && string.IsNullOrWhiteSpace(reason))
            {
                throw new ValidationException("A rejection reason is required.");
            }
        }
    }
}
