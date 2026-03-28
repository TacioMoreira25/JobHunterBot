using JobHunterBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace JobHunterBot.Services;

public class TelegramService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramService> _logger;
    private readonly string? _chatId;

    public TelegramService(IConfiguration configuration, ILogger<TelegramService> logger)
    {
        _logger = logger;
        
        var botToken = configuration["Telegram:BotToken"];

        if (string.IsNullOrEmpty(botToken))
            _logger.LogWarning("Telegram BotToken não configurado.");
            
        // Criação rápida sem IoC estrito, ou pode ser injetado via DI no Program.cs
        _botClient = new TelegramBotClient(botToken ?? string.Empty);
    }

    public async Task EnviarVagasParaUsuarioAsync(IEnumerable<Vaga> vagasNovas, long chatId, CancellationToken cancellationToken = default)
    {
        foreach (var vaga in vagasNovas)
        {
            var isSuperMatch = vaga.Titulo.Contains("C#", StringComparison.OrdinalIgnoreCase) ||
                               vaga.Titulo.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
                               vaga.Descricao.Contains("C#", StringComparison.OrdinalIgnoreCase) ||
                               vaga.Descricao.Contains(".NET", StringComparison.OrdinalIgnoreCase);

            var textoDestaque = isSuperMatch 
                ? "🔥 *DESTAQUE SUPER MATCH \\- C\\# / \\.NET* 🔥\n" 
                : "*Nova Vaga Encontrada\\!*\n";

            var text = $$"""
            {{textoDestaque}}*Fonte:* {{EscapeMarkdownV2(vaga.Fonte)}}
            *Título:* {{EscapeMarkdownV2(vaga.Titulo)}}
            *Empresa:* {{EscapeMarkdownV2(vaga.Empresa)}}
            *Data Pública:* {{EscapeMarkdownV2(vaga.DataPublicacao.ToString("dd/MM/yyyy HH:mm"))}}

            [Candidatar\-se]({{EscapeMarkdownV2(vaga.Url)}})
            """;

            try
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2,
                    cancellationToken: cancellationToken
                );

                // Delay para evitar bloqueios de RATE LIMIT do Telegram
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no envio pelo Telegram.");
            }
        }
    }

    private string EscapeMarkdownV2(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        string[] reservedChars = { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        
        foreach (var ch in reservedChars)
        {
            input = input.Replace(ch, $"\\{ch}");
        }
        
        return input;
    }
}