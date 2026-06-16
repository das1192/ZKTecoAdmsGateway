using System.Collections.Concurrent;
using ZKTecoGateway.Config;
using ZKTecoGateway.Models;

namespace ZKTecoGateway.Services
{
    /// <summary>
    /// Tracks all devices that have checked in with this gateway,
    /// maps serial numbers to client configs, and records last-seen timestamps.
    /// Thread-safe: used from concurrent HTTP requests.
    /// </summary>
    public class DeviceRegistryService
    {
        private readonly ILogger<DeviceRegistryService> _logger;
        private readonly GatewayConfig _config;

        // SN -> DeviceInfo (devices seen at runtime)
        private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();

        // SN -> ClientConfig (built from appsettings, refreshed when config reloads)
        private readonly Dictionary<string, ClientConfig> _snToClient = new();

        public DeviceRegistryService(ILogger<DeviceRegistryService> logger, GatewayConfig config)
        {
            _logger = logger;
            _config = config;
            BuildSnMap();
        }

        private void BuildSnMap()
        {
            _snToClient.Clear();
            foreach (var client in _config.Clients)
                foreach (var sn in client.DeviceSerialNumbers)
                    _snToClient[sn.ToUpperInvariant()] = client;
        }

        /// <summary>Register or refresh a device. Returns the matched client config (or null).</summary>
        public ClientConfig? RegisterDevice(string sn, Dictionary<string, string> options)
        {
            var key = sn.ToUpperInvariant();
            var info = _devices.AddOrUpdate(key,
                _ => new DeviceInfo
                {
                    SerialNumber = sn,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    Options = options,
                    ClientId = _snToClient.TryGetValue(key, out var c) ? c.ClientId : "UNKNOWN"
                },
                (_, existing) =>
                {
                    existing.LastSeen = DateTime.UtcNow;
                    existing.Options = options;
                    return existing;
                });

            if (_snToClient.TryGetValue(key, out var client))
            {
                _logger.LogInformation("Device {SN} registered → client [{ClientId}] {ClientName}", sn, client.ClientId, client.ClientName);
                return client;
            }

            _logger.LogWarning("Device {SN} is not mapped to any client in appsettings.json", sn);
            return null;
        }

        /// <summary>Touch last-seen for an already-known device.</summary>
        //public void Heartbeat(string sn)
        //{
        //    _logger.LogInformation("Heartbeat {SN}", sn);
        //    if (_devices.TryGetValue(sn.ToUpperInvariant(), out var info))
        //        info.LastSeen = DateTime.UtcNow;
        //}


        public void Heartbeat(string sn)
        {
            var key = sn.ToUpperInvariant();

            _devices.AddOrUpdate(
                key,
                _ => new DeviceInfo
                {
                    SerialNumber = sn,
                    RegisteredAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    ClientId = _snToClient.TryGetValue(key, out var client)
                        ? client.ClientId
                        : "UNKNOWN"
                },
                (_, existing) =>
                {
                    existing.LastSeen = DateTime.UtcNow;
                    return existing;
                });

            _logger.LogInformation("Heartbeat {SN}", sn);
        }

        /// <summary>Resolve a serial number to its client config.</summary>
        public ClientConfig? GetClient(string sn) =>
            _snToClient.TryGetValue(sn.ToUpperInvariant(), out var c) ? c : null;

        public IEnumerable<DeviceInfo> GetAllDevices() => _devices.Values;

        /// <summary>Devices that sent a heartbeat in the last 5 minutes are considered online.</summary>
        public bool IsOnline(string sn) =>
          
            _devices.TryGetValue(sn.ToUpperInvariant(), out var d)
            && (DateTime.UtcNow - d.LastSeen).TotalMinutes < 5;

    }
}
