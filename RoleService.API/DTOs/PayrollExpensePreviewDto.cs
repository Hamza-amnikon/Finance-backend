namespace RoleService.API.DTOs;

public sealed record PayrollExpensePreviewDto(
    int PayrollRunId,
    int Year,
    int Month,
    string Currency,
    decimal NetSalary);
