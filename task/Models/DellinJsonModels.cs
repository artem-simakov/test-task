using System.Text.Json.Serialization;

namespace task.Models;

public class DellinCityResponse
{
    [JsonPropertyName("city")]
    public List<DellinCity> Cities { get; set; } = [];
}

public class DellinCity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("cityID")]
    public int CityId { get; set; }

    [JsonPropertyName("terminals")]
    public DellinTerminalsWrapper? Terminals { get; set; }
}

public class DellinTerminalsWrapper
{
    [JsonPropertyName("terminal")]
    public List<DellinTerminal> TerminalList { get; set; } = [];
}

public class DellinTerminal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("fullAddress")]
    public string FullAddress { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public string Latitude { get; set; } = string.Empty;

    [JsonPropertyName("longitude")]
    public string Longitude { get; set; } = string.Empty;

    [JsonPropertyName("isPVZ")]
    public bool IsPvz { get; set; }

    [JsonPropertyName("phones")]
    public List<DellinPhone>? Phones { get; set; }

    [JsonPropertyName("calcSchedule")]
    public DellinSchedule? CalcSchedule { get; set; }
}

public class DellinPhone
{
    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

public class DellinSchedule
{
    [JsonPropertyName("arrival")]
    public string? Arrival { get; set; }
}
