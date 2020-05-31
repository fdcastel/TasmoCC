namespace TasmoCC.MongoDb.Models
{
    public enum DocumentChangeKind
    {
        Insert,
        Update,
        Delete,
        Replace
    }

    public class DocumentChange<T>
    {
        public T Document { get; private set; }
        public DocumentChangeKind ChangeKind { get; private set; }

        public DocumentChange(T document, DocumentChangeKind changeKind)
        {
            Document = document;
            ChangeKind = changeKind;
        }
    }

}
