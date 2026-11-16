using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Ambiente;

public static class ValidadorAmbienteQ
{
    public const string AmbienteEsperado = "Q";
    public const string VariavelAmbienteAppEnv = "FUGAPET_Q_APP_ENV";
    public const string SapHostEsperado = "vhfufqs4ci.sap.fugacouros.com.br";
    public const int SapPortaEsperada = 44300;
    public const string DatabaseEsperado = "fuga_jales_local_homologacao_q_v1_2";
    public const string SchemaEsperado = "homologacao";
    public const string RoleAplicacaoEsperada = "fugapet_q_app";

    public static ResultadoValidacaoAmbienteQ ValidarStartup(
        ConfiguracaoSap configuracaoSap,
        ConfiguracaoBancoPostgreSql configuracaoBanco,
        Func<string, string?> obterVariavelAmbiente)
        => ValidarStartup(configuracaoSap, configuracaoBanco, obterVariavelAmbiente, perfilAmbiente: null);

    /// <summary>
    /// GATE 120G: startup com o perfil de ambiente LOCAL, para o EXE iniciado por duplo clique.
    /// Perfil null/nao reconhecido mantem o comportamento anterior (exige FUGAPET_Q_APP_ENV=Q).
    /// </summary>
    public static ResultadoValidacaoAmbienteQ ValidarStartup(
        ConfiguracaoSap configuracaoSap,
        ConfiguracaoBancoPostgreSql configuracaoBanco,
        Func<string, string?> obterVariavelAmbiente,
        PerfilAmbienteLocal? perfilAmbiente)
    {
        ResultadoValidacaoAmbienteQ appEnv = ValidarAppEnv(obterVariavelAmbiente, perfilAmbiente);
        if (!appEnv.Valido)
        {
            return appEnv;
        }

        ResultadoValidacaoAmbienteQ sap = ValidarSap(configuracaoSap);
        if (!sap.Valido)
        {
            return sap;
        }

        return ValidarBanco(configuracaoBanco);
    }

    public static ResultadoValidacaoAmbienteQ ValidarAppEnv(Func<string, string?> obterVariavelAmbiente)
        => ValidarAppEnv(obterVariavelAmbiente, perfilAmbiente: null);

    /// <summary>
    /// GATE 120G: o ambiente passa a poder ser determinado pela configuracao LOCAL, nao apenas por
    /// variavel de ambiente criada por script externo. Precedencia:
    /// <list type="number">
    /// <item>FUGAPET_Q_APP_ENV presente ⇒ decide (e precisa valer Q);</item>
    /// <item>ausente ⇒ perfil local Q;</item>
    /// <item>nenhum dos dois ⇒ BLOQUEADO (fail-closed).</item>
    /// </list>
    /// AMBIGUIDADE e tratada como bloqueio: variavel presente com valor != Q NAO e "corrigida" pelo
    /// perfil local, mesmo que o perfil diga Q.
    /// </summary>
    public static ResultadoValidacaoAmbienteQ ValidarAppEnv(
        Func<string, string?> obterVariavelAmbiente,
        PerfilAmbienteLocal? perfilAmbiente)
    {
        ArgumentNullException.ThrowIfNull(obterVariavelAmbiente);

        string valor = obterVariavelAmbiente(VariavelAmbienteAppEnv)?.Trim() ?? string.Empty;
        if (valor.Length > 0)
        {
            return string.Equals(valor, AmbienteEsperado, StringComparison.OrdinalIgnoreCase)
                ? ResultadoValidacaoAmbienteQ.Ok()
                : ResultadoValidacaoAmbienteQ.Bloqueado(
                    $"Ambiente Q bloqueado: {VariavelAmbienteAppEnv} presente com valor diferente de Q. "
                    + "O perfil local NAO sobrepoe um ambiente declarado explicitamente.");
        }

        if (perfilAmbiente?.EhQ == true)
        {
            return ResultadoValidacaoAmbienteQ.Ok();
        }

        return ResultadoValidacaoAmbienteQ.Bloqueado(
            $"Ambiente Q bloqueado: defina {VariavelAmbienteAppEnv}=Q ou declare "
            + $"\"{PerfilAmbienteLocal.SecaoAmbiente}\": {{ \"{PerfilAmbienteLocal.ChavePerfil}\": "
            + $"\"{PerfilAmbienteLocal.NomePerfilQ}\" }} na configuracao local.");
    }

