using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ZKTecoGateway.Config;
using ZKTecoGateway.Models;

namespace ZKTecoGateway.Services
{
    /// <summary>
    /// Forwards parsed attendance records to the client application's API.
    /// Supports batching (so large log dumps don't time out).
    /// Keeps an in-memory ring buffer of recent forward attempts for the admin dashboard.
    /// </summary>
    public class ForwardingService
    {
        private readonly ILogger<ForwardingService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Ring buffer — last 200 forward results for dashboard
        private readonly ConcurrentQueue<ForwardLog> _recentLogs = new();
        private const int MaxLogEntries = 200;
        private const int BatchSize = 1000; // records per POST

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ForwardingService(ILogger<ForwardingService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public IEnumerable<ForwardLog> RecentLogs => _recentLogs;

        /// <summary>
        /// Forward a list of AttendanceRecords to the correct client API.
        /// Records are batched to avoid payload size issues.
        /// </summary>
        public async Task<ForwardResult> ForwardAsync(
      List<AttendanceRecord> records,
      ClientConfig client,
      CancellationToken ct = default)
        {
            if (records.Count == 0)
            {
                return new ForwardResult
                {
                    Success = true,
                    Message = "No records",
                    RecordCount = 0
                };
            }

            if (client.ForwardUrls == null || client.ForwardUrls.Count == 0)
            {
                return new ForwardResult
                {
                    Success = false,
                    Message = "No forwarding URLs configured",
                    RecordCount = 0
                };
            }

            var viewModels = records
                .Select((r, i) => AttendanceParser.ToHistoryViewModel(r, i + 1))
                .ToList();

            var http = _httpClientFactory.CreateClient("ForwardClient");

            int sent = 0;
            int batchNum = 0;

            for (int offset = 0; offset < viewModels.Count; offset += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                batchNum++;

                var batch = viewModels
                    .Skip(offset)
                    .Take(BatchSize)
                    .ToList();

                var json = JsonSerializer.Serialize(batch, JsonOpts);

                bool batchSuccess = false;

                foreach (var url in client.ForwardUrls)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                        };

                        if (!string.IsNullOrWhiteSpace(client.ApiKey))
                        {
                            request.Headers.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue(
                                    "Bearer",
                                    client.ApiKey);
                        }

                        var response = await http.SendAsync(request, ct);
                        var body = await response.Content.ReadAsStringAsync(ct);

                        if (response.IsSuccessStatusCode)
                        {
                            batchSuccess = true;

                            _logger.LogInformation(
                                "[{ClientId}] Batch {Batch} -> {Url} -> {Count} records forwarded. HTTP {Status}",
                                client.ClientId,
                                batchNum,
                                url,
                                batch.Count,
                                (int)response.StatusCode);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[{ClientId}] Batch {Batch} -> {Url} failed. HTTP {Status}: {Body}",
                                client.ClientId,
                                batchNum,
                                url,
                                (int)response.StatusCode,
                                body);

                            EnqueueLog(new ForwardLog
                            {
                                Time = DateTime.UtcNow,
                                SerialNumber = records.First().SerialNumber,
                                ClientId = client.ClientId,
                                RecordCount = batch.Count,
                                Success = false,
                                Message = $"[{url}] HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "[{ClientId}] Forward exception -> {Url}",
                            client.ClientId,
                            url);

                        EnqueueLog(new ForwardLog
                        {
                            Time = DateTime.UtcNow,
                            SerialNumber = records.First().SerialNumber,
                            ClientId = client.ClientId,
                            RecordCount = batch.Count,
                            Success = false,
                            Message = $"[{url}] {ex.Message}"
                        });
                    }
                }

                // Count batch as successful if at least one URL accepted it
                if (batchSuccess)
                {
                    sent += batch.Count;
                }
            }

            EnqueueLog(new ForwardLog
            {
                Time = DateTime.UtcNow,
                SerialNumber = records.First().SerialNumber,
                ClientId = client.ClientId,
                RecordCount = sent,
                Success = sent > 0,
                Message = $"{sent}/{records.Count} records forwarded"
            });

            return new ForwardResult
            {
                Success = sent > 0,
                RecordCount = sent,
                Message = $"{sent}/{records.Count} records forwarded to {client.ForwardUrls.Count} destination(s)"
            };
        }

        private void EnqueueLog(ForwardLog log)
        {
            _recentLogs.Enqueue(log);
            // Trim to MaxLogEntries
            while (_recentLogs.Count > MaxLogEntries)
                _recentLogs.TryDequeue(out _);
        }
    }
}
