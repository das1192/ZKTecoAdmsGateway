/*
 * ============================================================
 *  CLIENT APPLICATION — HrPayroll Controller
 *  (Your existing application — add this endpoint)
 *
 *  The ZKTeco Gateway will POST attendance batches here.
 *  This replaces the old "pull from device" approach.
 *
 *  Matches the HistoryViewModel your original code already used.
 * ============================================================
 */

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace YourClientApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HrPayrollController : ControllerBase
    {
        private readonly ILogger<HrPayrollController> _logger;
        // Inject your DB context / service here
        // private readonly IAttendanceService _attendanceService;

        public HrPayrollController(ILogger<HrPayrollController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Receives attendance records forwarded by the ZKTeco Gateway.
        /// The gateway POSTs here automatically when devices push data.
        /// </summary>
        [HttpPost("SaveHistory")]
        public async Task<IActionResult> SaveHistory([FromBody] List<HistoryViewModel> records)
        {
            if (records == null || records.Count == 0)
                return Ok(new ApiResponse { Status = 200, Message = "No records received" });

            _logger.LogInformation("Received {Count} attendance records from gateway", records.Count);

            try
            {
                // ── Save to your database ──────────────────────────────────────
                // foreach (var record in records)
                // {
                //     await _attendanceService.SaveAsync(record);
                // }
                // ─────────────────────────────────────────────────────────────

                // For now, just log them
                foreach (var r in records.Take(5))
                    _logger.LogInformation("  EmpId={EmpId} Date={Date} Time={Time} Status={Status} SN={SN}",
                        r.EmpId, r.EvntDate, r.EvntTime, r.ChckType, r.SN);

                if (records.Count > 5)
                    _logger.LogInformation("  ... and {More} more records", records.Count - 5);

                return Ok(new ApiResponse
                {
                    Status = 200,
                    Message = $"{records.Count} records saved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving attendance records");
                return StatusCode(500, new ApiResponse
                {
                    Status = 500,
                    Message = "Internal error: " + ex.Message
                });
            }
        }
    }

    // ── Models (same as before — no change needed) ────────────────────────────

    public class HistoryViewModel
    {
        public long HistoryId { get; set; }
        public long UserId { get; set; }
        public DateTime? EventDate { get; set; }
        public string EventTime { get; set; } = "";
        public string CheckType { get; set; } = "";
        public long VerifyCode { get; set; }
        public string SensorId { get; set; } = "";
        public string LogId { get; set; } = "";
        public long MachineId { get; set; }
        public string UserExtFmt { get; set; } = "";
        public string WorkCode { get; set; } = "";
        public string EmpId { get; set; } = "";
        public string EvntDate { get; set; } = "";
        public string EvntTime { get; set; } = "";
        public string ChckType { get; set; } = "";
        public string VerfyCode { get; set; } = "";
        public string SensorCode { get; set; } = "";
        public string SN { get; set; } = "";           // Device serial number added by gateway
        public string MachineCode { get; set; } = "";
        public string Status { get; set; } = "";
        public string Remarks { get; set; } = "";
    }

    public class ApiResponse
    {
        public int Status { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
}
