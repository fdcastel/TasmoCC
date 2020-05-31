using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.MongoDb.Models;
using TasmoCC.MongoDb.Repositories;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Service.Services;
using TasmoCC.Tasmota.Configuration;
using TasmoCC.Tasmota.Services;
using TasmoCC.Tests.Extensions;
using TasmoCC.Tests.Mocks;

namespace TasmoCC.Tests
{
    [TestClass]
    public class MasterServiceTests
    {
        public MqttConfiguration MqttConfiguration { get; }
        private readonly MockNetwork Network;
        public IMongoDatabase Database { get; private set; }
        public DeviceRepository DeviceRepository { get; }
        private readonly TasmotaService TasmotaService;
        public readonly MasterService MasterService;

        public MasterServiceTests()
        {
            MqttConfiguration = new MqttConfiguration()
            {
                Host = "192.168.66.200",
                Port = 64220,
                Username = "vader",
                Password = "jedi"
            };

            var configuration = new TasmotaConfiguration()
            {
                Subnet = IPAddress.Parse("192.168.66.0"),
            };

            Network = new MockNetwork(configuration.Subnet, 30, MqttConfiguration);

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var tasmotaClient = new MockTasmotaClient(Network);
            var mongoClient = new MongoClient("mongodb://localhost");
            Database = mongoClient.GetDatabase("tasmocc-test");
            DeviceRepository = new DeviceRepository(Database);
            var deviceConfigurationRepository = new DeviceConfigurationRepository(Database);

            TasmotaService = new TasmotaService(Options.Create(configuration), loggerFactory.CreateLogger<TasmotaService>(), tasmotaClient);
            MasterService = new MasterService(Options.Create(MqttConfiguration), loggerFactory.CreateLogger<MasterService>(), TasmotaService, DeviceRepository, deviceConfigurationRepository)
            {
                ScanNetworkOnStart = false
            };
        }

        [TestMethod]
        public async Task ScanNetwork_ShouldInsertDiscoveredDevicesOnDatabase()
        {
            Database.DropCollection("device");

            await MasterService.ScanNetwork();
            Thread.Sleep(1000);        // ToDo: Await not enough?

            var devices = DeviceRepository.GetDevices();
            Assert.AreEqual(Network.Devices.Count, devices.Count());
        }

        [TestMethod]
        [Ignore]
        public async Task Adopt_WhenDeviceOnline_ShouldAdoptAndProvisionDevice()
        {
            await MasterService.ScanNetwork();

            var device = Network.Devices[0];
            var dbDevice = await DeviceRepository.GetDeviceAsync(device.MacAddress);
            Assert.IsNotNull(device);

            Assert.AreNotEqual(DeviceState.Adopting, dbDevice.State);
            Assert.IsNull(dbDevice.AdoptedAt);
            Assert.IsNull(dbDevice.ProvisionedAt);
            await MasterService.AdoptAsync(dbDevice._id);

            dbDevice = await DeviceRepository.GetDeviceAsync(device.MacAddress);
            Assert.AreEqual(DeviceState.Adopting, dbDevice.State);
            Assert.IsNull(dbDevice.AdoptedAt);

            device.AssertDeviceRestart();

            // Todo: Finish
        }
    }
}
