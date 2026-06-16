using ZKTecoGateway.Models;

namespace ZKTecoGateway.Services
{
    /// <summary>
    /// Parses the raw ATTLOG body that ZKTeco devices POST to /iclock/cdata.
    ///
    /// Each line is tab-separated:
    ///   PIN \t Timestamp \t Status \t VerifyMode \t WorkCode \t (optional fields)
    ///
    /// Example:
    ///   1001\t2024-06-08 09:32:10\t0\t1\t0\t0
    /// </summary>
    public static class AttendanceParser
    {
        private static readonly string[] TimestampFormats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
        };

        public static List<AttendanceRecord> Parse(string body, string serialNumber, string machineCode)
        {
            var records = new List<AttendanceRecord>();
            if (string.IsNullOrWhiteSpace(body)) return records;

            var lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Skip header / control lines
                if (line.StartsWith("GET") || line.StartsWith("POST") || line.Contains("=")) continue;

                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                var pin = parts[0].Trim();
                if (string.IsNullOrEmpty(pin)) continue;

                if (!DateTime.TryParseExact(
                    parts[1].Trim(),
                    TimestampFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var ts)) continue;

                _ = int.TryParse(parts.ElementAtOrDefault(2)?.Trim(), out var status);
                _ = int.TryParse(parts.ElementAtOrDefault(3)?.Trim(), out var verify);
                _ = int.TryParse(parts.ElementAtOrDefault(4)?.Trim(), out var workCode);

                records.Add(new AttendanceRecord
                {
                    SerialNumber = serialNumber,
                    Pin = pin,
                    Timestamp = ts,
                    Status = status,
                    VerifyMode = verify,
                    WorkCode = workCode,
                    MachineCode = machineCode
                });
            }

            return records;
        }

        /// <summary>Convert gateway AttendanceRecord to the HistoryViewModel your client apps already expect.</summary>
        public static HistoryViewModel ToHistoryViewModel(AttendanceRecord r, int index)
        {
            long userId = 0;
            long.TryParse(r.Pin, out userId);

            return new HistoryViewModel
            {
                EmpId = r.Pin,
                UserId = userId,
                EvntDate = r.Timestamp.ToString("yyyy-MM-dd"),
                EventDate = r.Timestamp,
                EvntTime = r.Timestamp.ToString("HH:mm:ss"),
                EventTime = r.Timestamp.ToString("HH:mm:ss"),
                ChckType = r.Status.ToString(),
                CheckType = MapStatus(r.Status),
                VerifyCode = r.VerifyMode,
                LogId = index.ToString(),
                SN = r.SerialNumber,
                MachineCode = r.MachineCode,
                WorkCode = r.WorkCode.ToString(),
                Status = MapStatus(r.Status),
                Remarks = ""
            };
        }

        private static string MapStatus(int status) => status switch
        {
            0 => "Check-In",
            1 => "Check-Out",
            2 => "Break-Out",
            3 => "Break-In",
            4 => "OT-In",
            5 => "OT-Out",
            _ => "Unknown"
        };
    }
}
