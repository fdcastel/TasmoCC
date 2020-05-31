namespace TasmoCC.MongoDb.Models
{
    public enum DeviceState
    {
        AdoptionPending,
        Adopting,
        ProvisionPending,
        Provisioning,
        Connected,
        Restarting,
        Upgrading
    }
}
