using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.MongoDb.Repositories
{
    public class TemplateRepository
    {
        public static readonly string GenericDecription = "Generic";

        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Template> _templateCollection;

        public TemplateRepository(IMongoDatabase database)
        {
            _database = database;
            _templateCollection = _database.GetCollection<Template>("template");
        }

        public dynamic GetTemplatesAggregate(string? _id = null)
        {
            return _templateCollection
                .Aggregate()
                .ToList();
        }

        public async Task InsertInitialTemplatesAsync(IEnumerable<Template>? values)
        {
            await _templateCollection.InsertManyIgnoringErrorsAsync(values);

            // Adds generic template
            var filter = new FilterDefinitionBuilder<Template>().Eq(t => t._id, GenericDecription);
            var update = new BsonDocument { { "$setOnInsert", new BsonDocument("_id", GenericDecription) } };
            var options = new FindOneAndUpdateOptions<Template>() { IsUpsert = true };

            await _templateCollection.FindOneAndUpdateAsync(filter, update, options);
        }
    }
}
