using System.Net.Http.Headers;
using HtmlAgilityPack;
using JobHunterBot.Models;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class RemotarScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemotarScraper> _logger;

    public RemotarScraper(HttpClient httpClient, ILogger<RemotarScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://remotar.com.br/");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
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
                _logger.LogInformation("Iniciando busca no Remotar para o termo: {Termo}", termo);

                var url = $"search/jobs?q={Uri.EscapeDataString(termo)}";
                var html = await _httpClient.GetStringAsync(url, cancellationToken);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var nodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/job/')]");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var href = node.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrEmpty(href)) continue;

                        var absoluteUrl = new Uri(_httpClient.BaseAddress!, href).ToString();

                        if (!vagas.Any(x => x.Url == absoluteUrl))
                        {
                            var tituloNode = node.SelectSingleNode(".//h1") ?? node.SelectSingleNode(".//h2") ?? node.SelectSingleNode(".//h3");
                            var titulo = tituloNode?.InnerText.Trim() ?? "Vaga Remota";

                            var descNodes = node.SelectNodes(".//p") ?? node.SelectNodes(".//span");
                            var empresa = descNodes != null && descNodes.Count > 0 ? descNodes[0].InnerText.Trim() : "Não especificada";

                            vagas.Add(new Vaga
                            {
                                Titulo = titulo,
                                Empresa = empresa,
                                Descricao = "Informações completas no link do Remotar.",
                                Url = absoluteUrl,
                                DataPublicacao = DateTime.UtcNow,
                                Fonte = "Remotar"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar vagas no Remotar para o termo {Termo}.", termo);
            }
        }

        return vagas;
    }
}