using JobHunterBot.Models;

namespace JobHunterBot.Scrapers;

public interface IVagaScraper
{
    Task<List<Vaga>> BuscarVagasAsync(CancellationToken cancellationToken = default);
}