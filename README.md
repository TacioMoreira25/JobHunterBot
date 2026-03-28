# JobHunterBot

JobHunterBot é um Worker Service em C# (.NET) que atua como um agregador de vagas de tecnologia. Ele realiza web scraping em diversas plataformas de emprego e notifica usuários diretamente via Telegram, de acordo com seus filtros personalizados de nível e área de atuação.

## Funcionalidades Principais

- **Múltiplas Fontes de Vagas:** Integração com Gupy, Programathor, Vagas.com, Remotar, GitHub Issues e LinkedIn (via RapidAPI JSearch).
- **Filtros Dinâmicos no Telegram:** Os usuários podem interagir com o bot usando comandos como `/nivel_junior` ou `/area_backend` para definir suas preferências que ficam salvas no banco de dados.
- **Filtragem Inteligente de Lixo:** Remove "Bancos de Talentos" e cruza níveis para evitar spam (ex: mandar vaga Sênior quando o filtro é Júnior).
- **Resiliência e Performance:** Scrapers independentes, chamadas HTTP resilientes contra falhas transitórias e persistência anti-duplicidade garantida pelo SQLite.
- **Match Destacado:** Realça vagas de frameworks e linguagens de interesse principal (como C# / .NET) com emojis de destaque no envio pelo Telegram.

## Tecnologias

- **C# / .NET 10** (BackgroundService / Worker)
- **Entity Framework Core** (SQLite) para armazenamento do estado do usuário e vagas enviadas.
- **Telegram.Bot** para interface de entrega e comandos de usuário.
- **HtmlAgilityPack** para parseamento de DOM e extração via XPath.
- **Microsoft.Extensions.Http.Resilience** para pipelines http confiáveis.

## Configuração e Execução

1. Configure seu arquivo `appsettings.json` (ou `appsettings.Development.json`) preenchendo as 3 credenciais exigidas:
   - `Telegram:BotToken`: O token de acesso do seu bot (fornecido pelo BotFather).
   - `Telegram:ChatId`: O ID do chat no qual o bot fará o envio geral.
   - `RapidApi:Key`: Sua chave de acesso à JSearch API do LinkedIn na RapidAPI.
2. Execute a aplicação baixando os pacotes e rodando o projeto:
   ```bash
   dotnet restore
   dotnet run
   ```
3. O Entity Framework irá criar automaticamente o banco de dados `JobHunterBot.db` na inicialização, se não existir.
4. No Telegram, mande um `/start` para o bot criado por você para começar a receber as notificações.
