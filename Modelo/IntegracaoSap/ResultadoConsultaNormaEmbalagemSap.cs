namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Cenário tipado da consulta (somente leitura) da norma de embalagem (INT012). Substitui o antigo
/// "tudo vira null" — a Tela decide a mensagem a partir do cenário (não exibe "SEM NORMA CADASTRADA"
/// para todo erro). Ver <see cref="ResultadoConsultaNormaEmbalagemSap"/>.
/// </summary>
public enum CenarioConsultaNormaEmbalagem
{
    /// <summary>API respondeu 200 e a norma foi parseada (interpretação ainda pode bloquear no controller).</summary>
    Encontrada,

    /// <summary>Falta configuração da API de embalagem (URL/allowlist ausentes).</summary>
    NaoConfigurada,

    /// <summary>URL/allowlist presentes, porém falta usuário/senha PRÓPRIOS da embalagem.</summary>
    CredencialAusente,

    /// <summary>O host do endpoint não está na allowlist própria da embalagem.</summary>
    HostNaoPermitido,

    /// <summary>API respondeu corretamente (200 sem registro ou 404): não há cadastro de norma.</summary>
    NaoEncontrada,

    /// <summary>HTTP 401 — credenciais inválidas/expiradas.</summary>
    ErroAutenticacao,

    /// <summary>HTTP 403 — sem autorização para o recurso.</summary>
    ErroAutorizacao,

    /// <summary>Demais respostas não 2xx (ex.: 500).</summary>
    ErroHttp,

    /// <summary>Tempo limite de rede/servidor.</summary>
    Timeout,

    /// <summary>Resposta 2xx porém não parseável (JSON inválido).</summary>
    RespostaInvalida,

    /// <summary>Consulta cancelada pelo chamador.</summary>
    Cancelada
}

/// <summary>
/// Resultado tipado da consulta da norma de embalagem. NUNCA carrega segredo (usuário/senha/Authorization/
/// cookie/token). Mensagem e URL são SANITIZADAS. <see cref="Norma"/> só vem preenchida no cenário
/// <see cref="CenarioConsultaNormaEmbalagem.Encontrada"/>.
/// </summary>
public sealed class ResultadoConsultaNormaEmbalagemSap
{
    private ResultadoConsultaNormaEmbalagemSap(
        CenarioConsultaNormaEmbalagem cenario,
        ConsultaNormaEmbalagemSapResponse? norma,
        int? httpStatus,
        string mensagemSanitizada,
        string urlSanitizada,
        string correlationIdConsulta)
    {
        Cenario = cenario;
        Norma = norma;
        HttpStatus = httpStatus;
        MensagemSanitizada = mensagemSanitizada;
        UrlSanitizada = urlSanitizada;
        CorrelationIdConsulta = correlationIdConsulta;
    }

    public CenarioConsultaNormaEmbalagem Cenario { get; }

    /// <summary>Resposta crua parseada — apenas quando <see cref="Cenario"/> = Encontrada.</summary>
    public ConsultaNormaEmbalagemSapResponse? Norma { get; }

    public int? HttpStatus { get; }

    public string MensagemSanitizada { get; }

    public string UrlSanitizada { get; }

    public string CorrelationIdConsulta { get; }

    public bool Encontrada => Cenario == CenarioConsultaNormaEmbalagem.Encontrada;

    public static ResultadoConsultaNormaEmbalagemSap DeEncontrada(
        ConsultaNormaEmbalagemSapResponse norma, int? httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.Encontrada, norma, httpStatus,
            "Norma de embalagem consultada.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeNaoConfigurada(string urlSanitizada = "")
        => new(CenarioConsultaNormaEmbalagem.NaoConfigurada, null, null,
            "Consulta da norma de embalagem não configurada.", urlSanitizada, string.Empty);

    public static ResultadoConsultaNormaEmbalagemSap DeCredencialAusente(string urlSanitizada = "")
        => new(CenarioConsultaNormaEmbalagem.CredencialAusente, null, null,
            "Credenciais próprias da API de embalagem ausentes.", urlSanitizada, string.Empty);

    public static ResultadoConsultaNormaEmbalagemSap DeHostNaoPermitido(string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.HostNaoPermitido, null, null,
            "Host do endpoint de embalagem fora da allowlist.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeNaoEncontrada(int? httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.NaoEncontrada, null, httpStatus,
            "Sem cadastro de norma de embalagem para o material.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeErroAutenticacao(int httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.ErroAutenticacao, null, httpStatus,
            "Falha de autenticação na API de embalagem (HTTP 401).", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeErroAutorizacao(int httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.ErroAutorizacao, null, httpStatus,
            "Acesso não autorizado à API de embalagem (HTTP 403).", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeErroHttp(int httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.ErroHttp, null, httpStatus,
            $"Erro HTTP {httpStatus} na consulta da norma de embalagem.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeTimeout(string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.Timeout, null, null,
            "Tempo limite na consulta da norma de embalagem.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeRespostaInvalida(int? httpStatus, string urlSanitizada, string correlationId)
        => new(CenarioConsultaNormaEmbalagem.RespostaInvalida, null, httpStatus,
            "Resposta inválida da API de embalagem.", urlSanitizada, correlationId);

    public static ResultadoConsultaNormaEmbalagemSap DeCancelada(string urlSanitizada = "")
        => new(CenarioConsultaNormaEmbalagem.Cancelada, null, null,
            "Consulta da norma de embalagem cancelada.", urlSanitizada, string.Empty);
}
