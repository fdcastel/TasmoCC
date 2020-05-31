namespace TasmoCC.MongoDb.Configuration
{
    public class MongoDbConfiguration
    {
        public static readonly string DefaultDatabase = "tasmocc";

        public string ConnectionString { get; set; } = default!;
        public string Database { get; set; } = DefaultDatabase;
    }
}
