using System.Net.Http.Headers;
using HtmlAgilityPack;
using JobHunterBot.Models;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class ProgramathorScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProgramathorScraper> _logger;

    public ProgramathorScraper(HttpClient httpClient, ILogger<ProgramathorScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://programathor.com.br/");
    }

    public async Task<List<Vaga>> BuscarVagasAsync(CancellationToken cancellationToken = default)
    {
        var vagas = new List<Vaga>();
        var termosDeBusca = new[] { "Estágio", "Junior", "Trainee", "Pleno", "Sênior" };

        foreach (var termo in termosDeBusca)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Iniciando busca no Programathor para o termo: {Termo}", termo);
                
                var url = $"jobs?q={Uri.EscapeDataString(termo)}";
                var html = await _httpClient.GetStringAsync(url, cancellationToken);
                
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@class, 'cell-list')]");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var href = node.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrEmpty(href)) continue;

                        var absoluteUrl = new Uri(_httpClient.BaseAddress!, href).ToString();

                        if (!vagas.Any(v => v.Url == absoluteUrl))
                        {
                            var tituloNode = node.SelectSingleNode(".//h3");
                            var titulo = tituloNode?.InnerText.Trim() ?? "Vaga Desconhecida";

                            var spans = node.SelectNodes(".//span");
                            var empresa = spans != null && spans.Count > 0 ? spans[0].InnerText.Trim() : "Confidencial";

                            vagas.Add(new Vaga
                            {
                                Titulo = titulo,
                                Empresa = empresa,
                                Descricao = "Acesse para ver os requisitos.",
                                Url = absoluteUrl,
                                DataPublicacao = DateTime.UtcNow,
                                Fonte = "Programathor"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no scraping do Programathor para o termo {Termo}.", termo);
            }
        }

        return vagas;
    }
}