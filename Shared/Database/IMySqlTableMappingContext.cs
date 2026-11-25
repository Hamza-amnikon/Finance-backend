namespace SparkFinance.Database
{
    public interface IMySqlTableMappingContext
    {
        IReadOnlyDictionary<string, string> MySqlTableMappings { get; }
    }
}
