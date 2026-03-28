using JobHunterBot.Common;
using JobHunterBot.Data;
using JobHunterBot.Models;
using JobHunterBot.Scrapers;
using JobHunterBot.Services;
using Microsoft.EntityFrameworkCore;

namespace JobHunterBot;

public class Worker(ILogger<Worker> logger, IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("JobHunterBot iniciado às: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Iniciando varredura de vagas às: {time}", DateTimeOffset.Now);

            try
            {
                using var scope = serviceProvider.CreateScope();
                
                var scrapers = scope.ServiceProvider.GetRequiredService<IEnumerable<IVagaScraper>>();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var telegramService = scope.ServiceProvider.GetRequiredService<TelegramService>();

                logger.LogInformation("Disparando buscas nos {Count} scrapers registrados.", scrapers.Count());

                var tasksDeBusca = scrapers.Select(s => s.BuscarVagasAsync(stoppingToken));
                var resultados = await Task.WhenAll(tasksDeBusca);
                
                var todasVagasBrutas = resultados.SelectMany(x => x).ToList();
                
                logger.LogInformation("Total de vagas brutas encontradas: {Count}", todasVagasBrutas.Count);

                // Pegar URLs logicas
                var urlsEncontradas = todasVagasBrutas.Select(v => v.Url).Distinct().ToList();

                // Filtrar os que já existem no banco
                var urlsNoBanco = await dbContext.Vagas
                    .Where(v => urlsEncontradas.Contains(v.Url))
                    .Select(v => v.Url)
                    .ToListAsync(stoppingToken);

                // 1. Remove duplicatas da varredura atual
                // 2. Remove o que já tá no banco
                // 3. Remove "Lixos" absolutos mapeados (Pedagogia, Rh, etc)
                var vagasIneditasLimpas = todasVagasBrutas
                    .DistinctBy(x => x.Url) 
                    .Where(v => !urlsNoBanco.Contains(v.Url))
                    .Where(v => !FiltrosVaga.ContemPalavrasProibidas(v.Titulo))
                    .ToList();

                logger.LogInformation("Total de vagas INÉDITAS e LIMPAS: {Count}", vagasIneditasLimpas.Count);

                if (vagasIneditasLimpas.Any())
                {
                    // Salva as novas vagas limpas no banco
                    await dbContext.Vagas.AddRangeAsync(vagasIneditasLimpas, stoppingToken);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    // Pega usuários ativos
                    var usuarios = await dbContext.UsuariosConfig.ToListAsync(stoppingToken);
                    
                    // Cruza as vagas ineditas da rodada com o filtro LINQ de cada usuário e notifica
                    foreach (var usuario in usuarios)
                    {
                        var vagasParaUsuario = vagasIneditasLimpas
                            .Where(v => FiltrosVaga.VagaValidaParaUsuario(v, usuario))
                            .OrderByDescending(v => v.Titulo.Contains("C#", StringComparison.OrdinalIgnoreCase) || 
                                                    v.Titulo.Contains(".NET", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (vagasParaUsuario.Any())
                        {
                            logger.LogInformation("Enviando {Count} vagas para o chatId {Id}", vagasParaUsuario.Count, usuario.ChatId);
                            await telegramService.EnviarVagasParaUsuarioAsync(vagasParaUsuario, long.Parse(usuario.ChatId), stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no ciclo principal de Execução.");
            }

            // Aguarda 4 horas para o próximo loop
            await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
        }
    }
}
