namespace TasmoCC.MongoDb.Models
{
    public class Template : IMongoDbDocument
    {
        public string _id { get; set; } = default!;

        public string Definition { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