    public static ResultadoValidacaoAmbienteQ ValidarSap(ConfiguracaoSap configuracao)
    {
        // GATE 126X: o bloco de DESTINO (URLs efetivas + allowlist + sap-client) foi EXTRAIDO para
        // ValidarDestinoSapQ, sem alteracao de comportamento, para que outros consumidores possam exigir
        // exatamente o MESMO contrato de destino sem duplicar o validador de inicializacao.
        ResultadoValidacaoAmbienteQ destino = ValidarDestinoSapQ(configuracao);
        if (!destino.Valido)
        {
            return destino;
        }

        // GATES 112D / 113E / 122C — modos de escrita ADMITIDOS no startup do Q, e SOMENTE eles.
        // Gate SEMPRE proibido (nenhum modo o admite):
        //   escrita_habilitada (escrita SAP genérica).
        // GATE 122C: pallet_write_habilitado passou de "sempre proibido" para "admitido APENAS no
        //   perfil composto completo" (SAFE_PA_PIPELINE_WITH_PALLET) — ver ValidarModoEscritaAdmitido.
        // Modos seguros:
        //   SAFE_HU_ONLY_STARTUP (112D) ..... hu_write isolado. Autoriza exclusivamente POST /HandlingUnit
        //       (EscritaHuPermitida recusa qualquer outro método/endpoint); nada de 261/101/INT012/pipeline.
        //   SAFE_PA_PIPELINE_STARTUP (113E) . pa_pipeline + pa_material_document + hu_write, os TRÊS juntos,
        //       porque o pipeline é uma cadeia ESTRITA 261→101→HU: habilitar parte dela é mais perigoso que
        //       nada — pa_material_document sozinho liberaria os gateways 261/101 fora da orquestração, e
        //       pipeline sem hu_write postaria 261+101 para travar na terceira etapa.
        // Qualquer combinação diferente ⇒ BLOCK.
        string[] gatesProibidosLigados = ObterGatesPerigososLigados(configuracao);
        if (gatesProibidosLigados.Length > 0)
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado(
                "Ambiente Q bloqueado: write gates devem iniciar false — ligado(s): "
                + string.Join(", ", gatesProibidosLigados)
                + ". Apenas hu_write_habilitado isolado (HU-only), o trio "
                + "pa_pipeline_habilitado + pa_material_document_write_habilitado + hu_write_habilitado "
                + "(pipeline 261→101→HU) ou esse trio + packaging_habilitado + pallet_write_habilitado "
                + $"({ModoSeguroPipelineComPalete}) podem iniciar true.");
        }

