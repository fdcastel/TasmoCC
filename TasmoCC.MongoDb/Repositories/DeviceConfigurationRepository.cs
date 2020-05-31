using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.MongoDb.Repositories
{
    public class DeviceConfigurationRepository
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<DeviceConfiguration> _deviceConfigurationCollection;

        public DeviceConfigurationRepository(IMongoDatabase database)
        {
            _database = database;
            _deviceConfigurationCollection = _database.GetCollection<DeviceConfiguration>("deviceConfiguration");
        }

        public async Task<DeviceConfiguration> GetDeviceConfigurationAsync(string _id)
        {
            var filter = new FilterDefinitionBuilder<DeviceConfiguration>().Eq(d => d._id, _id);
            var cursor = await _deviceConfigurationCollection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<DeviceConfiguration> ReplaceDeviceConfigurationAsync(DeviceConfiguration deviceConfiguration)
        {
            var filter = new FilterDefinitionBuilder<DeviceConfiguration>().Eq(d => d._id, deviceConfiguration._id);
            var options = new FindOneAndReplaceOptions<DeviceConfiguration>() { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _deviceConfigurationCollection.FindOneAndReplaceAsync(filter, deviceConfiguration, options);
        }

        public async Task<DeviceConfiguration> DeleteDeviceConfigurationAsync(string id)
        {
            var filter = new FilterDefinitionBuilder<DeviceConfiguration>().Eq(d => d._id, id);
            return await _deviceConfigurationCollection.FindOneAndDeleteAsync(filter);
        }

        public IObservable<DocumentChange<DeviceConfiguration>> WhenDeviceConfigurationChanges(CancellationToken cancellationToken = default) =>
            _deviceConfigurationCollection
                .WhenCollectionChanges(cancellationToken)
                .Select(change => change.ToDocumentChange());

        public async Task InsertInitialDeviceConfigurationsAsync(IEnumerable<DeviceConfiguration>? values) =>
            await _deviceConfigurationCollection.InsertManyIgnoringErrorsAsync(values);
    }
}
