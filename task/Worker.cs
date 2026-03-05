using task.Services;

namespace task;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITerminalImportService _importService;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        ITerminalImportService importService,
        IConfiguration configuration)
    {
        _logger = logger;
        _importService = importService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetNextRunDelay();
            _logger.LogInformation("Next run scheduled in {delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RunImportAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled import");
            }
        }
    }

    protected virtual TimeSpan GetNextRunDelay()
    {
        var mskZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        var nowMsk = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, mskZone);
        
        var nextRunMsk = nowMsk.Date.AddHours(2);
        if (nowMsk >= nextRunMsk)
        {
            nextRunMsk = nextRunMsk.AddDays(1);
        }

        return nextRunMsk - nowMsk;
    }

    protected virtual async Task RunImportAsync(CancellationToken ct)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "files", "terminals.json");
        if (!File.Exists(filePath))
        {
             filePath = Path.Combine(Directory.GetCurrentDirectory(), "files", "terminals.json");
        }
        
        await _importService.ImportFromPathAsync(filePath, ct);
    }
}