        return ValidarModoEscritaAdmitido(configuracao);
    }

    /// <summary>
    /// GATE 126X: contrato de DESTINO SAP do ambiente Q, extraido de <see cref="ValidarSap"/> sem mudanca de
    /// comportamento. Exige, cumulativamente:
    /// <list type="number">
    /// <item>toda URL SAP EFETIVA em https, host <see cref="SapHostEsperado"/> e porta <see cref="SapPortaEsperada"/>;</item>
    /// <item>allowlist contendo o host Q e NAO contendo o host DS;</item>
    /// <item>sap-client presente com exatamente 3 digitos.</item>
    /// </list>
    /// Comparacao de host e por IGUALDADE do host da URI (nunca substring/Contains), o que separa
    /// materialmente o destino Q do destino DS, ainda que ambos usem o mandante 110.
    /// </summary>
    public static ResultadoValidacaoAmbienteQ ValidarDestinoSapQ(ConfiguracaoSap configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // GATE 126AA (R1): e obrigatorio existir ao menos UMA URL SAP efetiva. Sem destino real nao ha o
        // que validar, e aprovar nesse estado era PASS INDEVIDO: perfil, allowlist e sap-client NAO
        // substituem uma URL. Zero URLs ⇒ fail-closed.
        string[] urlsEfetivas = [.. ObterUrlsSap(configuracao)];
        if (urlsEfetivas.Length == 0)
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado(
                "Ambiente Q bloqueado: nenhuma URL SAP efetiva configurada. Allowlist, perfil e sap-client "
                + "nao substituem um destino SAP real.");
        }

        // GATE 126AA (R4): UMA unica URL efetiva incompativel bloqueia TODA a validacao. Q + DS nunca e
        // ambiente valido. Comparacao por igualdade exata de host e porta em ValidarUriSap.
        foreach (string url in urlsEfetivas)
        {
            if (!ValidarUriSap(url, out string mensagem))
            {
                return ResultadoValidacaoAmbienteQ.Bloqueado(mensagem);
            }
        }

        bool allowlistQ = configuracao.HostsPermitidos.Any(host =>
            string.Equals(host, SapHostEsperado, StringComparison.OrdinalIgnoreCase));
        bool allowlistDs = configuracao.HostsPermitidos.Any(host =>
            string.Equals(host, SapHostDesenvolvimento, StringComparison.OrdinalIgnoreCase));

        if (!allowlistQ || allowlistDs)
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: allowlist SAP deve conter somente o host Q autorizado.");
        }

        string sapClient = configuracao.SapClient.Trim();
        if (sapClient.Length != 3 || !sapClient.All(char.IsDigit))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: sap-client Q dedicado ausente ou invalido.");
        }

        return ResultadoValidacaoAmbienteQ.Ok();
    }

    /// <summary>
    /// GATE 126X: host SAP de DESENVOLVIMENTO/DS, nomeado para que a exclusao deixe de ser literal solto.
    /// Destino distinto do Q mesmo compartilhando o mandante 110.
    /// </summary>
    public const string SapHostDesenvolvimento = "vhfufds4ci.sap.fugacouros.com.br";

    /// <summary>
    /// GATE 126G: destino SAP INEQUIVOCO, sem nomear ambiente. Prova que a instalacao aponta para UM
    /// unico sistema SAP declarado por ela mesma — o que a <see cref="ValidarDestinoSapQ"/> provava
    /// apenas para o Q, por host cravado.
    /// <para>
    /// POR QUE EXISTE: a chave da tara fisica do palete e (mandante, centro, material de embalagem), e
    /// o mandante 110 e usado TANTO no Q QUANTO no DS. Sem um discriminante, a mesma chave valeria em
    /// dois sistemas diferentes. A versao anterior resolvia isso exigindo o host do Q, o que tornava a
    /// tara inalcancavel em qualquer outra instalacao, inclusive no DEV110.
    /// </para>
    /// <para>
    /// A GARANTIA QUE PERMANECE, e e a que importa: ambiente MISTURADO ou mal declarado continua
    /// bloqueado. Exige, cumulativamente:
    /// <list type="number">
    /// <item>ao menos UMA URL SAP efetiva — allowlist e perfil nao substituem destino real;</item>
    /// <item>TODAS as URLs efetivas em https, no MESMO host e na MESMA porta (igualdade exata, nunca
    ///   substring): Q + DS juntos nunca e ambiente valido;</item>
    /// <item>allowlist com EXATAMENTE esse host e mais nenhum — allowlist larga reabriria a ambiguidade
    ///   que este contrato existe para fechar;</item>
    /// <item>sap-client presente com exatamente 3 digitos.</item>
    /// </list>
    /// </para>
    /// <para>
    /// O QUE MUDA EM RELACAO AO CONTRATO ANTERIOR, dito sem rodeio: a identidade deixa de significar
    /// "este runtime esta no QAS110" e passa a significar "este runtime aponta, sem ambiguidade, para o
    /// sistema que ele mesmo declarou". A instalacao passa a responder por qual sistema e o seu — o que
    /// ja era verdade para o banco, a allowlist e as credenciais.
    /// </para>
    /// </summary>
    public static ResultadoValidacaoAmbienteQ ValidarDestinoSapDeclarado(ConfiguracaoSap configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        string[] urlsEfetivas = [.. ObterUrlsSap(configuracao)];
        if (urlsEfetivas.Length == 0)
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado(
                "Destino SAP bloqueado: nenhuma URL SAP efetiva configurada. Allowlist, perfil e "
                + "sap-client nao substituem um destino SAP real.");
        }

        string? host = null;
        int porta = 0;
        foreach (string url in urlsEfetivas)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoValidacaoAmbienteQ.Bloqueado(
                    "Destino SAP bloqueado: ha URL SAP efetiva que nao e https absoluta.");
            }

            if (host is null)
            {
                host = uri.Host;
                porta = uri.Port;
                continue;
            }

            if (!string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase) || uri.Port != porta)
            {
                return ResultadoValidacaoAmbienteQ.Bloqueado(
                    "Destino SAP bloqueado: as URLs SAP efetivas apontam para sistemas DIFERENTES. "
                    + "Um ambiente misturado nunca e destino valido.");
            }
        }

        bool allowlistExata = configuracao.HostsPermitidos.Count == 1
            && string.Equals(configuracao.HostsPermitidos[0], host, StringComparison.OrdinalIgnoreCase);

        if (!allowlistExata)
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado(
                "Destino SAP bloqueado: a allowlist SAP deve conter exatamente o host do destino "
                + "efetivo, e mais nenhum.");
        }

        string sapClient = configuracao.SapClient.Trim();
        if (sapClient.Length != 3 || !sapClient.All(char.IsDigit))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado(
                "Destino SAP bloqueado: sap-client ausente ou invalido.");
        }

        return ResultadoValidacaoAmbienteQ.Ok();
    }

    /// <summary>
    /// GATE 126G: host e porta do destino SAP efetivo, para a identidade carregar o que foi COMPROVADO
    /// em vez de constantes. Chamar somente apos <see cref="ValidarDestinoSapDeclarado"/> aprovar.
    /// </summary>
    internal static (string Host, int Porta) ObterDestinoEfetivo(ConfiguracaoSap configuracao)
    {
        foreach (string url in ObterUrlsSap(configuracao))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return (uri.Host, uri.Port);
            }
        }

        return (string.Empty, 0);
    }

    public static ResultadoValidacaoAmbienteQ ValidarBanco(ConfiguracaoBancoPostgreSql configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao.Servidor)
            || string.Equals(configuracao.Servidor, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuracao.Servidor, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: servidor PostgreSQL Q ausente ou local nao autorizado.");
        }

        if (string.IsNullOrWhiteSpace(configuracao.NomeBanco))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: database Q esperado nao configurado.");
        }

        if (string.IsNullOrWhiteSpace(configuracao.Schema))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: schema Q esperado nao configurado.");
        }

        if (!string.Equals(configuracao.NomeBanco, DatabaseEsperado, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configuracao.Schema, SchemaEsperado, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configuracao.Usuario, RoleAplicacaoEsperada, StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: database, schema ou role nao correspondem ao contrato Q.");
        }

        return ResultadoValidacaoAmbienteQ.Ok();
    }

    public static ResultadoValidacaoAmbienteQ ValidarIdentidadeBanco(
        ConfiguracaoBancoPostgreSql configuracao,
        string databaseReal,
        string schemaReal)
    {
        ResultadoValidacaoAmbienteQ contrato = ValidarBanco(configuracao);
        if (!contrato.Valido)
        {
            return contrato;
        }

        if (!string.Equals(configuracao.NomeBanco, databaseReal, StringComparison.Ordinal))
        {
            return ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: database fisico diferente do database Q configurado.");
        }

        return string.Equals(configuracao.Schema, schemaReal, StringComparison.Ordinal)
            ? ResultadoValidacaoAmbienteQ.Ok()
            : ResultadoValidacaoAmbienteQ.Bloqueado("Ambiente Q bloqueado: schema fisico diferente do schema Q configurado.");
    }

    public static Task<ResultadoProbeBancoQ> CriarProbeBancoAsync(
        Func<CancellationToken, Task<ResultadoProbeBancoQ>> executarProbeReadOnly,
        CancellationToken cancellationToken = default)
        => executarProbeReadOnly(cancellationToken);

    /// <summary>
    /// GATES 112D/113E/122C: gates de escrita que NUNCA podem iniciar true em NENHUM modo.
    /// <para>
    /// Resta APENAS <c>escrita_habilitada</c> (escrita SAP genérica): nenhum modo seguro a admite.
    /// </para>
    /// <para>
    /// GATE 122C: <c>pallet_write_habilitado</c> SAIU desta lista. Ele não é mais proibido em termos
    /// absolutos — passou a ser validado como COMBINAÇÃO por
    /// <see cref="ValidarModoEscritaAdmitido"/>, exatamente como já ocorria com
    /// <c>hu_write_habilitado</c>, <c>pa_pipeline_habilitado</c> e
    /// <c>pa_material_document_write_habilitado</c>. Isolado ou em combinação parcial ele continua
    /// BLOQUEANDO o startup; só o perfil composto completo (SAFE_PA_PIPELINE_WITH_PALLET) o admite.
    /// </para>
    /// </summary>
    internal static string[] ObterGatesPerigososLigados(ConfiguracaoSap configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        List<string> ligados = [];
        if (configuracao.EscritaHabilitada)
        {
            ligados.Add("escrita_habilitada (escrita SAP genérica)");
        }

        return [.. ligados];
    }

    /// <summary>
    /// GATE 113E: valida a COMBINAÇÃO dos gates que participam dos modos seguros. Pressupõe que os gates
    /// sempre proibidos já foram reprovados. Só dois modos passam:
    /// SAFE_HU_ONLY_STARTUP (hu isolado, ou nenhum gate) e
    /// SAFE_PA_PIPELINE_STARTUP (pa_pipeline + pa_material_document + hu_write, os três juntos).
    /// Combinação parcial é recusada NOMEANDO o que falta — habilitar meia cadeia 261→101→HU é pior que
    /// não habilitar.
    /// </summary>
    /// <summary>Nome canonico do modo seguro que inclui o write de palete (GATE 122B/122C).</summary>
    public const string ModoSeguroPipelineComPalete = "SAFE_PA_PIPELINE_WITH_PALLET";

    /// <summary>Nome canonico do modo seguro do pipeline sem palete.</summary>
    public const string ModoSeguroPipeline = "SAFE_PA_PIPELINE";

    internal static ResultadoValidacaoAmbienteQ ValidarModoEscritaAdmitido(ConfiguracaoSap configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        bool pipeline = configuracao.ProdutoAcabadoPipelineHabilitado;
        bool materialDocumentPa = configuracao.ProdutoAcabadoMaterialDocumentWriteHabilitado;
        bool hu = configuracao.HuWriteHabilitado;
        bool packaging = configuracao.PackagingHabilitado;
        bool palete = configuracao.PalletWriteHabilitado;

        // GATE 122C — Modo 3: SAFE_PA_PIPELINE_WITH_PALLET.
        // O palete só é admitido no perfil composto EXATO: o trio do pipeline + packaging, TODOS true.
        // Fora disso, pallet=true BLOQUEIA — inclusive isolado e em qualquer combinação parcial.
        // Motivo: o palete agrupa HUs que só existem se 261→101→HU tiver rodado inteiro, e a norma de
        // embalagem (packaging) é o que fornece o material de embalagem real do palete.
        if (palete)
        {
            if (pipeline && materialDocumentPa && hu && packaging)
            {
                return ResultadoValidacaoAmbienteQ.Ok();
            }

            List<string> faltantesPalete = [];
            if (!pipeline) { faltantesPalete.Add("pa_pipeline_habilitado"); }
            if (!materialDocumentPa) { faltantesPalete.Add("pa_material_document_write_habilitado"); }
            if (!hu) { faltantesPalete.Add("hu_write_habilitado"); }
            if (!packaging) { faltantesPalete.Add("packaging_habilitado"); }

            return ResultadoValidacaoAmbienteQ.Bloqueado(
                $"Ambiente Q bloqueado: pallet_write_habilitado só inicia true no modo "
                + $"{ModoSeguroPipelineComPalete} — exige pa_pipeline_habilitado + "
                + "pa_material_document_write_habilitado + hu_write_habilitado + packaging_habilitado "
                + "juntos; faltando: " + string.Join(", ", faltantesPalete)
                + ". Habilitar o palete sem a cadeia completa é mais perigoso que não habilitar.");
        }

        // Modo 1 (112D): nenhum gate, ou hu_write isolado.
        if (!pipeline && !materialDocumentPa)
        {
            return ResultadoValidacaoAmbienteQ.Ok();
        }

        // Modo 2 (113E): o trio COMPLETO do pipeline. Comportamento PRESERVADO sem alteração —
        // packaging continua NÃO sendo exigido aqui (só o é quando o palete entra).
        if (pipeline && materialDocumentPa && hu)
        {
            return ResultadoValidacaoAmbienteQ.Ok();
        }

        List<string> faltantes = [];
        if (!pipeline)
        {
            faltantes.Add("pa_pipeline_habilitado");
        }

        if (!materialDocumentPa)
        {
            faltantes.Add("pa_material_document_write_habilitado");
        }

        if (!hu)
        {
            faltantes.Add("hu_write_habilitado");
        }

        return ResultadoValidacaoAmbienteQ.Bloqueado(
            "Ambiente Q bloqueado: combinacao de write gates nao admitida — o pipeline 261→101→HU exige "
            + "pa_pipeline_habilitado + pa_material_document_write_habilitado + hu_write_habilitado juntos; "
            + "faltando: " + string.Join(", ", faltantes)
            + ". Alternativa admitida: hu_write_habilitado isolado (HU-only).");
    }

    /// <summary>
    /// GATE 122C: nome do modo de escrita efetivamente admitido, para diagnóstico. Devolve null quando
    /// a combinação NÃO é admitida — nunca inventa um modo para uma combinação bloqueada.
    /// </summary>
    internal static string? ObterModoEscritaAdmitido(ConfiguracaoSap configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (ObterGatesPerigososLigados(configuracao).Length > 0
            || !ValidarModoEscritaAdmitido(configuracao).Valido)
        {
            return null;
        }

        return configuracao.PalletWriteHabilitado
            ? ModoSeguroPipelineComPalete
            : configuracao.ProdutoAcabadoPipelineHabilitado
                ? ModoSeguroPipeline
                : "SAFE_HU_ONLY_STARTUP";
    }

    /// <summary>
    /// GATE 126AA: TODAS as URLs do SAP on-premise que o runtime pode usar, incluindo as formas EFETIVAS
    /// (derivadas). Uma URL OPCIONAL AUSENTE e simplesmente ignorada; uma URL PRESENTE invalida bloqueia.
    /// <para>
    /// Incluidas no 126AA, por estarem configuraveis e serem usadas pelo runtime: PurchaseOrderBaseUrl e
    /// sua forma efetiva (defeito do 126Y — cenario BaseUrl=Q com PurchaseOrder=DS passava),
    /// ProductionVersionBaseUrlEfetiva (roteiro autoritativo, GATE 048-E REV2) e WorkCenterBaseUrlEfetiva
    /// (Plant + WorkCenter do Controle de Apontamentos).
    /// </para>
    /// <para>
    /// NAO incluidas, de proposito: PackagingBaseUrl e PalletInt012BaseUrl. Ambas apontam para o tenant do
    /// Integration Suite/CPI, que NAO e o host SAP on-premise — valida-las contra o host Q seria errado.
    /// A rota INT012 tem contrato proprio (allowlist dedicada 046-K, verificada em
    /// ValidadorIdentidadeQas110).
    /// </para>
    /// </summary>
    private static IEnumerable<string> ObterUrlsSap(ConfiguracaoSap configuracao)
    {
        string[] urls =
        [
            configuracao.BaseUrl,
            configuracao.PurchaseOrderBaseUrl,
            configuracao.PurchaseOrderBaseUrlEfetiva,
            configuracao.MaterialDocumentBaseUrl,
            configuracao.ProductionOrderBaseUrl,
            configuracao.ProductionOrderBaseUrlEfetiva,
            configuracao.ProductionOrderConfirmationBaseUrl,
            configuracao.ProductionOrderConfirmationBaseUrlEfetiva,
            configuracao.ProductBaseUrl,
            configuracao.ProductMasterBaseUrlEfetiva,
            configuracao.HandlingUnitBaseUrl,
            configuracao.ProductionVersionBaseUrlEfetiva,
            configuracao.ProductionRoutingBaseUrlEfetiva,
            configuracao.WorkCenterBaseUrlEfetiva
        ];

        return urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ValidarUriSap(string url, out string mensagem)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, SapHostEsperado, StringComparison.OrdinalIgnoreCase)
            || uri.Port != SapPortaEsperada)
        {
            mensagem = "Ambiente Q bloqueado: host/porta SAP efetivos nao correspondem ao Q autorizado.";
            return false;
        }

        mensagem = string.Empty;
        return true;
    }
}

public sealed record ResultadoValidacaoAmbienteQ(bool Valido, string Mensagem)
{
    public static ResultadoValidacaoAmbienteQ Ok() => new(true, string.Empty);
    public static ResultadoValidacaoAmbienteQ Bloqueado(string mensagem) => new(false, mensagem);
}

public sealed record ResultadoProbeBancoQ(string Database, string Schema);

