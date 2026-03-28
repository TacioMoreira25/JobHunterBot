using System.Net.Http.Headers;
using HtmlAgilityPack;
using JobHunterBot.Models;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class VagasComScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VagasComScraper> _logger;

    public VagasComScraper(HttpClient httpClient, ILogger<VagasComScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://www.vagas.com.br/");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
    }

    public async Task<List<Vaga>> BuscarVagasAsync(CancellationToken cancellationToken = default)
    {
        var vagas = new List<Vaga>();
        var termosDeBusca = new[] { "Desenvolvedor", "Programador", "Engenheiro de Software", "Backend", "Frontend", "Fullstack", "Dados", "QA", "C#", ".NET", "Python", "Java" };

        foreach (var termo in termosDeBusca)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Iniciando busca no Vagas.com para o termo: {Termo}", termo);

                var termoFormatado = Uri.EscapeDataString(termo.Replace(" ", "-").ToLower());
                var url = $"vagas-de-{termoFormatado}";
                
                var html = await _httpClient.GetStringAsync(url, cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//li[contains(@class, 'vaga')]");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var linkNode = node.SelectSingleNode(".//a[contains(@class, 'link-detalhes-vaga')]");
                        if (linkNode == null) continue;

                        var href = linkNode.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrEmpty(href)) continue;

                        var absoluteUrl = href.StartsWith("http") ? href : new Uri(_httpClient.BaseAddress!, href).ToString();

                        if (!vagas.Any(v => v.Url == absoluteUrl))
                        {
                            var titulo = linkNode.InnerText.Trim() ?? "Vaga Desconhecida";
                            var empresaNode = node.SelectSingleNode(".//span[contains(@class, 'emprVaga')]");
                            var empresa = empresaNode?.InnerText.Trim() ?? "Confidencial";

                            vagas.Add(new Vaga
                            {
                                Titulo = titulo,
                                Empresa = empresa,
                                Descricao = "Detalhes disponíveis na página do Vagas.com.",
                                Url = absoluteUrl,
                                DataPublicacao = DateTime.UtcNow,
                                Fonte = "Vagas.com"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no scraping do Vagas.com para o termo {Termo}.", termo);
            }
        }

        return vagas;
    }
}