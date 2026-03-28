using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobHunterBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobHunterBot.Scrapers;

public class LinkedInRapidApiScraper : IVagaScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInRapidApiScraper> _logger;
    private readonly string? _apiKey;
    private readonly string _apiHost = "jsearch.p.rapidapi.com";

    public LinkedInRapidApiScraper(HttpClient httpClient, IConfiguration configuration, ILogger<LinkedInRapidApiScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["RapidApi:Key"];
        var configHost = configuration["RapidApi:Host"];
        if (!string.IsNullOrEmpty(configHost))
        {
             _apiHost = configHost;
        }

        _httpClient.BaseAddress = new Uri($"https://{_apiHost}/");
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Host", _apiHost);
        }
    }

    public async Task<List<Vaga>> BuscarVagasAsync(CancellationToken cancellationToken = default)
    {
        var vagas = new List<Vaga>();

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("RapidApi:Key não configurada. Pulando LinkedInRapidApiScraper.");
            return vagas;
        }

        try
        {
            var termoAmplo = "Desenvolvedor no Brasil"; // Termo Único - Consome apenas 1 request!
            _logger.LogInformation("Iniciando busca na API RapidAPI (LinkedIn/JSearch) para a cota única: '{Termo}'", termoAmplo);

            var query = Uri.EscapeDataString(termoAmplo);
            var url = $"search?query={query}&page=1&num_pages=1&country=br&date_posted=today";

            var response = await _httpClient.GetFromJsonAsync<RapidApiJobResponse>(url, cancellationToken);

            if (response?.Data != null)
            {
                foreach (var job in response.Data)
                {
                    // Evita vagas sem link
                    if (string.IsNullOrEmpty(job.JobApplyLink)) continue;

                    vagas.Add(new Vaga
                    {
                        Titulo = job.JobTitle ?? "Vaga sem título",
                        Empresa = job.EmployerName ?? "Não Informada",
                        Descricao = $"Modalidade: {job.JobEmploymentTypeText ?? "Não definida"}.", // Pode ser preenchida com job_description se disp.
                        Url = job.JobApplyLink,
                        DataPublicacao = DateTime.UtcNow, // Fallback requisitado
                        Fonte = "LinkedIn via RapidAPI"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar dados na RapidAPI na requisição única.");
        }

        return vagas;
    }

    // DTOs baseados na resposta do payload fornecido
    private class RapidApiJobResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("data")]
        public List<RapidApiJobData>? Data { get; set; }
    }

    private class RapidApiJobData
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        [JsonPropertyName("job_title")]
        public string? JobTitle { get; set; }

        [JsonPropertyName("employer_name")]
        public string? EmployerName { get; set; }

        [JsonPropertyName("job_apply_link")]
        public string? JobApplyLink { get; set; }

        [JsonPropertyName("job_employment_type_text")]
        public string? JobEmploymentTypeText { get; set; }
    }
}