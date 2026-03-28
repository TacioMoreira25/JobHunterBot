using JobHunterBot.Models;

namespace JobHunterBot.Common;

public static class FiltrosVaga
{
    public static readonly List<string> PalavrasProibidas = new()
    {
        "banco de talentos", "pedagogia", "fiscal", "administrativo", "suporte comercial", 
        "vendas", "marketing", "rh", "direito", "contábil", "estoque", "mecânica", "civil", 
        "produção", "professor", "tutor", "voluntário", "jovem aprendiz", "enfermagem", 
        "odontologia", "psicologia", "nutrição", "farmácia", "medicina", "veterinária", 
        "atendimento", "recepcionista", "telemarketing", "call center", "caixa", "vendedor", 
        "logística", "motorista", "limpeza", "eletrotécnica", "jornalismo", "social media", 
        "coordenador", "gerente", "diretor", "tech lead"
    };

    public static readonly Dictionary<string, List<string>> SinonimosNivel = new(StringComparer.OrdinalIgnoreCase)
    {
        { "estagio", new List<string> { "estágio", "estagio", "intern", "internship", "estagiário", "estagiario" } },
        { "junior", new List<string> { "júnior", "junior", "jr", "entry level" } },
        { "trainee", new List<string> { "trainee", "recém-formado" } },
        { "pleno", new List<string> { "pleno", "pl", "mid-level", "mid level" } },
        { "senior", new List<string> { "sênior", "senior", "sr" } }
    };

    public static readonly Dictionary<string, List<string>> SinonimosArea = new(StringComparer.OrdinalIgnoreCase)
    {
        { "backend", new List<string> { "backend", "back-end", "back end", "c#", ".net", "java", "python", "php", "node", "golang", "ruby" } },
        { "frontend", new List<string> { "frontend", "front-end", "front end", "react", "angular", "vue", "javascript", "typescript", "html", "css" } },
        { "dados", new List<string> { "dados", "data", "engenharia de dados", "ciência de dados", "análise de dados", "machine learning", "ia", "sql", "python", "bi" } },
        { "qa", new List<string> { "qa", "quality assurance", "teste", "tester", "automação", "software engineer in test" } },
        { "mobile", new List<string> { "mobile", "flutter", "dart", "android", "ios", "kotlin", "swift", "react native" } }
    };

    public static bool ContemPalavrasProibidas(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var textoLower = texto.ToLowerInvariant();
        return PalavrasProibidas.Any(p => textoLower.Contains(p));
    }

    public static bool AtendeFiltroNivel(string titulo, string nivelAtivo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return false;
        string tituloLower = titulo.ToLowerInvariant();

        if (nivelAtivo.Equals("todos", StringComparison.OrdinalIgnoreCase)) return true;

        if (nivelAtivo.Equals("iniciantes", StringComparison.OrdinalIgnoreCase))
        {
            var aceitos = SinonimosNivel["estagio"].Concat(SinonimosNivel["junior"]).Concat(SinonimosNivel["trainee"]);
            return aceitos.Any(s => tituloLower.Contains(s));
        }

        if (nivelAtivo.Equals("pleno", StringComparison.OrdinalIgnoreCase))
        {
            return SinonimosNivel["pleno"].Any(s => tituloLower.Contains(s));
        }

        if (nivelAtivo.Equals("senior", StringComparison.OrdinalIgnoreCase))
        {
            return SinonimosNivel["senior"].Any(s => tituloLower.Contains(s));
        }

        return false;
    }

    public static bool AtendeFiltroArea(string titulo, string areaAtiva)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return false;
        string tituloLower = titulo.ToLowerInvariant();

        if (areaAtiva.Equals("todos", StringComparison.OrdinalIgnoreCase)) return true;

        var chave = areaAtiva.Replace("area_", "").ToLowerInvariant();

        if (SinonimosArea.TryGetValue(chave, out var sinonimos))
        {
             return sinonimos.Any(s => tituloLower.Contains(s));
        }
        
        return true; // Se a área não estiver mapeada, assume que atende pra não perder a vaga
    }

    public static bool VagaValidaParaUsuario(Vaga vaga, UsuarioConfig usuario)
    {
        if (ContemPalavrasProibidas(vaga.Titulo)) return false;

        bool atendeNivel = AtendeFiltroNivel(vaga.Titulo, usuario.NivelAtivo);
        bool atendeArea = AtendeFiltroArea(vaga.Titulo, usuario.AreaAtiva);

        return atendeNivel && atendeArea;
    }
}