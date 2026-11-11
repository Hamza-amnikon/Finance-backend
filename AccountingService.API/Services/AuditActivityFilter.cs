using System.ComponentModel.DataAnnotations;
using AccountingService.API.Data;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public static class AuditActivityFilter
    {
        // Only timestamp-only synchronization updates are technical. Financial changes remain visible.
        private const string Technical = """
            e.EntityType = N'PaymentBankingLink' AND e.Action = N'Updated'
            AND e.ActorKind = N'System'
            AND ISJSON(ISNULL(e.BeforeJson, N'')) = 1 AND ISJSON(ISNULL(e.AfterJson, N'')) = 1
            AND NOT EXISTS
            (
                SELECT changes.[key], changes.[value], changes.[type]
                FROM
                (
                    SELECT [key] COLLATE Latin1_General_100_BIN2 AS [key],
                        [value] COLLATE Latin1_General_100_BIN2 AS [value], [type]
                    FROM OPENJSON(CASE WHEN e.AfterJson COLLATE Latin1_General_100_BIN2 =
                (LEFT(e.BeforeJson, LEN(e.BeforeJson) - 1) +
                N',"Other fields changed (values excluded)":"NextAttemptAtUtc, UpdatedAtUtc"' +
                NCHAR(125)) COLLATE Latin1_General_100_BIN2 THEN NCHAR(123) + NCHAR(125)
                        WHEN ISJSON(e.BeforeJson) = 1 THEN e.BeforeJson ELSE NCHAR(123) + NCHAR(125) END)
                    UNION ALL
                    SELECT [key] COLLATE Latin1_General_100_BIN2,
                        [value] COLLATE Latin1_General_100_BIN2, [type]
                    FROM OPENJSON(CASE WHEN e.AfterJson COLLATE Latin1_General_100_BIN2 =
                (LEFT(e.BeforeJson, LEN(e.BeforeJson) - 1) +
                N',"Other fields changed (values excluded)":"NextAttemptAtUtc, UpdatedAtUtc"' +
                NCHAR(125)) COLLATE Latin1_General_100_BIN2 THEN NCHAR(123) + NCHAR(125)
                        WHEN ISJSON(e.AfterJson) = 1 THEN e.AfterJson ELSE NCHAR(123) + NCHAR(125) END)
                ) changes
                WHERE changes.[key] NOT IN
                    (N'Other fields changed (values excluded)', N'NextAttemptAtUtc', N'UpdatedAtUtc')
                GROUP BY changes.[key], changes.[value], changes.[type]
                HAVING COUNT_BIG(*) <> 2
            )
            AND NOT EXISTS
            (
                SELECT 1 FROM STRING_SPLIT(
                    ISNULL(JSON_VALUE(CASE WHEN e.AfterJson COLLATE Latin1_General_100_BIN2 =
                (LEFT(e.BeforeJson, LEN(e.BeforeJson) - 1) +
                N',"Other fields changed (values excluded)":"NextAttemptAtUtc, UpdatedAtUtc"' +
                NCHAR(125)) COLLATE Latin1_General_100_BIN2 THEN NCHAR(123) + NCHAR(125) WHEN ISJSON(e.AfterJson) = 1 THEN e.AfterJson ELSE NCHAR(123) + NCHAR(125) END, '$."Other fields changed (values excluded)"'), N''), N',') item
                WHERE LTRIM(RTRIM(item.[value])) NOT IN (N'', N'NextAttemptAtUtc', N'UpdatedAtUtc')
            )
            """;

        public static IQueryable<AuditEvent> Apply(AccountingDbContext context, string? activity)
        {
            return activity switch
            {
                null or "" or "All" => context.AuditEvents.AsNoTracking(),
                "Business" => context.AuditEvents.FromSqlRaw(
                    "SELECT e.* FROM audit.Events e WHERE e.Action <> N'Snapshot' AND NOT (" + Technical + ")"
                ).AsNoTracking(),
                "Technical" => context.AuditEvents.FromSqlRaw(
                    "SELECT e.* FROM audit.Events e WHERE " + Technical
                ).AsNoTracking(),
                "Snapshots" => context.AuditEvents.AsNoTracking().Where(row => row.Action == "Snapshot"),
                _ => throw new ValidationException("Select Business, Technical, Snapshots or All activity.")
            };
        }
    }
}
