using System.Text;
using System.Text.Json;
using ZKTecoGateway.Models;

namespace ZKTecoGateway.Services;

public class AttendanceArchiveService
{
    private readonly IWebHostEnvironment _env;

    public AttendanceArchiveService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task SaveAsync(
        string serialNumber,
        string clientId,
        List<AttendanceRecord> records)
    {
        if (records.Count == 0)
            return;

        var folder = Path.Combine(_env.ContentRootPath, "data");

        Directory.CreateDirectory(folder);

        var file = Path.Combine(
            folder,
            $"attendance-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

        await using var stream = new FileStream(
            file,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);

        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        foreach (var record in records)
        {
            var line = JsonSerializer.Serialize(new
            {
                Time = DateTime.UtcNow,
                SerialNumber = serialNumber,
                ClientId = clientId,
                Record = record
            });

            await writer.WriteLineAsync(line);
        }
    }
}