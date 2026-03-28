using JobHunterBot.Common;
using JobHunterBot.Data;
using JobHunterBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JobHunterBot.Services;

public class TelegramListenerService : BackgroundService
{
    private readonly ILogger<TelegramListenerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramBotClient _botClient;

    public TelegramListenerService(
        ILogger<TelegramListenerService> logger, 
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        var botToken = configuration["Telegram:BotToken"];
        if (string.IsNullOrEmpty(botToken))
            throw new ArgumentException("Telegram BotToken não configurado em appsettings.json.");
            
        _botClient = new TelegramBotClient(botToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Recebe todos os tipos de update
        };

        _logger.LogInformation("Iniciando escuta do TelegramBot...");

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        // Mantém o background service vivo
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Apenas mensagens de texto nos interessam
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;

        var chatId = message.Chat.Id.ToString();
        _logger.LogInformation("Mensagem recebida de '{ChatId}': {Mensagem}", chatId, messageText);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegramService = scope.ServiceProvider.GetRequiredService<TelegramService>();

        // Busca ou cria usuário
        var usuario = await dbContext.UsuariosConfig.FindAsync(new object[] { chatId }, cancellationToken);
        if (usuario == null)
        {
            usuario = new UsuarioConfig { ChatId = chatId, AreaAtiva = "todos", NivelAtivo = "iniciantes" };
            dbContext.UsuariosConfig.Add(usuario);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        bool enviouComandoValido = false;

        string comandoLower = messageText.ToLowerInvariant().Trim();

        // Processa comando área
        if (comandoLower.StartsWith("/area_"))
        {
            string area = comandoLower.Replace("/area_", "");
            if (FiltrosVaga.SinonimosArea.ContainsKey(area) || area == "todos")
            {
                usuario.AreaAtiva = area;
                enviouComandoValido = true;
                await botClient.SendMessage(chatId, $"✅ Área atualizada para: *{area.ToUpper()}*", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
        }
        // Processa comando nível
        else if (comandoLower.StartsWith("/nivel_"))
        {
            string nivel = comandoLower.Replace("/nivel_", "");
            var niveisValidos = new[] { "todos", "iniciantes", "pleno", "senior" };
            if (niveisValidos.Contains(nivel))
            {
                usuario.NivelAtivo = nivel;
                enviouComandoValido = true;
                await botClient.SendMessage(chatId, $"✅ Nível atualizado para: *{nivel.ToUpper()}*", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
        }

        if (comandoLower == "/start" || !enviouComandoValido)
        {
            var msgMenu = $@"🤖 Olá! Sou seu Caçador de Vagas Tech.

                ⚙️ *Configuração Atual:*
                Área: *{usuario.AreaAtiva.ToUpper()}* | Nível: *{usuario.NivelAtivo.ToUpper()}*

                👇 *MUDAR ÁREA:*
                /area_todos | /area_backend | /area_frontend | /area_dados | /area_qa | /area_mobile

                👇 *MUDAR NÍVEL:*
                /nivel_todos (Traz TUDO)
                /nivel_iniciantes (Estágio \+ Jr \+ Trainee)
                /nivel_pleno (Bônus)
                /nivel_senior (Bônus)";

            await botClient.SendMessage(
                chatId: chatId,
                text: msgMenu,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
        }

        if (enviouComandoValido)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Envia amostra das vagas do banco que batem com o novo filtro
            await EnviarAmostrasDoBancoAsync(chatId, usuario, dbContext, telegramService, cancellationToken);
        }
    }

    private async Task EnviarAmostrasDoBancoAsync(string chatId, UsuarioConfig usuario, AppDbContext dbContext, TelegramService telegramService, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(chatId, "🔍 *Buscando Vagas Recentes com o seu novo filtro...*", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);

        // Busca as 50 últimas para não puxar muito da memória
        var ultimasVagas = await dbContext.Vagas
            .OrderByDescending(v => v.DataPublicacao)
            .Take(50)
            .ToListAsync(cancellationToken);

        var amostras = ultimasVagas
            .Where(v => FiltrosVaga.VagaValidaParaUsuario(v, usuario))
            .Take(5)
            .ToList();

        if (amostras.Any())
        {
            await telegramService.EnviarVagasParaUsuarioAsync(amostras, long.Parse(chatId), cancellationToken);
        }
        else
        {
             await _botClient.SendMessage(chatId, "🤷‍♂️ Não encontrei vagas recentes no banco para este filtro.", cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Erro na API do Telegram:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(ErrorMessage);
        return Task.CompletedTask;
    }
}