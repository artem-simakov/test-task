using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using task.Data;
using task.Services;
using Xunit;

namespace task.Tests;

public class TerminalImportServiceTests
{
    private readonly IDbContextFactory<DellinDictionaryDbContext> _dbContextFactory;

    public TerminalImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<DellinDictionaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockFactory = new Mock<IDbContextFactory<DellinDictionaryDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(default))
                   .ReturnsAsync(() => new DellinDictionaryDbContext(options));
        _dbContextFactory = mockFactory.Object;
    }

    [Fact]
    public async Task ImportFromPathAsync_ShouldImportDataCorrectly()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "Test City",
                    code = "123",
                    cityID = 100,
                    terminals = new
                    {
                        terminal = new[]
                        {
                            new
                            {
                                id = "10",
                                name = "Test Terminal",
                                address = "Street 1",
                                fullAddress = "Full Street 1",
                                latitude = "55.0",
                                longitude = "37.0",
                                isPVZ = true,
                                phones = new[]
                                {
                                    new { number = "12345", comment = "Work" }
                                },
                                calcSchedule = new { arrival = "Mon-Fri" }
                            }
                        }
                    }
                }
            }
        };

        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));

        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var office = await context.Offices.Include(o => o.Phones).FirstOrDefaultAsync(o => o.Id == 10);
        
        Assert.NotNull(office);
        Assert.Equal("Test City", office.AddressCity);
        Assert.Equal(100, office.CityCode);
        Assert.Single(office.Phones);
        Assert.Equal("12345", office.Phones.First().PhoneNumber);

        // Cleanup
        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_FileNotFound_ShouldThrowException()
    {
        // Arrange
        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);
        var nonExistentPath = "non_existent_file.json";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            service.ImportFromPathAsync(nonExistentPath, CancellationToken.None));
    }

    [Fact]
    public async Task ImportFromPathAsync_EmptyData_ShouldLogWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TerminalImportService>>();
        var service = new TerminalImportService(_dbContextFactory, mockLogger.Object);
        
        var testJson = new { city = (object[]?)null };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No data found in JSON")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_InvalidTerminalId_ShouldSkip()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "City",
                    cityID = 100,
                    terminals = new
                    {
                        terminal = new[]
                        {
                            new { id = "invalid", name = "Terminal" }
                        }
                    }
                }
            }
        };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));
        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        using var context = await _dbContextFactory.CreateDbContextAsync();
        Assert.Empty(context.Offices);

        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_InvalidCoordinates_ShouldDefaultToZero()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "City",
                    cityID = 100,
                    terminals = new
                    {
                        terminal = new[]
                        {
                            new 
                            { 
                                id = "10", 
                                name = "Terminal",
                                latitude = "invalid",
                                longitude = "invalid"
                            }
                        }
                    }
                }
            }
        };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));
        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var office = await context.Offices.FirstAsync();
        Assert.Equal(0, office.Coordinates.Latitude);
        Assert.Equal(0, office.Coordinates.Longitude);

        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_DatabaseError_ShouldRollbackAndThrow()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "City",
                    cityID = 100,
                    terminals = new
                    {
                        terminal = new[] { new { id = "10", name = "Terminal" } }
                    }
                }
            }
        };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));

        var options = new DbContextOptionsBuilder<DellinDictionaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockFactory = new Mock<IDbContextFactory<DellinDictionaryDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new Exception("Factory Error"));

        var service = new TerminalImportService(mockFactory.Object, NullLogger<TerminalImportService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            service.ImportFromPathAsync(filePath, CancellationToken.None));

        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_PhoneMapping_ShouldWorkCorrectly()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "City",
                    cityID = 100,
                    terminals = new
                    {
                        terminal = new[]
                        {
                            new 
                            { 
                                id = "10", 
                                name = "Terminal",
                                phones = new[]
                                {
                                    new { number = "111", comment = "C1" },
                                    new { number = "222", comment = (string?)null }
                                }
                            }
                        }
                    }
                }
            }
        };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));
        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var office = await context.Offices.Include(o => o.Phones).FirstAsync();
        Assert.Equal(2, office.Phones.Count);
        Assert.Contains(office.Phones, p => p.PhoneNumber == "111" && p.Additional == "C1");
        Assert.Contains(office.Phones, p => p.PhoneNumber == "222" && p.Additional == null);

        File.Delete(filePath);
    }

    [Fact]
    public async Task ImportFromPathAsync_MissingTerminals_ShouldSkipCity()
    {
        // Arrange
        var testJson = new
        {
            city = new[]
            {
                new
                {
                    id = "1",
                    name = "City",
                    cityID = 100,
                    terminals = (object?)null
                }
            }
        };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(testJson));
        var service = new TerminalImportService(_dbContextFactory, NullLogger<TerminalImportService>.Instance);

        // Act
        await service.ImportFromPathAsync(filePath, CancellationToken.None);

        // Assert
        using var context = await _dbContextFactory.CreateDbContextAsync();
        Assert.Empty(context.Offices);

        File.Delete(filePath);
    }
}
