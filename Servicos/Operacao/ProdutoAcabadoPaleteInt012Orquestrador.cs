using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Resultado do pipeline de palete (uma passagem). <see cref="Executou"/>=true â‡’ houve exatamente UM POST_FORMACAO.</summary>
public sealed record ResultadoPalete045(bool Executou, EstadoPaleteInt012 Estado, string? UcGerada, string Mensagem)
{
    public static ResultadoPalete045 Bloqueado(string mensagem) => new(false, EstadoPaleteInt012.NaoEnviado, null, mensagem);
}

/// <summary>
/// Orquestrador PRODUTIVO do palete sobre o contrato 045 + INT012. Fluxo: valida elegibilidade (prÃ©-POST) â†’
/// <c>palete_criar</c> (retorna codigo_hu_palete) â†’ <c>vincular_caixa</c> (persiste composiÃ§Ã£o) â†’
/// <c>claim_envio</c> (capability sÃ³ do retorno) â†’ UM POST_FORMACAO
/// (gateway INT012 existente) â†’ <c>registrar_sucesso|erro|timeout</c>. A criaÃ§Ã£o local do candidato NÃƒO dispara
/// POST. Sem claim â‡’ zero POST. Timeout â‡’ registrar_timeout, zero retry cego. Fail-closed: gateway nÃ£o autorizado
/// (gate/URL/allowlist CPI) ou PackagingMaterial ausente => nenhum POST, nenhum registro.
/// guard/consistÃªncia de estado Ã© do banco (funÃ§Ãµes SECURITY DEFINER). NÃ£o faz DML nem lÃª token.
/// </summary>
public sealed class ProdutoAcabadoPaleteInt012Orquestrador
{
    private const string EndpointPalete = FabricaProdutoAcabadoPaleteInt012Gateway.EndpointPath;

    private readonly IProdutoAcabadoPipeline045Operacoes _ops;
    private readonly IProdutoAcabadoPaleteInt012Gateway _gateway;

    public ProdutoAcabadoPaleteInt012Orquestrador(IProdutoAcabadoPipeline045Operacoes ops, IProdutoAcabadoPaleteInt012Gateway gateway)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<ResultadoPalete045> ExecutarAsync(
        IReadOnlyList<long> codigosCaixas,
        ProdutoAcabadoPaleteRequest requisicao,
        long usuario,
        string terminal,
        long? codigoPaleteExistente = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigosCaixas);
        ArgumentNullException.ThrowIfNull(requisicao);

        // §10: fail-closed ANTES de qualquer claim/POST. Gateway não autorizado (gate/URL/allowlist CPI) => bloqueia.
        if (!_gateway.EnvioAutorizado)
        {
            return ResultadoPalete045.Bloqueado("Envio de palete (INT012) não autorizado (gate/URL/allowlist CPI). Nenhum POST.");
        }
        // §10: PackagingMaterial real obrigatório. Ausente/vazio/branco => zero claim/POST.
        if (string.IsNullOrWhiteSpace(requisicao.PackagingMaterial))
        {
            return ResultadoPalete045.Bloqueado("PackagingMaterial ausente/vazio: palete não enviado.");
        }
        // Â§4: cada caixa precisa de HU SAP individual vÃ¡lida (HandlingUnit filho). Sem HU â‡’ zero POST.
        if (requisicao.HandlingUnitItems.Count == 0
            || requisicao.HandlingUnitItems.Any(i => string.IsNullOrWhiteSpace(i.HandlingUnit)))
        {
            return ResultadoPalete045.Bloqueado("Palete com caixa sem HU SAP individual: bloqueado (nenhum POST).");
        }

        // GATE 046-AQ-C1: o palete LOCAL ja foi criado e vinculado pela UI (persistencia 046-E). O ENVIO reutiliza
        // a identidade persistida (codigo_hu_palete) e NAO recria/revincula — isso violava constraint do banco e
        // lancava excecao ANTES do claim. Sem identidade persistida, mantem o caminho legado de criar+vincular.
        long codigoPalete;
        if (codigoPaleteExistente is long paleteExistente && paleteExistente > 0)
        {
            codigoPalete = paleteExistente;
        }
        else
        {
            long? codigoPaleteCriado = await _ops.CriarPaleteAsync(
                requisicao.PackagingMaterial,
                requisicao.Plant,
                requisicao.StorageLocation,
                requisicao.GrossWeight,
                requisicao.NetWeight,
                requisicao.TareWeight,
                usuario,
                terminal,
                cancellationToken);
            if (codigoPaleteCriado is not long codigoCriado || codigoCriado <= 0)
            {
                return ResultadoPalete045.Bloqueado("Criacao do palete negada pelo banco. Nenhuma vinculacao ou POST executado.");
            }

            codigoPalete = codigoCriado;
            int sequencia = 1;
            foreach (long codigoCaixa in codigosCaixas)
            {
                bool vinculado = await _ops.VincularCaixaPaleteAsync(codigoPalete, codigoCaixa, sequencia++, usuario, terminal, cancellationToken);
                if (!vinculado)
                {
                    return ResultadoPalete045.Bloqueado("Vinculo da caixa ao palete negado pelo banco. Nenhum claim ou POST executado.");
                }
            }
        }

