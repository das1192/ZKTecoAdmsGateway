using System.Collections.Concurrent;
using ZKTecoGateway.Models;

namespace ZKTecoGateway.Services
{
    /// <summary>
    /// Central state manager for each device.
    /// Controls whether auto-pull is on/off and tracks pull requests.
    /// </summary>
    public class DeviceStateService
    {
        // Auto-pull enabled per device (default: OFF — manual only)
        private readonly ConcurrentDictionary<string, bool> _autoPull = new();

        // Whether a manual pull has been requested and not yet fulfilled
        private readonly ConcurrentDictionary<string, bool> _pullPending = new();

        // Last stamp received per device (0 = never received, get everything)
        private readonly ConcurrentDictionary<string, long> _lastStamp = new();

        // Initial upload done this session
        private readonly ConcurrentDictionary<string, bool> _initialDone = new();

        // Pull history per device — last 100 events
        private readonly ConcurrentDictionary<string, Queue<PullEvent>> _history = new();

        private const int MaxHistory = 100;


        private readonly ConcurrentQueue<AttendancePullLog> _pullLogs = new();

        public void RecordAttendancePull(
    string sn,
    string clientId,
    List<AttendanceRecord> records)
        {
            _pullLogs.Enqueue(new AttendancePullLog
            {
                Time = DateTime.UtcNow,
                SerialNumber = sn,
                ClientId = clientId,
                RecordCount = records.Count,
                Records = records
            });

            while (_pullLogs.Count > 500)
                _pullLogs.TryDequeue(out _);
        }

        public IEnumerable<AttendancePullLog> GetPullLogs()
        {
            return _pullLogs
                .OrderByDescending(x => x.Time);
        }
        public void UpdateLastPullForwardStatus(
    string sn,
    bool success,
    string message)
        {
            var log = _pullLogs
                .LastOrDefault(x => x.SerialNumber == sn);

            if (log == null)
                return;

            log.ForwardSuccess = success;
            log.ForwardMessage = message;
        }





        // ── Auto Pull ────────────────────────────────────────────────────────

        public bool IsAutoPullEnabled(string sn) =>
            _autoPull.GetOrAdd(NormSN(sn), false);

        public void SetAutoPull(string sn, bool enabled)
        {
            _autoPull[NormSN(sn)] = enabled;
            if (enabled)
                // When turning auto on, reset initial so it pulls right away
                _initialDone[NormSN(sn)] = false;
        }

        // ── Manual Pull ──────────────────────────────────────────────────────

        public bool IsPullPending(string sn) =>
            _pullPending.GetOrAdd(NormSN(sn), false);

        public void RequestPull(string sn)
        {
            _pullPending[NormSN(sn)] = true;
            _initialDone[NormSN(sn)] = false;
        }

        public void ClearPullPending(string sn) =>
            _pullPending[NormSN(sn)] = false;

        // ── Stamp Tracking ───────────────────────────────────────────────────

        public long GetStamp(string sn) =>
            _lastStamp.GetOrAdd(NormSN(sn), 0);

        public void SetStamp(string sn, long stamp) =>
            _lastStamp[NormSN(sn)] = stamp;

        // ── Initial Done ─────────────────────────────────────────────────────

        public bool IsInitialDone(string sn) =>
            _initialDone.GetOrAdd(NormSN(sn), false);

        public void SetInitialDone(string sn, bool done) =>
            _initialDone[NormSN(sn)] = done;

        // ── Should device be commanded to upload? ────────────────────────────

        /// <summary>
        /// Returns true if the device should receive C:DATA QUERY ATTLOG command.
        /// True when: manual pull pending, OR auto-pull is on and initial not done.
        /// </summary>
        public bool ShouldUpload(string sn)
        {
            var key = NormSN(sn);
            if (IsPullPending(key)) return true;
            if (IsAutoPullEnabled(key) && !IsInitialDone(key)) return true;
            return false;
        }

        // ── Pull History ─────────────────────────────────────────────────────

        public void RecordPull(string sn, int recordCount, bool success, string message)
        {
            var key = NormSN(sn);
            var queue = _history.GetOrAdd(key, _ => new Queue<PullEvent>());
            lock (queue)
            {
                queue.Enqueue(new PullEvent
                {
                    Time = DateTime.UtcNow,
                    SerialNumber = sn,
                    RecordCount = recordCount,
                    Success = success,
                    Message = message
                });
                while (queue.Count > MaxHistory)
                    queue.Dequeue();
            }
        }

        public IEnumerable<PullEvent> GetHistory(string sn)
        {
            var key = NormSN(sn);
            if (_history.TryGetValue(key, out var queue))
                lock (queue) return queue.OrderByDescending(e => e.Time).ToList();
            return Enumerable.Empty<PullEvent>();
        }

        public IEnumerable<PullEvent> GetAllHistory() =>
            _history.Values
                .SelectMany(q => { lock (q) return q.ToList(); })
                .OrderByDescending(e => e.Time)
                .Take(200);

        // ── Device Summary ───────────────────────────────────────────────────

        public DeviceControlState GetState(string sn)
        {
            var key = NormSN(sn);
            return new DeviceControlState
            {
                SerialNumber = sn,
                AutoPullEnabled = IsAutoPullEnabled(key),
                PullPending = IsPullPending(key),
                LastStamp = GetStamp(key),
                InitialDone = IsInitialDone(key)
            };
        }

        private static string NormSN(string sn) => sn.ToUpperInvariant();
    }

    public class DeviceControlState
    {
        public string SerialNumber { get; set; } = "";
        public bool AutoPullEnabled { get; set; }
        public bool PullPending { get; set; }
        public long LastStamp { get; set; }
        public bool InitialDone { get; set; }
    }

    public class PullEvent
    {
        public DateTime Time { get; set; }
        public string SerialNumber { get; set; } = "";
        public int RecordCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class AttendancePullLog
    {
        public DateTime Time { get; set; }
        public string SerialNumber { get; set; } = "";
        public string ClientId { get; set; } = "";
        public int RecordCount { get; set; }
        public bool ForwardSuccess { get; set; }

        public string ForwardMessage { get; set; } = "";
        public List<AttendanceRecord> Records { get; set; } = new();
    }

}