SET NOCOUNT ON; SELECT COUNT(*) BusinessCount FROM audit.Events e WHERE e.Action<>N'Snapshot' AND NOT (
            e.EntityType = N'PaymentBankingLink' AND e.Action = N'Updated'
            AND e.ActorKind = N'System'
            AND ISJSON(ISNULL(e.BeforeJson, N'')) = 1 AND ISJSON(ISNULL(e.AfterJson, N'')) = 1
            AND NOT EXISTS
            (
                SELECT 1 FROM OPENJSON(CASE WHEN ISJSON(e.BeforeJson) = 1 THEN e.BeforeJson ELSE N'{{}}' END) b
                FULL JOIN OPENJSON(CASE WHEN ISJSON(e.AfterJson) = 1 THEN e.AfterJson ELSE N'{{}}' END) a ON a.[key] = b.[key]
                WHERE COALESCE(a.[key], b.[key]) NOT IN
                    (N'Other fields changed (values excluded)', N'NextAttemptAtUtc', N'UpdatedAtUtc')
                AND (a.[key] IS NULL OR b.[key] IS NULL OR a.[type] <> b.[type]
                    OR ISNULL(a.[value], N'') COLLATE Latin1_General_100_BIN2 <>
                       ISNULL(b.[value], N'') COLLATE Latin1_General_100_BIN2)
            )
            AND NOT EXISTS
            (
                SELECT 1 FROM STRING_SPLIT(
                    ISNULL(JSON_VALUE(CASE WHEN ISJSON(e.AfterJson) = 1 THEN e.AfterJson ELSE N'{{}}' END, '$."Other fields changed (values excluded)"'), N''), N',') item
                WHERE LTRIM(RTRIM(item.[value])) NOT IN (N'', N'NextAttemptAtUtc', N'UpdatedAtUtc')
            )
            );