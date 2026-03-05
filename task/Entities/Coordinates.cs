using Microsoft.EntityFrameworkCore;

namespace task.Entities;

[Owned]
public class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}