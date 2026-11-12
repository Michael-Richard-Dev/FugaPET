namespace FugaPET_Dev.Modelo.Ambiente;

/// <summary>
/// GATE 126X: PROVA de que o runtime esta materialmente no ambiente QAS110 — perfil Q reconhecido na
/// configuracao local E destino SAP Q validado pelo contrato de inicializacao ja existente.
/// <para>
/// NAO E FORJAVEL pela superficie publica: o construtor e privado e nao existe fabrica publica. A unica
/// forma de obter uma instancia e <c>Servicos.Ambiente.ValidadorIdentidadeQas110.Resolver(...)</c>, que
/// executa a validacao real. Uma declaracao isolada "ambiente=Q" NAO produz identidade.
/// </para>
/// <para>
/// O tipo e intencionalmente opaco: carrega apenas o que foi COMPROVADO (mandante, host e porta
/// efetivamente validados), para diagnostico. Nao carrega peso, nao carrega capability e nao concede
/// escrita.
/// </para>
/// </summary>
public sealed class IdentidadeAmbienteQas110
{
    private IdentidadeAmbienteQas110(string sapClient, string hostSap, int portaSap)
    {
        SapClient = sapClient;
        HostSap = hostSap;
        PortaSap = portaSap;
    }

    /// <summary>Mandante SAP COMPROVADO na configuracao validada.</summary>
    public string SapClient { get; }

    /// <summary>Host SAP COMPROVADO (igualdade exata, nunca substring).</summary>
    public string HostSap { get; }

    /// <summary>Porta SAP COMPROVADA.</summary>
    public int PortaSap { get; }

    /// <summary>
    /// Emissao da identidade. <c>internal</c> de proposito: fica fora da superficie publica e o unico
    /// chamador de producao e o validador de identidade. Nunca chamar sem ter validado o destino.
    /// </summary>
    internal static IdentidadeAmbienteQas110 Emitir(string sapClient, string hostSap, int portaSap)
        => new(sapClient, hostSap, portaSap);
}

/// <summary>GATE 126X: desfechos da resolucao de identidade. Nenhum deles e permissivo.</summary>
public enum CenarioIdentidadeQas110
{
    /// <summary>Perfil Q e destino SAP Q comprovados.</summary>
    Confirmada = 0,

    /// <summary>Perfil local nao e Q (ausente, vazio, malformado ou desconhecido).</summary>
    PerfilNaoQ = 1,

    /// <summary>Destino SAP nao corresponde ao Q autorizado (host/porta/allowlist/sap-client).</summary>
    DestinoNaoQ = 2,

    /// <summary>Configuracao ausente para decidir.</summary>
    ConfiguracaoAusente = 3,

    /// <summary>Rota CPI/INT012 presente porem inconsistente com a propria allowlist.</summary>
    RotaCpiInconsistente = 4
}

/// <summary>
/// GATE 126X: resultado TRI-STATE da resolucao de identidade QAS110. <see cref="Identidade"/> e
/// <c>null</c> em todo desfecho que nao seja <see cref="CenarioIdentidadeQas110.Confirmada"/>.
/// </summary>
public sealed class ResultadoIdentidadeQas110
{
    private ResultadoIdentidadeQas110(
        CenarioIdentidadeQas110 cenario,
        IdentidadeAmbienteQas110? identidade,
        string mensagem)
    {
        Cenario = cenario;
        Identidade = identidade;
        Mensagem = mensagem;
    }

    public CenarioIdentidadeQas110 Cenario { get; }

    public IdentidadeAmbienteQas110? Identidade { get; }

    public string Mensagem { get; }

    public bool Confirmada => Cenario == CenarioIdentidadeQas110.Confirmada && Identidade is not null;

    internal static ResultadoIdentidadeQas110 Ok(IdentidadeAmbienteQas110 identidade)
        => new(CenarioIdentidadeQas110.Confirmada, identidade, string.Empty);

    internal static ResultadoIdentidadeQas110 Bloqueada(CenarioIdentidadeQas110 cenario, string mensagem)
        => new(cenario, null, mensagem);
}
