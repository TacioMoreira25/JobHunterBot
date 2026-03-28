using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobHunterBot.Models;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class GupyScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GupyScraper> _logger;

    public GupyScraper(HttpClient httpClient, ILogger<GupyScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://portal.api.gupy.io/");
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
                _logger.LogInformation("Iniciando busca na Gupy para o termo: {Termo}", termo);
                
                var url = $"api/v1/jobs?jobName={Uri.EscapeDataString(termo)}&limit=20";
                var response = await _httpClient.GetFromJsonAsync<GupyApiResponse>(url, cancellationToken);

                if (response?.Data != null)
                {
                    foreach (var item in response.Data)
                    {
                        var absoluteUrl = item.JobUrl;
                        if (!vagas.Any(v => v.Url == absoluteUrl))
                        {
                            vagas.Add(new Vaga
                            {
                                Titulo = item.Name ?? "Vaga sem título",
                                Empresa = item.CompanyName ?? "Confidencial",
                                Descricao = "Ver detalhes no link da vaga.",
                                Url = absoluteUrl,
                                DataPublicacao = item.PublishedDate ?? DateTime.UtcNow,
                                Fonte = "Gupy"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar vagas na Gupy para o termo {Termo}.", termo);
            }
        }

        return vagas;
    }

    private class GupyApiResponse
    {
        [JsonPropertyName("data")]
        public List<GupyJob>? Data { get; set; }
    }

    private class GupyJob
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("jobUrl")]
        public required string JobUrl { get; set; }

        [JsonPropertyName("publishedDate")]
        public DateTime? PublishedDate { get; set; }
    }
}