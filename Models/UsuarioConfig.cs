using System.ComponentModel.DataAnnotations;

namespace JobHunterBot.Models;

public class UsuarioConfig
{
    [Key]
    public required string ChatId { get; set; }
    
    public string AreaAtiva { get; set; } = "todos";
    
    public string NivelAtivo { get; set; } = "iniciantes";
    
    public string LocalizacaoAtiva { get; set; } = "todas";
}