        // Claim de envio. Capability SOMENTE do retorno. Sem claim => ZERO POST.

        string requestJson = System.Text.Json.JsonSerializer.Serialize(requisicao);
        ResultadoClaim045 claim = await _ops.ClaimEnvioPaleteAsync(codigoPalete, requestJson, EndpointPalete, usuario, terminal, cancellationToken);
        if (!claim.Obtido)
        {
            return ResultadoPalete045.Bloqueado("Claim de envio do palete não obtido (banco negou). Nenhum POST executado.");
        }
        if (claim.Token is not Guid claimToken || claim.Tentativa is not int claimTentativa || claimTentativa <= 0)
        {
            return ResultadoPalete045.Bloqueado("Claim de envio do palete retornou sem tentativa/token válidos. Nenhum POST executado.");
        }        // GATE 047-I-B: marca local do instante do claim, para medir quanto do lease nominal (300s) decorreu ate o
        // fechamento. Nao substitui claim_expira_em do banco; e um sinal client-side para o diagnostico do proximo caso.
        DateTimeOffset claimObtidoUtc = DateTimeOffset.UtcNow;

        // UM POST_FORMACAO (o gateway aplica o critÃ©rio de sucesso 2xx+Status S+UC+sem Tipo E).
        ResultadoPaleteInt012 r = await _gateway.EnviarPaleteAsync(requisicao, cancellationToken);

