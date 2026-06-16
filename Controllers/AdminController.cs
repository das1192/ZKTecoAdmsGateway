using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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
        private readonly IWebHostEnvironment _env;
        public AdminController(
            DeviceRegistryService registry,
            ForwardingService forwarder,
            DeviceStateService state,
            GatewayConfig config,
            IWebHostEnvironment env)
        {
            _registry = registry;
            _forwarder = forwarder;
            _state = state;
            _config = config;
            _env = env;
        }


        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "Healthy",
                time = DateTime.UtcNow,
                version = "1.0.0"
            });
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

        [HttpGet("attendance-log/{date}")]
        public async Task<IActionResult> AttendanceLog(string date)
        {
            var file = Path.Combine(
                AppContext.BaseDirectory,
                "data",
                $"attendance-{date}.jsonl");

            if (!System.IO.File.Exists(file))
                return Ok(new List<AttendanceArchiveEntry>());

            var result = new List<AttendanceArchiveEntry>();

            foreach (var line in await System.IO.File.ReadAllLinesAsync(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry =
                    JsonSerializer.Deserialize<AttendanceArchiveEntry>(line);

                if (entry != null)
                    result.Add(entry);
            }

            return Ok(result);
        }
    }
 }
