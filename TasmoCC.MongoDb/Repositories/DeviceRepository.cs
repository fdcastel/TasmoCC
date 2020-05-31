using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.MongoDb.Repositories
{
    public class DeviceRepository
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Device> _deviceCollection;

        public DeviceRepository(IMongoDatabase database)
        {
            _database = database;
            _deviceCollection = _database.GetCollection<Device>("device");
        }

        public IEnumerable<Device> GetDevices()
        {
            return _deviceCollection
                .Aggregate<Device>()
                .ToEnumerable();
        }

        public DeviceAggregate GetDeviceAggregate(string _id, bool useTemplateFromConfiguration = false)
        {
            return _deviceCollection
                .Aggregate<DeviceAggregate>(GetDevicesAggregatePipeline(_id, useTemplateFromConfiguration))
                .FirstOrDefault();
        }

        public IEnumerable<DeviceAggregate> GetDevicesAggregate()
        {
            return _deviceCollection
                .Aggregate<DeviceAggregate>(GetDevicesAggregatePipeline())
                .ToList();
        }

        private static List<BsonDocument> GetDevicesAggregatePipeline(string? _id = null, bool useTemplateFromConfiguration = false)
        {
            var pipeline = new List<BsonDocument>()
            {
                new BsonDocument{ { "$lookup", new BsonDocument {
                    { "from", "deviceConfiguration" },
                    { "localField", "_id" } ,
                    { "foreignField", "_id" } ,
                    { "as", "configuration" } } }
                },
                new BsonDocument{ { "$unwind", new BsonDocument {
                    {"path", "$configuration" },
                    {"preserveNullAndEmptyArrays", true } } }
                },
                new BsonDocument{ { "$lookup", new BsonDocument {
                    { "from", "template" },
                    { "localField", useTemplateFromConfiguration ? "configuration.templateName" : "templateName" } ,
                    { "foreignField", "_id" } ,
                    { "as", "template" } } }
                },
                new BsonDocument{ { "$unwind", new BsonDocument {
                    {"path", "$template" },
                    {"preserveNullAndEmptyArrays", true } } }
                },
            };

            if (_id != null)
            {
                var match = new BsonDocument { { "$match", new BsonDocument { { "_id", _id } } } };
                pipeline.Insert(0, match);
            }

            return pipeline;
        }

        public async Task<Device> GetDeviceAsync(string _id)
        {
            var filter = new FilterDefinitionBuilder<Device>().Eq(d => d._id, _id);
            var cursor = await _deviceCollection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<Device> GetDeviceFromTopicNameAsync(string topicName)
        {
            var filter = new FilterDefinitionBuilder<Device>().Eq(d => d.TopicName, topicName);
            var cursor = await _deviceCollection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<Device> UpdateDeviceAsync(Device device, bool isUpsert = false, params string[] fieldsToUnset)
        {
            device.UpdatedAt = DateTime.Now;

            var newValues = device.ToBsonDocument();
            foreach (var f in fieldsToUnset)
            {
                // Avoid "Updating the path would create a conflict at..." -- https://stackoverflow.com/a/50947773
                newValues.Remove(f);
            }

            var filter = new FilterDefinitionBuilder<Device>().Eq(d => d._id, device._id);
            var update = new BsonDocument { { "$set", newValues } };
            if (fieldsToUnset.Length > 0)
            {
                update.Add("$unset", new BsonDocument(fieldsToUnset.Select(f => new BsonElement(f, ""))));
            }
            var options = new FindOneAndUpdateOptions<Device>() { IsUpsert = isUpsert, ReturnDocument = ReturnDocument.After };

            return await _deviceCollection.FindOneAndUpdateAsync(filter, update, options);
        }

        public async Task<Device> UpdateDeviceStateAsync(string id, DeviceState? newState, params string[] fieldsToUnset)
        {
            var fieldsToUpdate = new Device()
            {
                _id = id,
                State = newState
            };

            if (newState == null)
            {
                fieldsToUnset = fieldsToUnset.Append("state").ToArray();
            }
            return await UpdateDeviceAsync(fieldsToUpdate, fieldsToUnset: fieldsToUnset);
        }

        public async Task<Device> DeleteDeviceAsync(string id)
        {
            var filter = new FilterDefinitionBuilder<Device>().Eq(d => d._id, id);
            return await _deviceCollection.FindOneAndDeleteAsync(filter);
        }

        public async Task SetDeviceOfflineAsync(IPAddress ipAddress, bool offline = true)
        {
            var filter = new FilterDefinitionBuilder<Device>().Eq(d => d.Ipv4Address, ipAddress);
            var update = offline
                ? new BsonDocument { { "$set", new BsonDocument { { "offline", true } } } }
                : new BsonDocument { { "$unset", new BsonDocument { { "offline", true } } } };

            await _deviceCollection.FindOneAndUpdateAsync(filter, update);
        }

        public async Task<long> SetDevicesOfflineAsync(DateTime notUpdatedSince)
        {
            var filter = new FilterDefinitionBuilder<Device>().Lt(d => d.UpdatedAt, notUpdatedSince);
            var update = new BsonDocument { { "$set", new BsonDocument { { "offline", true } } } };
            var options = new UpdateOptions() { IsUpsert = false };

            var updateResult = await _deviceCollection.UpdateManyAsync(filter, update, options);

            return updateResult.IsAcknowledged && updateResult.IsModifiedCountAvailable ? updateResult.MatchedCount : 0;
        }

        public void WaitForDeviceAdopted(string deviceId, Func<Task> adoptCommand, Action<bool, int> callback, CancellationToken cancellationToken = default)
        {
            // ToDo: use changestream instead of polling
            Task.Run(async () =>
            {
                var attempts = 0;
                var isAdopted = false;
                while (!isAdopted && attempts < 5)
                {
                    try
                    {
                        await adoptCommand();
                        Thread.Sleep(TimeSpan.FromSeconds(1));

                        var device = await GetDeviceAsync(deviceId);
                        isAdopted = device.AdoptedAt.HasValue;
                    }
                    catch (Exception)
                    {
                        isAdopted = false;
                    }

                    attempts++;

                    cancellationToken.ThrowIfCancellationRequested();
                }

                callback(isAdopted, attempts);
            }, cancellationToken);
        }

        public IObservable<DocumentChange<Device>> WhenDeviceChanges(CancellationToken cancellationToken = default) =>
            _deviceCollection
                .WhenCollectionChanges(cancellationToken)
                .Select(change => change.ToDocumentChange());
    }
}
