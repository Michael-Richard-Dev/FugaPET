using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class SemiAcabadoServico
{
    internal const string MensagemInconsistenciaLegado = "O lançamento local antigo possui dados incompatíveis com a versão atual. Ele não pode ser reenviado nem alterado. Cancele-o de forma controlada para iniciar um novo lançamento.";

    private readonly ISemiAcabadoRepositorio _repositorio;
    private readonly SemiAcabadoMaterialDocument101PayloadBuilder _payloadBuilder;
    private readonly Func<IMaterialDocumentSapServico> _criarMaterialDocumentSapServico;

    public SemiAcabadoServico()
        : this(
            new SemiAcabadoRepositorio(new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())),
            new SemiAcabadoMaterialDocument101PayloadBuilder(),
            FabricaMaterialDocumentSapServico.Criar)
    {
    }

    public SemiAcabadoServico(
        ISemiAcabadoRepositorio repositorio,
        SemiAcabadoMaterialDocument101PayloadBuilder payloadBuilder,
        Func<IMaterialDocumentSapServico> criarMaterialDocumentSapServico)
    {
        _repositorio = repositorio;
        _payloadBuilder = payloadBuilder;
        _criarMaterialDocumentSapServico = criarMaterialDocumentSapServico;
    }

    public async Task<ResultadoEnvioSemiAcabadoSap> SalvarEEnviarSap101Async(
        LancamentoSemiAcabado lancamento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        if (!await _repositorio.EstruturaDisponivelAsync(cancellationToken))
        {
            return ResultadoEnvioSemiAcabadoSap.EstruturaNaoAplicada();
        }

        long codigoLancamento;
        try
        {
            if (lancamento.CodigoSemiAcabadoLancamento is long codigoPersistido)
            {
                codigoLancamento = codigoPersistido;
            }
            else
            {
                ResultadoPreviewSemiAcabado101 previewTela = _payloadBuilder.MontarPreview101(lancamento, DateTime.UtcNow);
                if (!previewTela.Sucesso)
                {
                    return ResultadoEnvioSemiAcabadoSap.Falha(previewTela.Mensagem);
                }

                codigoLancamento = await _repositorio.SalvarLancamentoLocalAsync(lancamento, previewTela.PayloadJson, cancellationToken);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return ResultadoEnvioSemiAcabadoSap.EstruturaNaoAplicada();
        }

        LancamentoSemiAcabado? lancamentoPersistido;
        try
        {
            lancamentoPersistido = await ObterLancamentoCompletoValidadoAsync(codigoLancamento, cancellationToken);
        }
        catch (InconsistenciaLancamentoSemiAcabadoException)
        {
            return ResultadoEnvioSemiAcabadoSap.FalhaInconsistenciaLocal(MensagemInconsistenciaLegado, codigoLancamento);
        }
        if (lancamentoPersistido is null)
        {
            return ResultadoEnvioSemiAcabadoSap.Falha("LanÃ§amento persistido do semi-acabado nÃ£o encontrado.", codigoLancamento);
        }

        if (!ContextoCorresponde(lancamento, lancamentoPersistido))
        {
            return ResultadoEnvioSemiAcabadoSap.Falha("Reenvio bloqueado: lanÃ§amento persistido nÃ£o corresponde Ã  OP/item selecionado.", codigoLancamento);
        }

        ResultadoEnvioSemiAcabadoSap validacaoLegado = ValidarLancamentoPersistidoParaEnvio(lancamentoPersistido, codigoLancamento);
        if (validacaoLegado.InconsistenciaLocal)
        {
            return validacaoLegado;
        }

        if (lancamentoPersistido.StatusLancamento is not "FINALIZADO_LOCAL" and not "ERRO_SAP")
        {
            return ResultadoEnvioSemiAcabadoSap.BloqueadoDuplicidade(
                $"Envio SAP 101 bloqueado: lanÃ§amento estÃ¡ em status {lancamentoPersistido.StatusLancamento}.",
                codigoLancamento);
        }

        ResultadoMaterialDocumentSemiAcabadoRequest request = _payloadBuilder.MontarRequisicao101(lancamentoPersistido, DateTime.UtcNow);
        if (!request.Sucesso || request.Requisicao is null)
        {
            return ResultadoEnvioSemiAcabadoSap.Falha(request.Mensagem, codigoLancamento);
        }

        bool reservado = await _repositorio.TentarReservarEnvioSapAsync(codigoLancamento, cancellationToken);
        if (!reservado)
        {
            return ResultadoEnvioSemiAcabadoSap.BloqueadoDuplicidade(
                "Envio SAP 101 bloqueado: lanÃ§amento jÃ¡ estÃ¡ em envio, confirmado ou sem status elegÃ­vel.",
                codigoLancamento);
        }

        IMaterialDocumentSapServico sapServico = _criarMaterialDocumentSapServico();
        ResultadoMaterialDocumentSap resultadoSap = await sapServico.CriarDocumentoMaterial101Async(
            request.Requisicao,
            $"SEMI_ACABADO:{lancamentoPersistido.Ordem.NumeroOrdem}:{codigoLancamento}",
            cancellationToken);

        return await ClassificarResultadoSapAsync(codigoLancamento, resultadoSap, cancellationToken);
    }

    /// <summary>
    /// Classifica o resultado do cliente SAP em CONFIRMADO / DIVERGENCIA (indeterminado, bloqueado) / ERRO
    /// (falha segura, reenviÃ¡vel), alinhado Ã  semÃ¢ntica REAL de <see cref="MaterialDocumentSapApiClient"/>:
    /// 2xx sem documento devolve Sucesso=false + StatusHttp 2xx + Etapa PARSE_RESPOSTA; rede interrompida
    /// durante o POST devolve StatusHttp null + Etapa POST_DOCUMENTO_MATERIAL.
    /// </summary>
    private async Task<ResultadoEnvioSemiAcabadoSap> ClassificarResultadoSapAsync(
        long codigoLancamento,
        ResultadoMaterialDocumentSap resultadoSap,
        CancellationToken cancellationToken)
    {
        bool documentoAusente = string.IsNullOrWhiteSpace(resultadoSap.MaterialDocument)
            || string.IsNullOrWhiteSpace(resultadoSap.MaterialDocumentYear);

        bool respostaHttp2xx = resultadoSap.StatusHttp is >= 200 and <= 299;
        bool sucessoConfirmado = resultadoSap.Sucesso && respostaHttp2xx && !documentoAusente;

        // Sucesso real (Sucesso=true + 2xx + MaterialDocument/Year).
        if (sucessoConfirmado
            && resultadoSap.MaterialDocument is { } documento
            && resultadoSap.MaterialDocumentYear is { } exercicio)
        {
            await _repositorio.MarcarConfirmadoSapAsync(codigoLancamento, documento, exercicio, cancellationToken);
            return ResultadoEnvioSemiAcabadoSap.Confirmado(documento, exercicio, codigoLancamento);
        }

        // INDETERMINADO â†’ DIVERGENCIA_SAP (bloqueado, nÃ£o reenviÃ¡vel):
        //  (a) HTTP 2xx sem MaterialDocument/Year â€” o SAP pode ter criado o documento sem rastreabilidade;
        //  (b) falha indeterminada DURANTE o POST (sem status, timeout 408 ou HTTP 5xx).
        bool respostaIndeterminadaDurantePost = string.Equals(
                resultadoSap.Etapa,
                MaterialDocumentSapApiClient.EtapaPost,
                StringComparison.Ordinal)
            && (resultadoSap.StatusHttp is null
                || resultadoSap.StatusHttp == 408
                || resultadoSap.StatusHttp is >= 500 and <= 599);

        if ((respostaHttp2xx && documentoAusente)
            || (!resultadoSap.Sucesso && respostaHttp2xx)
            || (!resultadoSap.Sucesso && !documentoAusente)
            || respostaIndeterminadaDurantePost)
        {
            string mensagemDivergencia = respostaIndeterminadaDurantePost
                ? "O SAP ou a infraestrutura retornou uma falha indeterminada durante o POST. O documento pode ter sido criado. NÃ£o reenviar sem verificar no SAP."
                : "SAP respondeu sem rastreabilidade do documento. NÃ£o reenviar sem suporte.";
            await _repositorio.MarcarDivergenciaSapAsync(codigoLancamento, mensagemDivergencia, cancellationToken);
            return ResultadoEnvioSemiAcabadoSap.Divergencia(mensagemDivergencia, codigoLancamento);
        }

        // FALHA SEGURA â†’ ERRO_SAP (reenviÃ¡vel): validaÃ§Ã£o de URL/auth/montagem, falha no CSRF Fetch, falha de
        // rede ANTES do POST e rejeiÃ§Ãµes HTTP de negÃ³cio (nÃ£o-2xx), onde comprovadamente nÃ£o houve documento.
        string mensagem = string.IsNullOrWhiteSpace(resultadoSap.MensagemSanitizada)
            ? "SAP 101 nÃ£o confirmou documento de material do semi-acabado."
            : resultadoSap.MensagemSanitizada;
        await _repositorio.MarcarErroSapAsync(codigoLancamento, mensagem, cancellationToken);
        return ResultadoEnvioSemiAcabadoSap.Falha(mensagem, codigoLancamento);
    }

    private static bool ContextoCorresponde(LancamentoSemiAcabado esperado, LancamentoSemiAcabado persistido)
        => string.Equals(esperado.Ordem.NumeroOrdem, persistido.Ordem.NumeroOrdem, StringComparison.Ordinal)
            && string.Equals(esperado.Ordem.ItemOrdem ?? string.Empty, persistido.Ordem.ItemOrdem ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(esperado.Ordem.MaterialProduzido, persistido.Ordem.MaterialProduzido, StringComparison.Ordinal);

    public async Task<ResultadoEnvioSemiAcabadoSap> CancelarLancamentoLocalAsync(
        long codigoLancamento,
        string motivo,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        if (codigoLancamento <= 0)
        {
            return ResultadoEnvioSemiAcabadoSap.Falha("Lançamento local inválido para cancelamento.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoEnvioSemiAcabadoSap.Falha("Informe o motivo do cancelamento local.");
        }

        string usuarioSeguro = string.IsNullOrWhiteSpace(usuario) ? Environment.UserName : usuario.Trim();
        LancamentoSemiAcabado? lancamentoPersistido = null;
        bool inconsistenteLegado = false;

        try
        {
            lancamentoPersistido = await ObterLancamentoCompletoValidadoAsync(codigoLancamento, cancellationToken);
        }
        catch (InconsistenciaLancamentoSemiAcabadoException)
        {
            inconsistenteLegado = true;
        }

        if (lancamentoPersistido is not null)
        {
            if (!string.Equals(lancamentoPersistido.StatusLancamento, "ERRO_SAP", StringComparison.Ordinal))
            {
                return ResultadoEnvioSemiAcabadoSap.BloqueadoDuplicidade(
                    $"Cancelamento local bloqueado: lançamento está em status {lancamentoPersistido.StatusLancamento}.",
                    codigoLancamento);
            }

            if (!string.IsNullOrWhiteSpace(lancamentoPersistido.MaterialDocument)
                || !string.IsNullOrWhiteSpace(lancamentoPersistido.MaterialDocumentYear))
            {
                return ResultadoEnvioSemiAcabadoSap.Divergencia(
                    "Cancelamento local bloqueado: lançamento possui documento SAP informado. Verifique o SAP antes de qualquer ação.",
                    codigoLancamento);
            }
        }

        bool cancelado = await _repositorio.CancelarLancamentoLocalAsync(
            codigoLancamento,
            motivo.Trim(),
            usuarioSeguro,
            cancellationToken);

        if (!cancelado)
        {
            string complemento = inconsistenteLegado
                ? " O lançamento possui inconsistência local antiga; confirme se ele está em ERRO_SAP e sem documento SAP."
                : string.Empty;

            return ResultadoEnvioSemiAcabadoSap.BloqueadoDuplicidade(
                "Cancelamento local não executado: somente lançamentos ERRO_SAP sem documento SAP podem ser cancelados." + complemento,
                codigoLancamento);
        }

        return ResultadoEnvioSemiAcabadoSap.BloqueadoDuplicidade(
            "Lançamento local cancelado com sucesso. As pesagens foram preservadas para auditoria e um novo lançamento pode ser iniciado.",
            codigoLancamento);
    }

    private async Task<LancamentoSemiAcabado?> ObterLancamentoCompletoValidadoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repositorio.ObterLancamentoCompletoAsync(codigoLancamento, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Somatório", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Somatorio", StringComparison.OrdinalIgnoreCase))
        {
            throw new InconsistenciaLancamentoSemiAcabadoException(MensagemInconsistenciaLegado, ex);
        }
    }

    private static ResultadoEnvioSemiAcabadoSap ValidarLancamentoPersistidoParaEnvio(
        LancamentoSemiAcabado lancamento,
        long codigoLancamento)
    {
        if (string.IsNullOrWhiteSpace(lancamento.Ordem.NumeroOrdem)
            || string.IsNullOrWhiteSpace(lancamento.Ordem.MaterialProduzido)
            || string.IsNullOrWhiteSpace(lancamento.Ordem.Centro)
            || string.IsNullOrWhiteSpace(lancamento.Ordem.DepositoDestino)
            || string.IsNullOrWhiteSpace(lancamento.Ordem.Unidade))
        {
            return ResultadoEnvioSemiAcabadoSap.FalhaInconsistenciaLocal(MensagemInconsistenciaLegado, codigoLancamento);
        }

        if (!string.Equals(lancamento.Ordem.Unidade, "KG", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoEnvioSemiAcabadoSap.FalhaInconsistenciaLocal(MensagemInconsistenciaLegado, codigoLancamento);
        }

        IReadOnlyList<PesagemSemiAcabado> pesagensValidas = lancamento.Pesagens
            .Where(PesagemSemiAcabadoCalculos.PesagemValida)
            .ToList();

        if (pesagensValidas.Count == 0)
        {
            return ResultadoEnvioSemiAcabadoSap.FalhaInconsistenciaLocal(MensagemInconsistenciaLegado, codigoLancamento);
        }

        foreach (PesagemSemiAcabado pesagem in pesagensValidas)
        {
            if (string.IsNullOrWhiteSpace(pesagem.CodigoEtiqueta)
                || pesagem.PesoLiquidoKg <= 0m
                || pesagem.SaldoAposPesagemKg < 0m)
            {
                return ResultadoEnvioSemiAcabadoSap.FalhaInconsistenciaLocal(MensagemInconsistenciaLegado, codigoLancamento);
            }
        }

        return ResultadoEnvioSemiAcabadoSap.Falha(string.Empty, codigoLancamento);
    }

    /// <summary>Pesagens persistidas de um lanÃ§amento (histÃ³rico/reimpressÃ£o), sem recalcular cÃ³digo de etiqueta.</summary>
    public Task<IReadOnlyList<PesagemSemiAcabado>> ListarPesagensPorLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _repositorio.ListarPesagensPorLancamentoAsync(codigoLancamento, cancellationToken);

    public Task<LancamentoSemiAcabado?> ObterLancamentoCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterLancamentoCompletoAsync(codigoLancamento, cancellationToken);

    /// <summary>CabeÃ§alho persistido do lanÃ§amento mais recente de uma OP/item.</summary>
    public Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterLancamentoPorOpItemAsync(numeroOrdem, itemOrdem, cancellationToken);

    /// <summary>LanÃ§amento ABERTO (bloqueante) mais recente de uma OP/item â€” ver <see cref="ISemiAcabadoRepositorio"/>.</summary>
    public Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoAbertoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterLancamentoAbertoPorOpItemAsync(numeroOrdem, itemOrdem, cancellationToken);

    /// <summary>HistÃ³rico de lanÃ§amentos de uma OP/item (consulta/reimpressÃ£o, nunca compÃµe payload novo).</summary>
    public Task<IReadOnlyList<LancamentoSemiAcabadoPersistido>> ListarLancamentosPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
        => _repositorio.ListarLancamentosPorOpItemAsync(numeroOrdem, itemOrdem, cancellationToken);
}


internal sealed class InconsistenciaLancamentoSemiAcabadoException : Exception
{
    public InconsistenciaLancamentoSemiAcabadoException(string mensagem, Exception? innerException = null)
        : base(mensagem, innerException)
    {
    }
}
