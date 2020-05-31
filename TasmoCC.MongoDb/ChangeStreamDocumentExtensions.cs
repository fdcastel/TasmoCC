using MongoDB.Driver;
using System.ComponentModel;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.MongoDb
{
    public static class ChangeStreamDocumentExtensions
    {
        public static DocumentChange<T> ToDocumentChange<T>(this ChangeStreamDocument<T> change)
            where T : IMongoDbDocument, new()
        {
            if (change.OperationType == ChangeStreamOperationType.Delete)
            {
                var oldDevice = new T()
                {
                    _id = change.DocumentKey["_id"].AsString
                };
                return new DocumentChange<T>(oldDevice, DocumentChangeKind.Delete);
            }

            var device = change.FullDocument;
            var changeKind = change.OperationType switch
            {
                ChangeStreamOperationType.Insert => DocumentChangeKind.Insert,
                ChangeStreamOperationType.Update => DocumentChangeKind.Update,
                ChangeStreamOperationType.Replace => DocumentChangeKind.Replace,
                _ => throw new InvalidEnumArgumentException($"[BUGCHECK] Unexpected ChangeStreamOperationType ({change.OperationType})."),
            };

            return new DocumentChange<T>(device, changeKind);
        }
    }
}
