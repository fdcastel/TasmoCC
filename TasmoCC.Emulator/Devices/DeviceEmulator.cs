using Newtonsoft.Json;
using System;
using System.Dynamic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TasmoCC.Mqtt.Configuration;
using TasmoCC.Mqtt.Models;
using TasmoCC.Mqtt.Services;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Emulator.Devices
{
    public class DeviceEmulator : IDisposable
    {
        public static readonly MqttConfiguration DefaultConfiguration = new MqttConfiguration
        {
            Host = "",
            Port = 1883,
            Username = "DVES_USER",
            Password = string.Empty
        };

        public static readonly int DefaultSetOption3 = 1;
        public static readonly int DefaultSetOption19 = 0;
        public static readonly int DefaultSetOption59 = 0;

        public static TimeSpan RestartDelayBefore => TimeSpan.FromMilliseconds(UseQuickRestart ? 200 : 2000);
        public static TimeSpan RestartDelayAfter => TimeSpan.FromMilliseconds(UseQuickRestart ? 600 : 6000);
        public static bool UseQuickRestart = false;

        public TasmotaStatus Status { get; private set; }

        // Extra states
        private long _isOffline = 0;
        public bool IsOffline
        {
            get => Interlocked.Read(ref _isOffline) == 1;
            set
            {
                if (value)
                {
                    Interlocked.CompareExchange(ref _isOffline, 1, 0);
                }
                else
                {
                    Interlocked.CompareExchange(ref _isOffline, 0, 1);
                };
            }
        }

        public MqttConfiguration MqttConfiguration { get; set; } = DefaultConfiguration;

        public int SetOption3 { get; set; } = DefaultSetOption3;
        public int SetOption19 { get; set; } = DefaultSetOption19;
        public int SetOption59 { get; set; } = DefaultSetOption59;

        // Helper properties
        public int ChannelCount => Status.Status.FriendlyName.Length;
        public string DefaultTopic =>
            "tasmota_" + Status.StatusNet.Mac.Substring(9, 8).Replace(":", "").ToUpper();
        public IPAddress IpAddress { get => IPAddress.Parse(Status.StatusNet.IpAddress); }
        public string MacAddress { get => Status.StatusNet.Mac.ToLower(); }
        public string Topic { get => Status.Status.Topic; set => Status.Status.Topic = value; }
        public int TelePeriod { get => Status.StatusLog.TelePeriod; set => Status.StatusLog.TelePeriod = value; }
        public string? TemplateName => JsonConvert.DeserializeObject<dynamic>(Status.Template)?.NAME;

        public DateTime StartedAt { get; private set; }
        public TimeSpan Uptime => DateTime.Now - StartedAt;

        public event EventHandler DeviceChanged = default!;

        private readonly IMqttClient _client;
        private Timer? _telemetryTimer;
        private bool _disposed = false;

        public DeviceEmulator(TasmotaStatus status, IMqttClient client)
        {
            Status = status;
            _client = client;

            Startup();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _telemetryTimer?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        protected virtual void OnDeviceChanged(EventArgs e) => DeviceChanged?.Invoke(this, e);

        public dynamic? ExecuteCommand(string command, string? parameters)
        {
            var fullCommand = $"{command} {parameters}".Trim();
            return ExecuteCommand(fullCommand);
        }

        public dynamic? ExecuteCommand(string? fullCommand)
        {
            dynamic? result = null;
            if (IsOffline)
            {
                return result;
            }

            var separators = new[] { ';', ' ' };
            var tokens = fullCommand?.Split(separators, StringSplitOptions.RemoveEmptyEntries) ?? new string[] { };

            var nextTokenIndex = 0;
            string? NextToken(bool isCommand = false)
            {
                var result = nextTokenIndex < tokens.Length ? tokens[nextTokenIndex++] : null;
                return isCommand ? result?.ToLower() : result;
            };

            var requiresRestart = false;
            try
            {
                var nextCommand = NextToken(true);
                while (nextCommand != null)
                {
                    result = new ExpandoObject();
                    switch (nextCommand)
                    {
                        case "backlog":
                            result.Backlog = "Empty";
                            break;

                        case "delay":
                            var delay = NextToken();
                            result.Delay = 0;
                            if (delay != null && int.TryParse(delay, out var d))
                            {
                                result.Delay = d;
                            }
                            break;

                        case "module":
                            var newModule = NextToken();
                            if (newModule != null && newModule == "0")
                            {
                                requiresRestart = true;
                            }
                            result.Module = JsonConvert.DeserializeObject<dynamic>("{\"Module\":{\"0\":\"" + TemplateName + "\"}}");
                            break;

                        case "mqtthost":
                            var newMqttHost = NextToken();
                            if (newMqttHost != null)
                            {
                                MqttConfiguration.Host = newMqttHost == "1" ? DefaultConfiguration.Host : newMqttHost;
                                requiresRestart = true;
                            }
                            result.MqttHost = MqttConfiguration.Host;
                            break;

                        case "mqttport":
                            var newMqttPort = NextToken();
                            if (newMqttPort != null && int.TryParse(newMqttPort, out var p))
                            {
                                MqttConfiguration.Port = p == 1 ? DefaultConfiguration.Port : p;
                                requiresRestart = true;
                            }
                            result.MqttPort = MqttConfiguration.Port;
                            break;

                        case "mqttuser":
                            var newMqttUser = NextToken();
                            if (newMqttUser != null)
                            {
                                MqttConfiguration.Username = newMqttUser == "1" ? DefaultConfiguration.Username : newMqttUser;
                                requiresRestart = true;
                            }
                            result.MqttUser = MqttConfiguration.Username;
                            break;

                        case "mqttpassword":
                            var newMqttPassword = NextToken();
                            if (newMqttPassword != null)
                            {
                                MqttConfiguration.Password = newMqttPassword == "1" ? DefaultConfiguration.Password : newMqttPassword;
                                requiresRestart = true;
                            }
                            result.MqttPassword = "****";
                            break;

                        case "power":
                        case "power0":
                        case "power1":
                        case "power2":
                        case "power3":
                        case "power4":
                            var powerIndex = nextCommand != "power" ? Convert.ToInt32(nextCommand.Substring(5, 1)) : (int?)null;
                            if (powerIndex > ChannelCount)
                            {
                                result.Command = "Error";
                                return result;
                            }

                            var newPower = NextToken()?.ToUpper();
                            if (newPower != null)
                            {
                                switch (newPower)
                                {
                                    case "0": newPower = "OFF"; break;
                                    case "1": newPower = "ON"; break;
                                    case "2": newPower = "TOGGLE"; break;
                                }

                                switch (newPower)
                                {
                                    case "OFF":
                                    case "ON":
                                        if (ChannelCount == 1)
                                        {
                                            Status.StatusSts.Power = newPower;
                                        }
                                        else
                                        {
                                            if (powerIndex == 0 || powerIndex == 1) { Status.StatusSts.Power1 = newPower; }
                                            if (powerIndex == 0 || powerIndex == 2) { Status.StatusSts.Power2 = newPower; }
                                            if (powerIndex == 0 || powerIndex == 3) { Status.StatusSts.Power3 = newPower; }
                                            if (powerIndex == 0 || powerIndex == 4) { Status.StatusSts.Power4 = newPower; }
                                        }
                                        break;

                                    case "TOGGLE":
                                        if (ChannelCount == 1)
                                        {
                                            Status.StatusSts.Power = TogglePower(Status.StatusSts.Power!);
                                        }
                                        else
                                        {
                                            if (powerIndex == 0 || powerIndex == 1) { Status.StatusSts.Power1 = TogglePower(Status.StatusSts.Power1!); }
                                            if (powerIndex == 0 || powerIndex == 2) { Status.StatusSts.Power2 = TogglePower(Status.StatusSts.Power2!); }
                                            if (powerIndex == 0 || powerIndex == 3) { Status.StatusSts.Power3 = TogglePower(Status.StatusSts.Power3!); }
                                            if (powerIndex == 0 || powerIndex == 4) { Status.StatusSts.Power4 = TogglePower(Status.StatusSts.Power4!); }
                                        }
                                        break;
                                }

                                OnDeviceChanged(new EventArgs());
                                SendTelemetry();
                            }

                            if (ChannelCount == 1)
                            {
                                result.POWER = Status.StatusSts.Power;
                            }
                            else
                            {
                                if (powerIndex == 0 || powerIndex == 1) { result.POWER1 = Status.StatusSts.Power1; }
                                if (powerIndex == 0 || powerIndex == 2) { result.POWER2 = Status.StatusSts.Power2; }
                                if (powerIndex == 0 || powerIndex == 3) { result.POWER3 = Status.StatusSts.Power3; }
                                if (powerIndex == 0 || powerIndex == 4) { result.POWER4 = Status.StatusSts.Power4; }
                            }
                            break;

                        case "publish":
                            var topic = NextToken();
                            if (topic == null)
                            {
                                result.Command = "Error";
                                return result;
                            }
                            var payload = NextToken();
                            Publish(new MqttMessage()
                            {
                                Topic = topic,
                                Payload = payload
                            });
                            result = new { }; // Publish returns empty result
                            break;

                        case "restart":
                            var restart = NextToken();
                            result.Restart = "1 to restart";
                            if (restart != null && int.TryParse(restart, out var r))
                            {
                                if (r == 1)
                                {
                                    result.Restart = "Restarting";
                                    requiresRestart = true;
                                }
                            }
                            break;

                        case "setoption3":
                            var newOption3 = NextToken();
                            if (newOption3 != null && int.TryParse(newOption3, out var o3))
                            {
                                SetOption3 = o3;
                                TelePeriodChanged();
                                break;
                            }
                            result.SetOption19 = OptionAsString(SetOption19);
                            break;

                        case "setoption19":
                            var newOption19 = NextToken();
                            if (newOption19 != null && int.TryParse(newOption19, out var o19))
                            {
                                SetOption19 = o19;
                                break;
                            }
                            result.SetOption19 = OptionAsString(SetOption19);
                            break;

                        case "setoption59":
                            var newOption59 = NextToken();
                            if (newOption59 != null && int.TryParse(newOption59, out var o59))
                            {
                                SetOption59 = o59;
                                TelePeriodChanged();
                                break;
                            }
                            result.SetOption59 = OptionAsString(SetOption59);
                            break;

                        case "state":
                            SendTelemetry();
                            result = Status.StatusSts;
                            break;

                        case "status":
                            int statusKind;
                            if (!int.TryParse(NextToken(), out statusKind))
                            {
                                statusKind = -1;
                            };

                            switch (statusKind)
                            {
                                case 0: result = Status; break;
                                case 1: result.StatusPRM = Status.StatusPrm; break;
                                case 2: result.StatusFWR = Status.StatusFwr; break;
                                case 3: result.StatusLOG = Status.StatusLog; break;
                                case 4: result.StatusMEM = Status.StatusMem; break;
                                case 5: result.StatusNET = Status.StatusNet; break;
                                case 11: result.StatusSTS = Status.StatusSts; break;
                                default: result.Status = Status.Status; break;
                            }
                            break;

                        case "teleperiod":
                            var newTelePeriod = NextToken();
                            if (newTelePeriod != null && int.TryParse(newTelePeriod, out var tp))
                            {
                                if (0 <= tp && tp <= 3600)
                                {
                                    if (tp == 1)
                                    {
                                        tp = 300;
                                    }
                                    else if (tp < 10)
                                    {
                                        tp = 10;
                                    }
                                    TelePeriod = tp;
                                    TelePeriodChanged();
                                    break;
                                }
                            }
                            result.TelePeriod = TelePeriod;
                            break;

                        case "template":
                            var newTemplate = NextToken();
                            if (newTemplate != null)
                            {
                                Status.Template = newTemplate;
                                break;
                            }
                            result = JsonConvert.DeserializeObject<dynamic>(Status.Template);
                            break;

                        case "topic":
                            var newTopic = NextToken();
                            if (newTopic != null)
                            {
                                Topic = newTopic == "1" ? DefaultTopic : newTopic;
                                requiresRestart = true;
                                break;
                            }
                            result.Topic = Topic;
                            break;

                        default:
                            result.Command = "Unknown";
                            return result;
                    }

                    nextCommand = NextToken(true);
                }

                if (result == null)
                {
                    result = new ExpandoObject();
                    result.WARNING = "Enter command cmnd=";
                    return result;
                }

                return result;
            }
            finally
            {
                if (requiresRestart)
                {
                    Restart();
                }
            }
        }

        public void Publish(MqttMessage message)
        {
            if (!String.IsNullOrWhiteSpace(MqttConfiguration.Host))
            {
                _client.PublishMessageAsync(message);
            }
        }

        public void Restart()
        {
            Task.Run(() =>
            {
                _client.Stop();

                Thread.Sleep(RestartDelayBefore);
                IsOffline = true;
                Thread.Sleep(RestartDelayAfter);
                IsOffline = false;

                Startup();
            });
        }

        public void Startup()
        {
            StartedAt = DateTime.Now;

            _client.Stop();
            if (!String.IsNullOrWhiteSpace(MqttConfiguration.Host))
            {
                _client.Start(MqttConfiguration);
            }

            TelePeriodChanged();
        }

        private static string OptionAsString(int value) => value == 0 ? "OFF" : "ON";

        private void SendTelemetry()
        {
            if (SetOption59 == 1)
            {
                Publish(new MqttMessage()
                {
                    Topic = $"tele/{Topic}/STATE",
                    Payload = JsonConvert.SerializeObject(Status.StatusSts)
                });
            }
        }

        private void TelePeriodChanged()
        {
            _telemetryTimer?.Dispose();
            _telemetryTimer = null;

            if (SetOption3 == 1 && SetOption59 == 1 && TelePeriod != 0)
            {
                _telemetryTimer = new Timer((_) => SendTelemetry(), null, TelePeriod * 1000, TelePeriod * 1000);
            }
        }

        private static string TogglePower(string power) => power == "ON" ? "OFF" : "ON";
    }
}
