using Microsoft.AspNetCore.Mvc;
using ZKTecoGateway.Services;
using System.Collections.Concurrent;

namespace ZKTecoGateway.Controllers
{
    [ApiController]
    public class AdmsController : ControllerBase
    {
        private readonly ILogger<AdmsController> _logger;
        private readonly DeviceRegistryService _registry;
        private readonly ForwardingService _forwarder;
        private readonly DeviceStateService _state;
        private readonly AttendanceArchiveService _archive;
        public AdmsController(
            ILogger<AdmsController> logger,
            DeviceRegistryService registry,
            ForwardingService forwarder,
            DeviceStateService state,
            AttendanceArchiveService archive)
        {
            _logger = logger;
            _registry = registry;
            _forwarder = forwarder;
            _state = state;
            _archive = archive;
        }

        [HttpGet("/checkStatus")]
        public IActionResult Root() => Content("OK", "text/plain");

        [HttpGet("/iclock/test")]
        [HttpPost("/iclock/test")]
        public IActionResult Test() => Content("OK", "text/plain");

        // -----------------------------------------------------------------------
        // GET /iclock/cdata — ZAM70 initial handshake
        // -----------------------------------------------------------------------
        [HttpGet("/iclock/cdata")]
        public IActionResult CDataGet(
            [FromQuery] string SN = "",
            [FromQuery] string options = "",
            [FromQuery] string table = "",
            [FromQuery] string c = "")
        {
            _logger.LogInformation("GET /iclock/cdata SN={SN} options={options}", SN, options);
           
            _registry.Heartbeat(SN);

            if (options == "all")
            {
                var stamp = _state.GetStamp(SN);
                var now = DateTime.Now;
                var response =
                    "GET OPTION FROM:Server\r\n" +
                    $"Date={now:yyyy-MM-dd}\r\n" +
                    $"Time={now:HH:mm:ss}\r\n" +
                    "Reboot=0\r\n" +
                    $"Stamp={stamp}\r\n" +
                    "OpStamp=0\r\n" +
                    "PhotoStamp=0\r\n" +
                    $"ATTLOGStamp={stamp}\r\n" +
                    "OPERLOGStamp=0\r\n" +
                    "ATTPHOTOStamp=0\r\n" +
                    "ErrorDelay=30\r\n" +
                    "TransTimes=00:00;23:59\r\n" +
                    "TransInterval=1\r\n" +
                    "TransFlag=TransData AttLog\r\n" +
                    "Realtime=1\r\n" +
                    "Encrypt=0\r\n" +
                    "ServerVersion=2.4.1\r\n" +
                    "PushProtVer=2.4.1\r\n" +
                    "RequestDelay=10\r\n";

                _logger.LogInformation("options=all → {SN} ATTLOGStamp={Stamp}", SN, stamp);
                return Content(response, "text/plain");
            }

            return Content("OK", "text/plain");
        }

