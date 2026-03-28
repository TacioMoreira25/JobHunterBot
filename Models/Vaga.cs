namespace JobHunterBot.Models;

public class Vaga
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Titulo { get; set; }
    public required string Empresa { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public required string Url { get; set; }
    public DateTime DataPublicacao { get; set; }
    public required string Fonte { get; set; }
}