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
            var predicate = activity switch
            {
                null or "" or "All" => "1 = 1",
                "Business" => "e.Action <> N'Snapshot' AND NOT (" + Technical + ")",
                "Technical" => Technical,
                "Snapshots" => "e.Action = N'Snapshot'",
                _ => throw new ValidationException("Select Business, Technical, Snapshots or All activity.")
            };

            // Display fallback uses the recorded company and a unique current mapping.
            // Original audit events, snapshots and recorded branches remain unchanged.
            var sql = """
                SELECT e.Id, e.AtUtc, e.Service, e.EntityType, e.EntityId,
                    e.Action, e.Actor, e.ActorKind,
                    CASE WHEN e.Service = N'BankingService.API'
                        AND NULLIF(LTRIM(RTRIM(e.Branch)), N'') IS NULL
                        THEN mapped.Name ELSE e.Branch END AS Branch,
                    e.CompanyId, e.CorrelationId, e.BeforeJson, e.AfterJson
                FROM audit.Events e
                LEFT JOIN
                (
                    SELECT AccountingCompanyId, MIN(Name) AS Name
                    FROM amnikon.Branches
                    GROUP BY AccountingCompanyId
                    HAVING COUNT(*) = 1
                ) mapped ON mapped.AccountingCompanyId = e.CompanyId
                WHERE
                """ + " " + predicate;

            return context.AuditEvents.FromSqlRaw(sql).AsNoTracking();
        }
    }
}