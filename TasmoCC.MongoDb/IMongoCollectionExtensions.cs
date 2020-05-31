using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TasmoCC.MongoDb
{
    public static class IMongoCollectionExtensions
    {
        public static async Task InsertManyIgnoringErrorsAsync<T>(this IMongoCollection<T> collection, IEnumerable<T>? values)
        {
            if (values != null)
            {
                var insertManyOptions = new InsertManyOptions() { IsOrdered = false };       // continue on error
                try
                {
                    await collection.InsertManyAsync(values, insertManyOptions);
                }
                catch (MongoBulkWriteException)
                {
                    // Nop
                }
            }
        }

        public static IObservable<ChangeStreamDocument<T>> WhenCollectionChanges<T>(this IMongoCollection<T> collection, CancellationToken cancellationToken = default, params ChangeStreamOperationType[] operationTypes)
        {
            if (operationTypes.Length == 0)
            {
                operationTypes = new[] { ChangeStreamOperationType.Insert, ChangeStreamOperationType.Update, ChangeStreamOperationType.Replace, ChangeStreamOperationType.Delete };
            }

            // { operationType: { $in: [...] } }
            var filter = new FilterDefinitionBuilder<ChangeStreamDocument<T>>()
                .In(d => d.OperationType, operationTypes);

            var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<T>>()
                .Match(filter);

            var options = new ChangeStreamOptions() { FullDocument = ChangeStreamFullDocumentOption.UpdateLookup };
            var changeStreamCursor = collection.Watch(pipelineDefinition, options, cancellationToken);

            return Observable.Create<ChangeStreamDocument<T>>(observer =>
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        if (await changeStreamCursor.MoveNextAsync(cancellationToken))
                        {
                            foreach (var c in changeStreamCursor.Current)
                            {
                                observer.OnNext(c);
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }, cancellationToken);

                return Disposable.Create(() => changeStreamCursor.Dispose());
            });
        }
    }
}
