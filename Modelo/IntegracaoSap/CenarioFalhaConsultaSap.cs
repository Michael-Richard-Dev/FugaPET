namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// GATE 105D: taxonomia de falhas da CONSULTA SAP. Existe para que uma falha TÉCNICA nunca mais seja
/// convertida semanticamente em um status de NEGÓCIO ("pedido não liberado"). O diagnóstico técnico é
/// classificado na borda da integração e atravessa as camadas sem perder contexto.
/// </summary>
public enum CenarioFalhaConsultaSap
{
    /// <summary>Consulta técnica bem-sucedida (o status de negócio é avaliado separadamente).</summary>
    Nenhum = 0,

    /// <summary>HTTP 401 — credenciais da integração não aceitas pelo SAP.</summary>
    NaoAutenticado = 1,

    /// <summary>HTTP 403 — credencial aceita, porém sem autorização para o recurso.</summary>
    SemAutorizacao = 2,

    /// <summary>HTTP 404 — recurso inexistente. NÃO é falha técnica: é um resultado conhecido.</summary>
    NaoEncontrado = 3,

    /// <summary>Tempo limite esgotado sem cancelamento solicitado pelo chamador.</summary>
    Timeout = 4,

    /// <summary>Falha de rede/DNS/socket sem resposta HTTP.</summary>
    Conectividade = 5,

    /// <summary>Falha de handshake/certificado TLS.</summary>
    Tls = 6,

    /// <summary>HTTP 5xx — falha do lado do servidor SAP.</summary>
    SapIndisponivel = 7,

    /// <summary>Resposta 2xx cujo corpo não pôde ser interpretado com segurança.</summary>
    RespostaInvalida = 8,

    /// <summary>Consulta não pôde sequer ser executada (config/endpoint/allowlist inválidos ou ausentes).</summary>
    ConfiguracaoInvalida = 9,

    /// <summary>HTTP não-sucesso fora das faixas classificadas acima.</summary>
    HttpNaoClassificado = 10,

    /// <summary>
    /// Resposta SAP tecnicamente VÁLIDA, mas cujo status de liberação não pôde ser determinado
    /// (status vazio ou não mapeado). Não é falha técnica e não é "pedido não liberado": é indeterminação
    /// de negócio, que bloqueia por segurança com mensagem própria.
    /// </summary>
    StatusNegocioDesconhecido = 11
}

/// <summary>
/// GATE 105D: textos de APLICAÇÃO (título + mensagem) por cenário. Fonte ÚNICA para a UI — a tela não
/// conhece HTTP, exceções nem detalhes de transporte; apenas apresenta o que esta tabela devolve.
/// Nenhum texto atribui causa errada (403 não fala em senha; rede/timeout não falam em credencial;
/// 5xx não fala em pedido não liberado).
/// </summary>
public static class MensagensFalhaConsultaSap
{
    public const string TituloNaoAutenticado = "Acesso ao SAP não autorizado";
    public const string TituloSemAutorizacao = "Acesso ao serviço SAP negado";
    public const string TituloNaoEncontrado = "Pedido de Compra não encontrado";
    public const string TituloTimeout = "SAP não respondeu";
    public const string TituloConectividade = "Falha de comunicação com o SAP";
    public const string TituloTls = "Falha de segurança na conexão com o SAP";
    public const string TituloSapIndisponivel = "SAP indisponível";
    public const string TituloRespostaInvalida = "Resposta do SAP inválida";
    public const string TituloConfiguracaoInvalida = "Configuração da integração SAP inválida";
    public const string TituloHttpNaoClassificado = "Falha na consulta ao SAP";
    public const string TituloStatusNegocioDesconhecido = "Status do pedido não pôde ser validado";

    /// <summary>Título e mensagem de aplicação do cenário. <paramref name="numeroPedido"/> só compõe o 404.</summary>
    public static (string Titulo, string Mensagem) Descrever(
        CenarioFalhaConsultaSap cenario,
        string? numeroPedido = null)
    {
        string pedido = string.IsNullOrWhiteSpace(numeroPedido) ? "(não informado)" : numeroPedido.Trim();

        return cenario switch
        {
            CenarioFalhaConsultaSap.NaoAutenticado => (
                TituloNaoAutenticado,
                "Não foi possível consultar o Pedido de Compra porque as credenciais da integração SAP "
                + "não foram aceitas.\r\n\r\n"
                + "Acione o suporte para validar o usuário e a senha configurados para a API SAP."),

            CenarioFalhaConsultaSap.SemAutorizacao => (
                TituloSemAutorizacao,
                "A integração conseguiu acessar o SAP, mas a credencial utilizada não possui autorização "
                + "para consultar este recurso.\r\n\r\n"
                + "Acione o suporte SAP para revisar as autorizações da conta de integração."),

            CenarioFalhaConsultaSap.NaoEncontrado => (
                TituloNaoEncontrado,
                $"Pedido de Compra {pedido} não foi encontrado no SAP."),

            CenarioFalhaConsultaSap.Timeout => (
                TituloTimeout,
                "O SAP não respondeu dentro do tempo limite.\r\n\r\n"
                + "Tente novamente em alguns instantes. Se o problema continuar, acione o suporte."),

            CenarioFalhaConsultaSap.Conectividade => (
                TituloConectividade,
                "Não foi possível estabelecer comunicação com o SAP.\r\n\r\n"
                + "Verifique a conectividade ou acione o suporte."),

            CenarioFalhaConsultaSap.Tls => (
                TituloTls,
                "Não foi possível estabelecer uma conexão segura com o SAP.\r\n\r\n"
                + "Acione o suporte para validar o certificado, o protocolo TLS e a configuração do serviço."),

            CenarioFalhaConsultaSap.SapIndisponivel => (
                TituloSapIndisponivel,
                "O SAP respondeu com uma falha temporária do serviço.\r\n\r\n"
                + "Tente novamente mais tarde ou acione o suporte."),

            CenarioFalhaConsultaSap.RespostaInvalida => (
                TituloRespostaInvalida,
                "O SAP respondeu à consulta, mas os dados recebidos não puderam ser interpretados com "
                + "segurança.\r\n\r\n"
                + "A entrada permanece bloqueada. Acione o suporte."),

            CenarioFalhaConsultaSap.ConfiguracaoInvalida => (
                TituloConfiguracaoInvalida,
                "A consulta não pôde ser executada porque a configuração da integração SAP está ausente "
                + "ou inválida.\r\n\r\n"
                + "Acione o suporte."),

            CenarioFalhaConsultaSap.StatusNegocioDesconhecido => (
                TituloStatusNegocioDesconhecido,
                "O SAP respondeu à consulta, mas não foi possível determinar com segurança o status de "
                + "liberação do pedido.\r\n\r\n"
                + "A entrada permanece bloqueada por segurança. Acione o suporte."),

            _ => (
                TituloHttpNaoClassificado,
                "A consulta ao Pedido de Compra no SAP não foi concluída.\r\n\r\n"
                + "A entrada permanece bloqueada por segurança. Acione o suporte.")
        };
    }
}
