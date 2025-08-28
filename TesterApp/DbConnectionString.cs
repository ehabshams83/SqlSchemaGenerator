namespace TesterApp;

internal class DbConnectionString
{
    static string server = @".\SqlExpress";
    static string databaseName = "SqlSchemaGeneratorTestDb";
    public static string connectionString =>
        $"Server={server};Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=false;TrustServerCertificate=True;";

}
