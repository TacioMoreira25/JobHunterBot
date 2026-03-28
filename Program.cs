using JobHunterBot;
using JobHunterBot.Data;
using JobHunterBot.Scrapers;
using JobHunterBot.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Configuração do Banco de Dados SQLite (Connection String pode vir do appsettings)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=jobhunter.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Políticas de Resiliência para Request HTTP + Headers padrão
builder.Services.AddHttpClient<IVagaScraper, GupyScraper>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IVagaScraper, GitHubIssuesScraper>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IVagaScraper, ProgramathorScraper>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IVagaScraper, RemotarScraper>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IVagaScraper, VagasComScraper>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IVagaScraper, LinkedInRapidApiScraper>()
    .AddStandardResilienceHandler();

// Outros serviços
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddHostedService<TelegramListenerService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Garantir que o banco de dados seja criado/atualizado ao iniciar o Worker
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // Cria banco sem EF Migrations explícitos se preferir, ou usar MigrateAsync() p/ migrations
}

host.Run();
