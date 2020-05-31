namespace TasmoCC.MongoDb.Models
{
    public class DeviceAggregate : Device
    {
        public DeviceConfiguration? Configuration { get; set; }
        public Template? Template { get; set; }
    }
}
