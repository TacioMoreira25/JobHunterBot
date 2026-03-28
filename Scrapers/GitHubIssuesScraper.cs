using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobHunterBot.Models;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class GitHubIssuesScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubIssuesScraper> _logger;

    public GitHubIssuesScraper(HttpClient httpClient, ILogger<GitHubIssuesScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        
        // A API do GitHub exige um User-Agent válido
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JobHunterBot", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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
                _logger.LogInformation("Iniciando busca no GitHub Issues (backend-br/vagas) para o termo: {Termo}", termo);
                
                // Considerando que o termo de busca mapeia para 'labels'
                var url = $"repos/backend-br/vagas/issues?labels={Uri.EscapeDataString(termo)}&state=open&per_page=30";
                
                var issues = await _httpClient.GetFromJsonAsync<List<GitHubIssue>>(url, cancellationToken);

                if (issues != null)
                {
                    foreach (var issue in issues)
                    {
                        // As próprias issues de repositórios open-source muitas vezes colocam a empresa
                        // no título ou usam nomes de devs/orgs.
                        if (!vagas.Any(x => x.Url == issue.HtmlUrl))
                        {
                            vagas.Add(new Vaga
                            {
                                Titulo = issue.Title,
                                Empresa = issue.User?.Login ?? "Desconhecida", 
                                Descricao = issue.Body ?? string.Empty,
                                Url = issue.HtmlUrl,
                                DataPublicacao = issue.CreatedAt,
                                Fonte = "GitHub (backend-br/vagas)"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar vagas no GitHub para o termo {Termo}.", termo);
            }
        }

        return vagas;
    }

    private class GitHubIssue
    {
        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public required string HtmlUrl { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("user")]
        public GitHubUser? User { get; set; }
    }

    private class GitHubUser
    {
        [JsonPropertyName("login")]
        public string? Login { get; set; }
    }
}