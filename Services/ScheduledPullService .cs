using Microsoft.Extensions.Options;
using ZKTecoGateway.Config;
using ZKTecoGateway.Services;

public class ScheduledPullService : BackgroundService
{
    private readonly GatewayConfig _config;
    private readonly DeviceStateService _state;
    private readonly ScheduledPullConfig _schedule;
    private readonly ILogger<ScheduledPullService> _logger;

    private readonly HashSet<string> _executedToday = new();

    public ScheduledPullService(
        GatewayConfig config,
        DeviceStateService state,
        IOptions<ScheduledPullConfig> schedule,
        ILogger<ScheduledPullService> logger)
    {
        _config = config;
        _state = state;
        _schedule = schedule.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_schedule.Enabled)
            {
                var now = DateTime.Now;
                var current = now.ToString("HH:mm");

                if (_schedule.Times.Contains(current) &&
                    !_executedToday.Contains($"{now:yyyyMMdd}-{current}"))
                {
                    _logger.LogInformation(
                        "Running scheduled attendance pull at {Time}",
                        current);

                    foreach (var client in _config.Clients)
                    {
                        foreach (var sn in client.DeviceSerialNumbers)
                        {
                            _state.RequestPull(sn);

                            _logger.LogInformation(
                                "Scheduled pull queued for {SN}",
                                sn);
                        }
                    }

                    _executedToday.Add($"{now:yyyyMMdd}-{current}");
                }

                if (now.Hour == 0 && now.Minute == 0)
                {
                    _executedToday.Clear();
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(30),
                stoppingToken);
        }
    }
}