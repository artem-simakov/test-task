using Microsoft.EntityFrameworkCore;
using task;
using task.Data;
using task.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContextFactory<DellinDictionaryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITerminalImportService, TerminalImportService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
