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
            usuario = new UsuarioConfig { ChatId = chatId, AreaAtiva = "todos", NivelAtivo = "iniciantes", LocalizacaoAtiva = "todas" };
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
                await botClient.SendMessage(chatId, $"Área atualizada para: <b>{area.ToUpper()}</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
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
                await botClient.SendMessage(chatId, $"Nível atualizado para: <b>{nivel.ToUpper()}</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
            }
        }
        // Processa comando localização
        else if (comandoLower.StartsWith("/loc_"))
        {
            string loc = comandoLower.Replace("/loc_", "");
            if (FiltrosVaga.SinonimosLocalizacao.ContainsKey(loc) || loc == "todas")
            {
                usuario.LocalizacaoAtiva = loc;
                enviouComandoValido = true;
                await botClient.SendMessage(chatId, $"Localização atualizada para: <b>{loc.ToUpper()}</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
            }
        }
        else if (comandoLower == "/combo_back_jr_remoto")
        {
            usuario.AreaAtiva = "backend";
            usuario.NivelAtivo = "iniciantes";
            usuario.LocalizacaoAtiva = "remoto";
            enviouComandoValido = true;
            await botClient.SendMessage(chatId, "Configuração rápida aplicada: <b>Backend + Iniciantes + Remoto</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
        else if (comandoLower == "/combo_front_jr_remoto")
        {
            usuario.AreaAtiva = "frontend";
            usuario.NivelAtivo = "iniciantes";
            usuario.LocalizacaoAtiva = "remoto";
            enviouComandoValido = true;
            await botClient.SendMessage(chatId, "Configuração rápida aplicada: <b>Frontend + Iniciantes + Remoto</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
        else if (comandoLower == "/combo_fullstack_jr_remoto")
        {
            usuario.AreaAtiva = "fullstack";
            usuario.NivelAtivo = "iniciantes";
            usuario.LocalizacaoAtiva = "remoto";
            enviouComandoValido = true;
            await botClient.SendMessage(chatId, "Configuração rápida aplicada: <b>Fullstack + Iniciantes + Remoto</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }

        if (comandoLower == "/start" || !enviouComandoValido)
        {
            var msgMenu = $@"<b>Menu do Caçador de Vagas Tech</b>

<b>Sua Configuração Atual:</b>
Área: <b>{usuario.AreaAtiva.ToUpper()}</b>
Nível: <b>{usuario.NivelAtivo.ToUpper()}</b>
Localização: <b>{usuario.LocalizacaoAtiva.ToUpper()}</b>

<b>CONFIGURAÇÕES RÁPIDAS (COMBOS):</b>
/combo_back_jr_remoto (Backend + Iniciante + Remoto)
/combo_front_jr_remoto (Frontend + Iniciante + Remoto)
/combo_fullstack_jr_remoto (Fullstack + Iniciante + Remoto)

<b>PERSONALIZAR ÁREA:</b>
/area_todos | /area_backend | /area_frontend
/area_fullstack | /area_dados | /area_qa

<b>PERSONALIZAR NÍVEL:</b>
/nivel_todos (Traz tudo)
/nivel_iniciantes (Estágio, Jr, Trainee)
/nivel_pleno (Pleno e Mid-level)
/nivel_senior (Sênior)

<b>PERSONALIZAR LOCALIZAÇÃO:</b>
/loc_todas | /loc_remoto | /loc_presencial";

            await botClient.SendMessage(
                chatId: chatId,
                text: msgMenu,
                parseMode: ParseMode.Html,
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
        await _botClient.SendMessage(chatId, "<b>Buscando Vagas Recentes com o seu novo filtro...</b>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);

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