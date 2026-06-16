namespace ZKTecoGateway.Models
{
    // -------------------------------------------------------------------------
    // ADMS / Device-side models
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parsed from a single line in the ATTLOG body sent by the device.
    /// Format: PIN\tTimestamp\tStatus\tVerify\tWorkCode\tReserved
    /// </summary>
    public class AttendanceRecord
    {
        public string SerialNumber { get; set; } = "";
        public string Pin { get; set; } = "";          // Employee / user ID on device
        public DateTime Timestamp { get; set; }
        public int Status { get; set; }                // 0=check-in 1=check-out 2=break-out 3=break-in 4=OT-in 5=OT-out
        public int VerifyMode { get; set; }            // 1=finger 4=card 15=face etc.
        public int WorkCode { get; set; }
        public string MachineCode { get; set; } = "";  // Populated by gateway from config
    }

    // -------------------------------------------------------------------------
    // Gateway internal models
    // -------------------------------------------------------------------------

    public class DeviceInfo
    {
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public DateTime LastSeen { get; set; }
        public DateTime RegisteredAt { get; set; }
        public Dictionary<string, string> Options { get; set; } = new();
    }

    public class ForwardResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = "";
        public int RecordCount { get; set; }
    }

    // -------------------------------------------------------------------------
    // The payload we POST to the client's application
    // (mirrors the HistoryViewModel you were already using)
    // -------------------------------------------------------------------------

    public class HistoryViewModel
    {
        public string EmpId { get; set; } = "";
        public long UserId { get; set; }
        public string EvntDate { get; set; } = "";
        public DateTime? EventDate { get; set; }
        public string EvntTime { get; set; } = "";
        public string EventTime { get; set; } = "";
        public string ChckType { get; set; } = "";
        public string CheckType { get; set; } = "Service";
        public long VerifyCode { get; set; }
        public string LogId { get; set; } = "";
        public string VerfyCode { get; set; } = "";
        public string SensorCode { get; set; } = "";
        public string SN { get; set; } = "";
        public string MachineCode { get; set; } = "";
        public long MachineId { get; set; }
        public string UserExtFmt { get; set; } = "";
        public string WorkCode { get; set; } = "";
        public string Status { get; set; } = "";
        public string Remarks { get; set; } = "";
    }

    // -------------------------------------------------------------------------
    // Admin / monitoring models
    // -------------------------------------------------------------------------

    public class GatewayStatus
    {
        public DateTime ServerTime { get; set; }
        public int TotalDevices { get; set; }
        public int OnlineDevices { get; set; }
        public List<DeviceStatus> Devices { get; set; } = new();
        public List<ForwardLog> RecentLogs { get; set; } = new();
    }

    public class DeviceStatus
    {
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public string ForwardUrl { get; set; } = "";
    }

    public class ForwardLog
    {
        public DateTime Time { get; set; }
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public int RecordCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }


    public class ForwardEvent
    {
        public DateTime Time { get; set; }
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public int RecordCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }
 

}
