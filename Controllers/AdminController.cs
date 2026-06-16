using Microsoft.AspNetCore.Mvc;
using ZKTecoGateway.Config;
using ZKTecoGateway.Models;
using ZKTecoGateway.Services;

namespace ZKTecoGateway.Controllers
{
    [ApiController]
    [Route("api")]
    public class AdminController : ControllerBase
    {
        private readonly DeviceRegistryService _registry;
        private readonly ForwardingService _forwarder;
        private readonly DeviceStateService _state;
        private readonly GatewayConfig _config;

        public AdminController(
            DeviceRegistryService registry,
            ForwardingService forwarder,
            DeviceStateService state,
            GatewayConfig config)
        {
            _registry = registry;
            _forwarder = forwarder;
            _state = state;
            _config = config;
        }

        // ── Status ───────────────────────────────────────────────────────────

        [HttpGet("status")]
        public IActionResult Status()
        {
            var devices = BuildDeviceList();
            return Ok(new
            {
                serverTime = DateTime.UtcNow,
                totalDevices = devices.Count,
                onlineDevices = devices.Count(d => d.IsOnline),
                devices,
                recentActivity = _state.GetAllHistory().Take(30)
            });
        }

        // ── Device list with state ────────────────────────────────────────────

        [HttpGet("devices")]
        public IActionResult Devices() => Ok(BuildDeviceList());

        // ── Manual Pull ───────────────────────────────────────────────────────

        [HttpPost("pull/{sn}")]
        public IActionResult Pull(string sn)
        {
            if (_registry.GetClient(sn) == null)
                return NotFound(new { success = false, message = $"Device {sn} not configured" });

            if (!_registry.IsOnline(sn))
                return BadRequest(new { success = false, message = $"Device {sn} is offline" });

            _state.RequestPull(sn);  // sets pending = true, initialDone = false
            _state.SetStamp(sn, 0);  // reset stamp so we get ALL records

            return Ok(new { success = true, message = "Pull queued — data will arrive within 10 seconds" });
        }

        [HttpPost("pull-all")]
        public IActionResult PullAll()
        {
            var online = BuildDeviceList().Where(d => d.IsOnline).ToList();
            foreach (var d in online)
                _state.RequestPull(d.SerialNumber);

            return Ok(new { success = true, message = $"Pull queued for {online.Count} online device(s)", count = online.Count });
        }

        // ── Auto Pull Toggle ──────────────────────────────────────────────────

        [HttpPost("auto/{sn}/{mode}")]
        public IActionResult SetAuto(string sn, string mode)
        {

            Console.WriteLine($"AUTO HIT: {sn} {mode}");
            if (_registry.GetClient(sn) == null)
                return NotFound(new { success = false, message = $"Device {sn} not configured" });

            bool enable = mode.ToLower() == "on";
            _state.SetAutoPull(sn, enable);

            return Ok(new
            {
                success = true,
                serialNumber = sn,
                autoPull = enable,
                message = enable
                    ? "Auto-pull ON — device will upload logs on every connection"
                    : "Auto-pull OFF — use manual pull button to fetch data"
            });
        }

        [HttpGet("hello")]
        public IActionResult Hello()
        {
            return Ok("hello");
        }

        // ── Pull History for one device ───────────────────────────────────────

        [HttpGet("history/{sn}")]
        public IActionResult History(string sn) =>
            Ok(_state.GetHistory(sn).Take(50));

        // ── Helper ───────────────────────────────────────────────────────────

        private List<DeviceSummary> BuildDeviceList()
        {
            var seen = _registry.GetAllDevices().ToDictionary(
                d => d.SerialNumber.ToUpperInvariant());

            var result = new List<DeviceSummary>();

            foreach (var client in _config.Clients)
            {
                foreach (var sn in client.DeviceSerialNumbers)
                {
                    var key = sn.ToUpperInvariant();
                    seen.TryGetValue(key, out var info);
                    var ds = _state.GetState(sn);

                    result.Add(new DeviceSummary
                    {
                        SerialNumber = sn,
                        ClientId = client.ClientId,
                        ClientName = client.ClientName,
                        ForwardUrls = client.ForwardUrls,
                        IsOnline = _registry.IsOnline(sn),
                        LastSeen = info?.LastSeen,
                        AutoPull = ds.AutoPullEnabled,
                        PullPending = ds.PullPending,
                        LastStamp = ds.LastStamp,
                        RecentPulls = _state.GetHistory(sn).Take(5).ToList()
                    });
                }
            }

            return result;
        }
    }

    public class DeviceSummary
    {
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public List<string> ForwardUrls { get; set; } = new();
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public bool AutoPull { get; set; }
        public bool PullPending { get; set; }
        public long LastStamp { get; set; }
        public List<PullEvent> RecentPulls { get; set; } = new();
    }
}
