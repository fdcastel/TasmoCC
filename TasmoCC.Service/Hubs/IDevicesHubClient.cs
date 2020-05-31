using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;

namespace TasmoCC.Service.Hubs
{
    public interface IDevicesHubClient
    {
        Task ScanNetwork();

        Task Adopt(string id);
        Task Forget(string id);
        Task Provision(string id);
        Task Restart(string id);
        Task Upgrade(string id);

        Task SetPower(string id, int index, string state);

        Task DeviceChanged(dynamic deviceAggregate, DocumentChangeKind changeKind);
        Task InitialPayloadReceived(dynamic devices, dynamic templates);
        Task NetworkScanFinished();
    }
}