        switch (r.Estado)
        {
            case EstadoPaleteInt012.Confirmado:
                string sucessoJson = $$"""{"UC_gerada":"{{r.UcGerada}}","Status":"S"}""";
                DateTimeOffset fechamentoInicioUtc = DateTimeOffset.UtcNow;
                bool fechouLocal;
                string resultadoFechamento;
                string? sqlState = null;
                string? excecaoTipo = null;
                string? mensagemFalha = null;
                try
                {
                    fechouLocal = await _ops.RegistrarSucessoPaleteAsync(codigoPalete, claimTentativa, claimToken, r.HttpStatus ?? 0, r.UcGerada ?? string.Empty,
                        sucessoJson, EndpointPalete, usuario, terminal, cancellationToken);
                    resultadoFechamento = fechouLocal ? "TRUE" : "FALSE";
                }
                catch (Exception ex)
                {
                    // POST ja confirmou no SAP (UC recebida). Excecao no fechamento local => NAO mascarar como sucesso.
                    // GATE 047-I-B: a evidencia (tipo/SQLSTATE/mensagem) NAO pode ser descartada — e a unica prova que
                    // torna a causa raiz comprovavel no proximo caso. Capturada e registrada de forma sanitizada.
                    fechouLocal = false;
                    resultadoFechamento = "EXCEPTION";
                    sqlState = ExtrairSqlState(ex);
                    excecaoTipo = ex.GetType().Name;
                    mensagemFalha = Sanitizar(ex.Message);
                }

                RegistrarDiagnosticoFechamento(
                    codigoPalete, claimObtidoUtc, fechamentoInicioUtc, claimTentativa, claimToken,
                    r.HttpStatus, r.UcGerada, sucessoJson, resultadoFechamento, sqlState, excecaoTipo, mensagemFalha);

                if (fechouLocal)
                {
                    return new ResultadoPalete045(true, r.Estado, r.UcGerada, "Palete confirmado no SAP (POST_FORMACAO).");
                }

                // GATE 046-AQ-Z2-C: POST confirmou no SAP porem o fechamento local NAO foi comprovado
                // (registrar_sucesso=false ou excecao). NAO classificar como sucesso integral: INDETERMINADO,
                // reconciliacao necessaria, preservando a UC recebida para recuperacao segura. Zero retry cego.
                return new ResultadoPalete045(true, EstadoPaleteInt012.IndeterminadoTimeout, r.UcGerada,
                    $"POST confirmado no SAP (UC {r.UcGerada}) porem fechamento local NAO comprovado. Reconciliacao necessaria; sem reenvio.");

            case EstadoPaleteInt012.IndeterminadoTimeout:
                await _ops.RegistrarTimeoutPaleteAsync(codigoPalete, claimTentativa, claimToken, r.MensagemSanitizada, EndpointPalete, usuario, terminal, cancellationToken);
                return new ResultadoPalete045(true, r.Estado, null, r.MensagemSanitizada);

            case EstadoPaleteInt012.NaoEnviado:
                // Defesa: prÃ©-checagens jÃ¡ cobrem isto; se ocorrer, nÃ£o houve POST â‡’ nÃ£o registra.
                return ResultadoPalete045.Bloqueado(r.MensagemSanitizada);

            default: // ErroStatus / ErroHttp / ContratoStatusPendente â‡’ erro funcional/HTTP (sem retry).
                await _ops.RegistrarErroPaleteAsync(codigoPalete, claimTentativa, claimToken, r.HttpStatus ?? 0, "{}", r.MensagemSanitizada, EndpointPalete, usuario, terminal, cancellationToken);
                return new ResultadoPalete045(true, r.Estado, r.UcGerada, r.MensagemSanitizada);
        }
    }

    // ---------------- GATE 047-I-B: observabilidade sanitizada do fechamento local ----------------

    private static readonly object DiagSync = new();

    /// <summary>
    /// Registra, de forma SANITIZADA, o resultado do fechamento local (registrar_sucesso) apos POST confirmado.
    /// Torna a causa raiz comprovavel no proximo caso: claim/lease, HTTP/UC/response presentes, resultado
    /// TRUE/FALSE/EXCEPTION, SQLSTATE, tipo e mensagem. NUNCA registra senha/Authorization/Basic/token/payload integral.
    /// </summary>
    private static void RegistrarDiagnosticoFechamento(
        long codigoPalete, DateTimeOffset claimObtidoUtc, DateTimeOffset inicioUtc, int tentativa, Guid claimToken,
        int? httpStatus, string? huPai, string? responseJson, string resultadoRegistrarSucesso,
        string? sqlState, string? excecaoTipo, string? mensagemSanitizada)
    {
        try
        {
            DateTimeOffset fimUtc = DateTimeOffset.UtcNow;
            long decorridoDesdeClaimMs = (long)(inicioUtc - claimObtidoUtc).TotalMilliseconds;
            const long leaseNominalMs = 300_000; // lease nominal do claim de palete (300s)
            long leaseRestanteEstimadoMs = leaseNominalMs - decorridoDesdeClaimMs;
            bool responsePresente = !string.IsNullOrWhiteSpace(responseJson);
            bool responseJsonObject = responsePresente && responseJson!.TrimStart().StartsWith('{');

            string linha = string.Join('|',
                $"FECHAMENTO_LOCAL_INICIO_UTC={inicioUtc:O}",
                $"CODIGO_PALETE={codigoPalete}",
                $"TENTATIVA_REAL={tentativa.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"CLAIM_MATCH_CONTEXT={claimToken}",
                $"CLAIM_OBTIDO_EM={claimObtidoUtc:O}",
                $"LEASE_NOMINAL_MS={leaseNominalMs}",
                $"DECORRIDO_DESDE_CLAIM_MS={decorridoDesdeClaimMs}",
                $"LEASE_RESTANTE_MS_ESTIMADO={leaseRestanteEstimadoMs}",
                $"HTTP_PRESENTE={httpStatus.HasValue}",
                $"HTTP_STATUS={httpStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}",
                $"HU_PAI_PRESENTE={!string.IsNullOrWhiteSpace(huPai)}",
                $"RESPONSE_PRESENTE={responsePresente}",
                $"RESPONSE_JSON_OBJECT={responseJsonObject}",
                $"REGISTRAR_SUCESSO_RESULTADO={resultadoRegistrarSucesso}",
                $"SQLSTATE={sqlState ?? "-"}",
                $"EXCEPTION_TYPE={excecaoTipo ?? "-"}",
                $"MENSAGEM_SANITIZADA={(string.IsNullOrWhiteSpace(mensagemSanitizada) ? "-" : mensagemSanitizada)}",
                $"FECHAMENTO_LOCAL_FIM_UTC={fimUtc:O}") + Environment.NewLine;

            lock (DiagSync)
            {
                string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "pa045_fechamento_palete.log"), linha, System.Text.Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[PA045_FECHAMENTO_DIAG] Falha ao gravar diagnostico: {ex.GetType().Name}");
        }
    }

    /// <summary>Le SQLSTATE de forma defensiva, sem dependencia dura de Npgsql (PostgresException expoe SqlState).</summary>
    private static string? ExtrairSqlState(Exception ex)
    {
        for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual.GetType().GetProperty("SqlState")?.GetValue(atual) is string estado && !string.IsNullOrWhiteSpace(estado))
            {
                return estado;
            }
            if (atual.Data.Contains("SqlState") && atual.Data["SqlState"] is string dado && !string.IsNullOrWhiteSpace(dado))
            {
                return dado;
            }
        }
        return null;
    }

    /// <summary>Sanitiza a mensagem de falha: remove quebras de linha, trunca e mascara qualquer segredo conhecido.</summary>
    private static string Sanitizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return string.Empty; }
        string s = texto.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        foreach (string segredo in new[] { "authorization", "password", "senha", "cookie", "token", "csrf", "basic " })
        {
            int idx = s.IndexOf(segredo, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) { s = s[..idx] + "***"; break; }
        }
        return s.Length <= 400 ? s : s[..400];
    }
}