        // -----------------------------------------------------------------------
        // POST /iclock/cdata
        // -----------------------------------------------------------------------
        [HttpPost("/iclock/cdata")]
        public async Task<IActionResult> CDataPost(
            [FromQuery] string SN = "",
            [FromQuery] string table = "",
            [FromQuery] string c = "",
            [FromQuery] string Stamp = "")
        {
            string body;
            using (var reader = new StreamReader(Request.Body))
                body = await reader.ReadToEndAsync();

            _logger.LogInformation("POST /iclock/cdata SN={SN} table=[{table}] Stamp=[{Stamp}] bodyLen={Len}",
                SN, table, Stamp, body.Length);

            if (string.IsNullOrWhiteSpace(SN))
                return Content("OK", "text/plain");

            // ── Device Registration ─────────────────────────────────────────
            if (table == "options" || c == "registry")
            {
                var opts = ParseKeyValueBody(body);
                var client = _registry.RegisterDevice(SN, opts);
                // New session — reset initial done so auto-pull re-triggers if enabled
                _state.SetInitialDone(SN, false);

                _logger.LogInformation("Device {SN} registered. ClientId={ClientId}",
                    SN, client?.ClientId ?? "UNMAPPED");

                return Content("OK", "text/plain");
            }

            _registry.Heartbeat(SN);

            // ── Attendance Log ──────────────────────────────────────────────
            if (table.ToUpperInvariant() == "ATTLOG")
            {
                // Clear pending flags
                _state.ClearPullPending(SN);
                _state.SetInitialDone(SN, true);

                _logger.LogInformation("✓ ATTLOG from {SN} bodyLen={Len} Stamp={Stamp}",
                    SN, body.Length, Stamp);

                // Update stamp
                if (long.TryParse(Stamp, out long newStamp) && newStamp > 0)
                {
                    _state.SetStamp(SN, newStamp);
                    _logger.LogInformation("Stamp {SN} → {Stamp}", SN, newStamp);
                }

                if (body.Length > 0)
                    _logger.LogInformation("SAMPLE: [{S}]", body.Length > 200 ? body[..200] : body);

                var client = _registry.GetClient(SN);
                if (client == null)
                {
                    _logger.LogWarning("ATTLOG from unmapped device {SN}", SN);
                    _state.RecordPull(SN, 0, false, "Device not mapped to any client");
                    return Content("OK", "text/plain");
                }

                var records = AttendanceParser.Parse(body, SN, "1");
                _logger.LogInformation("Parsed {Count} records from {SN} → [{ClientId}]",
                    records.Count, SN, client.ClientId);
                _state.RecordAttendancePull(SN,client.ClientId,records);
                await _archive.SaveAsync( SN,client.ClientId,records);

                if (records.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        var result = await _forwarder.ForwardAsync(records, client);
                        _state.RecordPull(SN, result.RecordCount, result.Success, result.Message);
                        _state.UpdateLastPullForwardStatus(
    SN,
    result.Success,
    result.Message);
                    });
                }
                else
                {
                    _state.RecordPull(SN, 0, true, "No new records");
                }

                return Content("OK", "text/plain");
            }

            if (table.ToUpperInvariant() == "USERINFO")
            {
                _logger.LogInformation("USERINFO from {SN} (skipped)", SN);
                return Content("OK", "text/plain");
            }

            if (table.ToUpperInvariant() == "OPERLOG")
            {
                _logger.LogInformation("OPERLOG from {SN} (skipped)", SN);
                return Content("OK", "text/plain");
            }

            _logger.LogInformation("UNHANDLED table=[{table}] c=[{c}] SN={SN} body=[{Body}]",
                table, c, SN, body.Length > 200 ? body[..200] : body);

            return Content("OK", "text/plain");
        }

        // -----------------------------------------------------------------------
        // COMMAND POLLING
        // -----------------------------------------------------------------------
        [HttpGet("/iclock/getrequest")]
        public IActionResult GetRequest(
      [FromQuery] string SN = "",
      [FromQuery] string INFO = "")
        {
            _registry.Heartbeat(SN);

            if (_state.ShouldUpload(SN))
            {
                // Manual pull always uses stamp 0 to get all records
                // Auto pull uses last known stamp to get only new records
                var stamp = _state.IsPullPending(SN) ? 0 : _state.GetStamp(SN);
                _logger.LogInformation("→ Commanding {SN} QUERY ATTLOG Stamp={Stamp}", SN, stamp);
                return Content($"C:DATA QUERY ATTLOG Stamp={stamp}\r\n", "text/plain");
            }

            return Content("", "text/plain");
        }

        // -----------------------------------------------------------------------
        // COMMAND ACKNOWLEDGMENT
        // -----------------------------------------------------------------------
        [HttpPost("/iclock/devicecmd")]
        public async Task<IActionResult> DeviceCmd([FromQuery] string SN = "")
        {
            string body;
            using (var reader = new StreamReader(Request.Body))
                body = await reader.ReadToEndAsync();

            _logger.LogInformation("DeviceCmd SN={SN} body={Body}", SN, body);

            // Device responded with no data or command complete — clear pending
            _state.ClearPullPending(SN);
            _state.SetInitialDone(SN, true);

            if (body.Contains("No data") || body.Contains("0"))
            {
                _logger.LogInformation("No new records on {SN}", SN);
                _state.RecordPull(SN, 0, true, "No new records on device");
            }

            return Content("OK", "text/plain");
        }
        [HttpGet("attendance-history")]
        public IActionResult AttendanceHistory()
        {
            return Ok(_state.GetPullLogs().Take(100));
        }

        // -----------------------------------------------------------------------
        private static Dictionary<string, string> ParseKeyValueBody(string body)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(body)) return dict;
            foreach (var line in body.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = line.IndexOf('=');
                if (idx < 0) continue;
                dict[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }
            return dict;
        }
    }
}
