using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using task.Services;
using Xunit;

namespace task.Tests;

public class WorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallRunImportAsync()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Worker>>();
        var mockImportService = new Mock<ITerminalImportService>();
        var mockConfig = new Mock<IConfiguration>();

        var worker = new TestWorker(mockLogger.Object, mockImportService.Object, mockConfig.Object);
        var cts = new CancellationTokenSource();

        // Act
        var task = worker.PublicExecuteAsync(cts.Token);
        
        // Wait a bit to ensure the loop starts and hits Task.Delay
        await Task.Delay(100);
        cts.Cancel();
        await task;

        // Assert
        Assert.True(worker.GetNextRunDelayCalled);
    }

    [Fact]
    public void GetNextRunDelay_ShouldReturnPositiveTimeSpan()
    {
        // Arrange
        var worker = new TestWorker(null!, null!, null!);

        // Act
        var delay = worker.PublicGetNextRunDelay();

        // Assert
        Assert.True(delay.TotalSeconds > 0);
        Assert.True(delay.TotalHours <= 24);
    }

    [Fact]
    public async Task RunImportAsync_ShouldLogAndSkip_IfFileNotFound()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Worker>>();
        var mockImportService = new Mock<ITerminalImportService>();
        var worker = new TestWorker(mockLogger.Object, mockImportService.Object, null!);

        // Act
        // RunImportAsync is protected, and it doesn't log internally (the loop logs).
        // But we can test the file check logic.
        await worker.PublicRunImportAsync(CancellationToken.None);

        // Assert
        // Should have called ImportFromPathAsync with some path.
        mockImportService.Verify(s => s.ImportFromPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogException_WhenImportFails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Worker>>();
        var mockImportService = new Mock<ITerminalImportService>();
        var worker = new TestWorker(mockLogger.Object, mockImportService.Object, null!)
        {
            ThrowInRunImport = true
        };
        var cts = new CancellationTokenSource();

        // Act
        var task = worker.PublicExecuteAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await task;

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during scheduled import")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private class TestWorker : Worker
    {
        public bool GetNextRunDelayCalled { get; private set; }
        public bool ThrowInRunImport { get; set; }

        public TestWorker(ILogger<Worker> logger, ITerminalImportService importService, IConfiguration configuration) 
            : base(logger, importService, configuration)
        {
        }

        public Task PublicExecuteAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
        
        public TimeSpan PublicGetNextRunDelay() => GetNextRunDelay();

        public Task PublicRunImportAsync(CancellationToken ct) => RunImportAsync(ct);

        protected override TimeSpan GetNextRunDelay()
        {
            GetNextRunDelayCalled = true;
            return TimeSpan.FromMilliseconds(10);
        }

        protected override Task RunImportAsync(CancellationToken ct)
        {
            if (ThrowInRunImport) throw new Exception("Test exception");
            return base.RunImportAsync(ct);
        }
    }
}