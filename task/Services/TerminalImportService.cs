using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using task.Data;
using task.Entities;
using task.Models;

namespace task.Services;

public interface ITerminalImportService
{
    Task ImportFromPathAsync(string filePath, CancellationToken ct);
}

public class TerminalImportService : ITerminalImportService
{
    private readonly IDbContextFactory<DellinDictionaryDbContext> _contextFactory;
    private readonly ILogger<TerminalImportService> _logger;

    public TerminalImportService(
        IDbContextFactory<DellinDictionaryDbContext> contextFactory,
        ILogger<TerminalImportService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task ImportFromPathAsync(string filePath, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Source JSON file not found", filePath);
            }

            using var stream = File.OpenRead(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = await JsonSerializer.DeserializeAsync<DellinCityResponse>(stream, options, ct);

            if (data?.Cities == null)
            {
                _logger.LogWarning("No data found in JSON");
                return;
            }

            var offices = MapToOffices(data);
            _logger.LogInformation("Загружено {Count} терминалов из JSON", offices.Count);

            using var context = await _contextFactory.CreateDbContextAsync(ct);
            
            using var transaction = await context.Database.BeginTransactionAsync(ct);
            try
            {
                int oldCount = 0;
                if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                {
                    var allOffices = await context.Offices.ToListAsync(ct);
                    oldCount = allOffices.Count;
                    context.Offices.RemoveRange(allOffices);
                    await context.SaveChangesAsync(ct);
                }
                else
                {
                    oldCount = await context.Offices.ExecuteDeleteAsync(ct);
                }
                
                _logger.LogInformation("Удалено {OldCount} старых записей", oldCount);

                await context.Offices.AddRangeAsync(offices, ct);
                await context.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
                _logger.LogInformation("Сохранено {NewCount} новых терминалов", offices.Count);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка импорта: {Exception}", ex.ToString());
            throw; 
        }
    }

    private List<Office> MapToOffices(DellinCityResponse data)
    {
        var result = new List<Office>();
        foreach (var city in data.Cities)
        {
            if (city.Terminals?.TerminalList == null) continue;

            foreach (var t in city.Terminals.TerminalList)
            {
                if (!int.TryParse(t.Id, out var id)) continue;

                var office = new Office
                {
                    Id = id,
                    Code = t.Id,
                    CityCode = city.CityId,
                    Uuid = Guid.NewGuid().ToString(),
                    Type = t.IsPvz ? OfficeType.PVZ : OfficeType.WAREHOUSE,
                    CountryCode = "RU",
                    Coordinates = new Coordinates
                    {
                        Latitude = double.TryParse(t.Latitude, out var lat) ? lat : 0,
                        Longitude = double.TryParse(t.Longitude, out var lon) ? lon : 0
                    },
                    AddressCity = city.Name,
                    AddressStreet = t.Address,
                    WorkTime = t.CalcSchedule?.Arrival ?? "Не указано",
                    Phones = t.Phones?.Select(p => new Phone
                    {
                        PhoneNumber = p.Number,
                        Additional = p.Comment
                    }).ToList() ?? new List<Phone>()
                };
                result.Add(office);
            }
        }
        return result;
    }
}
