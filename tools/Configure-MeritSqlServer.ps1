param(
    [string]$Server = "172.16.3.10",
    [int]$Port = 1433,
    [string]$Database = "SparkFinance",
    [string]$UserId = "sa",
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = "Stop"

$repository = Split-Path $PSScriptRoot -Parent
$password = Read-Host "SQL Server password for $UserId" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)

try
{
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connectionString = "Server=$Server,$Port;Database=$Database;User ID=$UserId;Password=$plainPassword;Encrypt=True;TrustServerCertificate=$($TrustServerCertificate.IsPresent);Connect Timeout=30;"

    $projects = Get-ChildItem $repository -Directory -Filter "*Service.API" |
        ForEach-Object { Get-ChildItem $_.FullName -Filter "*.csproj" -File } |
        Where-Object { Select-String -Path $_.FullName -Pattern "<UserSecretsId>" -Quiet }

    foreach ($project in $projects)
    {
        & dotnet user-secrets set "Database:Provider" "SqlServer" --project $project.FullName
        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not configure the SQL Server provider for $($project.BaseName)."
        }

        & dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString --project $project.FullName
        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not configure the SQL Server connection for $($project.BaseName)."
        }

        Write-Output "Configured $($project.BaseName)."
    }
}
finally
{
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $plainPassword = $null
    $connectionString = $null
}
