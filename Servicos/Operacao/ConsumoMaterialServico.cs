using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Regras da Tela de Consumo de Materia-Prima. Tarefa 3: consulta (GET) da Ordem de Producao no SAP
/// via <see cref="IProductionOrderSapServico"/> (governanca/configuracao na fabrica), mapeia para o
/// modelo de tela e aplica a elegibilidade de componente. Sem pesagem real, sem POST, sem persistencia.
/// </summary>
public sealed class ConsumoMaterialServico
{
    public const string MensagemEstadoInicial = "Informe a Ordem de Produção para carregar os componentes.";
    public const string MensagemOrdemObrigatoria = "Informe a Ordem de Produção para consultar.";
    public const string MensagemNaoEncontrada = "Ordem de Produção não encontrada no SAP.";
    public const string MensagemSapIndisponivel = "Não foi possível consultar a Ordem de Produção no SAP no momento.";
    public const string MensagemNaoLiberada =
        "OP encontrada, mas nenhum componente consumível foi liberado para pesagem. Motivo: ordem não liberada para consumo no SAP.";
    public const string MensagemSemComponentes =
        "OP encontrada, porém o SAP não retornou componentes para consumo.";

    /// <summary>Tipo de movimento esperado para o consumo quando o SAP nao informar (modelo de tela; sem POST).</summary>
    public const string TipoMovimentoConsumoPadrao = "261";

    public const string UnidadePesavel = "KG";
    public const string MensagemPesoBrutoInvalido = "Informe um peso bruto maior que zero.";
    public const string MensagemPesoLiquidoInvalido = "Peso líquido deve ser maior que zero. Verifique o peso bruto e a tara.";
    public const string MensagemExcedePendente = "Peso líquido excede a quantidade pendente do componente.";
    public const string MensagemSemPesagemSalvar = "Nenhuma pesagem de consumo registrada para salvar.";
    public const string MensagemConsumoNaoCarregado = "Ordem de Produção não carregada.";
    public const string MensagemErroPersistencia = "Não foi possível salvar o consumo localmente. Acione o suporte.";

    private readonly IProductionOrderSapServico _ordemProducaoServico;
    // Repositorio/servico SAP LAZY: so sao construidos (config lida) ao SALVAR/ENVIAR â€” nao na construcao.
    private readonly Func<IConsumoMaterialRepositorio> _criarRepositorio;
    private readonly Func<IConsumoMaterialSap261Servico> _criarSap261Servico;
    private readonly Func<IConfirmacaoProducaoSapClient> _criarConfirmacaoProducaoServico;

    public ConsumoMaterialServico()
        : this(FabricaProductionOrderSapServico.Criar())
    {
    }

    internal ConsumoMaterialServico(IProductionOrderSapServico ordemProducaoServico)
        : this(
            ordemProducaoServico,
            () => new ConsumoMaterialRepositorio(
                new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())))
    {
    }

    internal ConsumoMaterialServico(
        IProductionOrderSapServico ordemProducaoServico,
        Func<IConsumoMaterialRepositorio> criarRepositorio)
        : this(ordemProducaoServico, criarRepositorio, FabricaConsumoMaterialSap261Servico.Criar)
    {
    }

    internal ConsumoMaterialServico(
        IProductionOrderSapServico ordemProducaoServico,
        Func<IConsumoMaterialRepositorio> criarRepositorio,
        Func<IConsumoMaterialSap261Servico> criarSap261Servico)
        : this(ordemProducaoServico, criarRepositorio, criarSap261Servico, () => new ConfirmacaoProducaoSapServico())
    {
    }

    internal ConsumoMaterialServico(
        IProductionOrderSapServico ordemProducaoServico,
        Func<IConsumoMaterialRepositorio> criarRepositorio,
        Func<IConsumoMaterialSap261Servico> criarSap261Servico,
        Func<IConfirmacaoProducaoSapClient> criarConfirmacaoProducaoServico)
    {
        _ordemProducaoServico = ordemProducaoServico
            ?? throw new ArgumentNullException(nameof(ordemProducaoServico));
        _criarRepositorio = criarRepositorio
            ?? throw new ArgumentNullException(nameof(criarRepositorio));
        _criarSap261Servico = criarSap261Servico
            ?? throw new ArgumentNullException(nameof(criarSap261Servico));
        _criarConfirmacaoProducaoServico = criarConfirmacaoProducaoServico
            ?? throw new ArgumentNullException(nameof(criarConfirmacaoProducaoServico));
    }

    public async Task<ResultadoConsultaOrdemConsumo> ConsultarOrdemAsync(
        string? numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        string numero = NormalizarNumeroOrdem(numeroOrdem);
        if (string.IsNullOrWhiteSpace(numero))
        {
            return ResultadoConsultaOrdemConsumo.Falha(MensagemOrdemObrigatoria);
        }

        ResultadoConsultaOrdemProducaoSap resultado =
            await _ordemProducaoServico.ConsultarOrdemAsync(numero, cancellationToken);

        return resultado.Cenario switch
        {
            CenarioConsultaOrdemProducaoSap.Encontrada when resultado.Ordem is not null =>
                Mapear(resultado.Ordem),
            CenarioConsultaOrdemProducaoSap.NaoEncontrada =>
                ResultadoConsultaOrdemConsumo.NaoEncontrada(numero, MensagemNaoEncontrada),
            // Indisponivel / NaoConfigurado: surfaca o diagnostico sanitizado do SAP (etapa/status/fallback)
            // quando houver; senao a mensagem amigavel padrao (Ajuste 3).
            _ => ResultadoConsultaOrdemConsumo.Indisponivel(
                numero,
                string.IsNullOrWhiteSpace(resultado.MensagemSanitizada)
                    ? MensagemSapIndisponivel
                    : resultado.MensagemSanitizada)
        };
    }

    /// <summary>
    /// Monta e PERSISTE LOCALMENTE (sem SAP) o consumo: cabecalho + itens (so componentes com pesagem)
    /// + pesagens, status PENDENTE_SAP. Valida OP carregada, >= 1 pesagem, unidade KG, peso liquido > 0
    /// e ausencia de excesso contra o pendente. Nao chama SAP. Persistencia atomica no repositorio.
    /// </summary>
    public async Task<ResultadoPersistenciaConsumoMaterial> SalvarConsumoLocalAsync(
        OrdemProducaoConsumo ordem,
        IReadOnlyCollection<ComponenteConsumoMaterial> componentes,
        IReadOnlyDictionary<string, List<PesagemConsumoMaterial>> pesagensPorComponente,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        if (ordem is null || string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
        {
            return ResultadoPersistenciaConsumoMaterial.DadosInvalidos(MensagemConsumoNaoCarregado);
        }

        int totalPesagens = pesagensPorComponente?.Values.Sum(lista => lista?.Count ?? 0) ?? 0;
        if (totalPesagens == 0)
        {
            return ResultadoPersistenciaConsumoMaterial.SemPesagem(MensagemSemPesagemSalvar);
        }

        List<ConsumoMaterialItem> itens = [];
        foreach (ComponenteConsumoMaterial componente in componentes)
        {
            string chave = ChaveComponente(componente);
            if (pesagensPorComponente is null
                || !pesagensPorComponente.TryGetValue(chave, out List<PesagemConsumoMaterial>? pesagens)
                || pesagens.Count == 0)
            {
                continue; // item somente para componentes COM pesagem
            }

            if (!string.Equals(componente.UnidadeMedida, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoPersistenciaConsumoMaterial.DadosInvalidos(
                    $"Componente {componente.CodigoMaterial} com unidade {componente.UnidadeMedida}. Conversão para pesagem ainda não implementada.");
            }

            decimal totalLiquido = 0m;
            List<ConsumoMaterialPesagem> pesagensItem = [];
            foreach (PesagemConsumoMaterial pesagem in pesagens)
            {
                if (pesagem.PesoLiquidoKg <= 0m)
                {
                    return ResultadoPersistenciaConsumoMaterial.DadosInvalidos(MensagemPesoLiquidoInvalido);
                }

                totalLiquido += pesagem.PesoLiquidoKg;
                pesagensItem.Add(new ConsumoMaterialPesagem
                {
                    Sequencia = pesagem.Sequencia,
                    PesoBrutoKg = pesagem.PesoBrutoKg,
                    PesoTaraKg = pesagem.PesoTaraKg,
                    PesoLiquidoKg = pesagem.PesoLiquidoKg,
                    Unidade = UnidadePesavel,
                    Origem = pesagem.Origem,
                    StatusPesagem = ConsumoMaterialPesagem.StatusRegistradaLocalmente,
                    PesadoEm = pesagem.PesadoEm,
                    UsuarioCriacao = usuario
                });
            }

            if (totalLiquido > componente.QuantidadePendente)
            {
                return ResultadoPersistenciaConsumoMaterial.DadosInvalidos(MensagemExcedePendente);
            }

            itens.Add(new ConsumoMaterialItem
            {
                NumeroOrdem = ordem.NumeroOrdem,
                CodigoMaterial = componente.CodigoMaterial,
                DescricaoMaterial = componente.DescricaoMaterial,
                Centro = componente.Centro,
                DepositoConsumo = componente.DepositoConsumo,
                NumeroReserva = componente.NumeroReserva,
                ItemReserva = componente.ItemReserva,
                Lote = componente.Lote,
                QuantidadePrevista = componente.QuantidadePrevista,
                QuantidadeRetiradaSap = componente.QuantidadeConsumida,
                QuantidadePendenteSap = componente.QuantidadePendente,
                QuantidadeConsumidaLocal = totalLiquido,
                Unidade = componente.UnidadeMedida,
                TipoMovimentoSap = string.IsNullOrWhiteSpace(componente.TipoMovimento)
                    ? TipoMovimentoConsumoPadrao
                    : componente.TipoMovimento,
                StatusItem = ConsumoMaterialItem.StatusPendenteSap,
                Pesagens = pesagensItem
            });
        }

        if (itens.Count == 0)
        {
            return ResultadoPersistenciaConsumoMaterial.SemPesagem(MensagemSemPesagemSalvar);
        }

        ConsumoMaterialLancamento lancamento = new()
        {
            NumeroOrdem = ordem.NumeroOrdem,
            Centro = ordem.Planta,
            MaterialProduzido = ordem.MaterialProduzido,
            LoteOrdem = ordem.LoteProdutoProduzido,
            QuantidadePrevista = ordem.QuantidadePrevista,
            Unidade = ordem.Unidade,
            StatusLancamento = ConsumoMaterialLancamento.StatusPendenteSap,
            UsuarioCriacao = usuario,
            Itens = itens
        };

        try
        {
            long codigo = await _criarRepositorio().SalvarConsumoLocalAsync(lancamento, cancellationToken);
            return ResultadoPersistenciaConsumoMaterial.Salvo(
                codigo,
                $"Consumo salvo localmente com sucesso. Lançamento {codigo} pendente de envio ao SAP.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Rollback ja aplicado pela transacao do repositorio; nao expoe detalhe tecnico.
            return ResultadoPersistenciaConsumoMaterial.Erro(MensagemErroPersistencia);
        }
    }

    /// <summary>
    /// Gera o PREVIEW do payload de consumo 261 (API_MATERIAL_DOCUMENT_SRV) a partir de um lancamento
    /// EM MEMORIA. Apenas montagem â€” NAO envia SAP, NAO faz POST, NAO busca CSRF. Testavel sem banco.
    /// </summary>
    public ResultadoPreviewConsumoSap261 GerarPreviewSap261(
        ConsumoMaterialLancamento lancamento,
        DateTime dataLancamentoUtc)
        => new ConsumoMaterialSapPayloadBuilder().MontarPreview261(lancamento, dataLancamentoUtc);

    /// <summary>
    /// Gera o preview 261 a partir de um lancamento PERSISTIDO (por codigo). Le o lancamento + itens
    /// no repositorio e delega ao builder. Nao envia SAP. (Caminho dependente do SQL 030 aplicado.)
    /// </summary>
    public async Task<ResultadoPreviewConsumoSap261> GerarPreviewSap261Async(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento =
            await _criarRepositorio().ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoPreviewConsumoSap261.Falha("Lançamento de consumo não encontrado.");
        }

        return GerarPreviewSap261(lancamento, DateTime.UtcNow);
    }

    /// <summary>
    /// Tarefa 16: PREVIEW de Confirmacao de Producao a partir do LANCAMENTO SALVO (dados persistidos).
    /// Somente montagem/visualizacao — sem POST/CSRF/PATCH.
    /// </summary>
    public async Task<ResultadoPreviewConfirmacaoProducao> GerarPreviewConfirmacaoProducaoDoLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento =
            await _criarRepositorio().ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoPreviewConfirmacaoProducao.Falha("Lançamento de consumo não encontrado.");
        }

        return ConfirmacaoProducaoPreviewBuilder.MontarDoLancamento(lancamento);
    }

    /// <summary>
    /// Tarefa 17.6: PREVIEW REAL da Confirmacao de Producao — resolve a operacao SAP (GET) e usa o MESMO
    /// builder do envio (<see cref="ConfirmacaoProducaoSapPayloadBuilder"/>), mostrando o JSON do POST.
    /// Disponivel p/ lancamento PENDENTE_SAP ou FALHA_SAP. NAO faz POST/CSRF/PATCH.
    /// </summary>
    public async Task<ResultadoPreviewConfirmacaoProducaoSap> GerarPreviewConfirmacaoProducaoRealAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento =
            await _criarRepositorio().ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Lançamento de consumo não encontrado.");
        }

        bool pendenteOuFalha =
            string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal)
            || string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusFalhaSap, StringComparison.Ordinal);
        if (!pendenteOuFalha)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha(
                "Preview da Confirmação disponível apenas para lançamento PENDENTE_SAP ou FALHA_SAP.");
        }

        ResultadoOperacaoConfirmacaoPrePost prePost =
            await PrepararPayloadConfirmacaoAsync(lancamento, _criarConfirmacaoProducaoServico(), cancellationToken);
        if (!prePost.Sucesso || prePost.Payload is null)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha(
                string.IsNullOrWhiteSpace(prePost.Mensagem)
                    ? "Não foi possível gerar o preview real da Confirmação porque a operação SAP não foi resolvida."
                    : prePost.Mensagem);
        }

        return ResultadoPreviewConfirmacaoProducaoSap.Ok(
            prePost.Payload,
            ConfirmacaoProducaoSapPayloadBuilder.SerializarPreview(prePost.Payload),
            "Preview real da Confirmação de Produção (payload do POST).");
    }

    public const string MensagemConsumoNaoEncontrado = "Lançamento de consumo não encontrado.";
    public const string MensagemReenvioBloqueado = "Consumo já confirmado no SAP. Reenvio bloqueado.";
    public const string MensagemJaEnviando = "Consumo já está em envio ao SAP. Aguarde ou verifique o status.";
    public const string MensagemFalhaSapBloqueado =
        "Este lançamento está com FALHA_SAP. Gere novo lançamento de teste ou use rotina futura de reprocessamento controlado.";
    public const string MensagemBackflushConfirmacaoYieldZero =
        "Este componente está configurado como Backflush no SAP. A confirmação com quantidade zero não gera movimento 261. Para consumir este material, é necessário apontamento real de produção na operação ou validação SAP para consumo manual via 261.";
    public const string MensagemConfirmacaoSemMovimento261 =
        "SAP criou a confirmação, mas não lançou o consumo 261. Como o componente é Backflush e a quantidade apontada da operação foi zero, nenhum item de material foi gerado.";
    public const string MensagemNaoPendente = "Consumo não está pendente de envio ao SAP.";
    public const string MensagemReservaFalhou = "Consumo já está em envio ou não está mais pendente. Reenvio bloqueado.";
    public const string MensagemSapExigeLote =
        "SAP exige lote para este componente. Informe o lote e gere novo consumo antes de reenviar.";
    public const string MensagemSapReservaNaoPermiteMovimento =
        "SAP recusou o movimento: a reserva/item não permite lançamento manual. Verifique se o componente é backflush ou se deve ser consumido pela confirmação de produção.";
    public const string MensagemConfirmacaoCritica =
        "Documento SAP pode ter sido criado, mas a confirmação local falhou (concorrência). Não reenviar; acione o suporte.";
    public const string MensagemBackflushUseConfirmacao =
        "Este componente é Backflush no SAP e não deve ser enviado por 261 direto.\n"
        + "Use o fluxo de Confirmação de Produção. Nesta versão, o envio por confirmação ainda está em preparação.";

    /// <summary>
    /// Envio CONTROLADO do consumo 261 ao SAP. Bloqueia reenvio (status != PENDENTE_SAP ou documento ja
    /// preenchido), monta o payload pelo builder (Tarefa 6), delega ao servico SAP 261 (CSRF/POST com
    /// WRITE_ENABLED) e, SOMENTE com documento/ano de retorno, marca CONFIRMADO_SAP localmente.
    /// </summary>
    public async Task<ResultadoEnvioConsumoSap261> EnviarConsumoSap261Async(
        long codigoLancamento,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        IConsumoMaterialRepositorio repositorio = _criarRepositorio();
        ConsumoMaterialLancamento? lancamento = await repositorio.ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemConsumoNaoEncontrado);
        }

        // Correcao 6: bloqueio de reenvio por status.
        if (!string.IsNullOrWhiteSpace(lancamento.DocumentoMaterialSap)
            || string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusConfirmadoSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemReenvioBloqueado);
        }

        if (string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusEnviandoSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemJaEnviando);
        }

        if (string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusFalhaSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemFalhaSapBloqueado);
        }

        if (!string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemNaoPendente);
        }

        // Payload pelo builder (sem duplicar montagem). Sem efeito colateral; se invalido, NAO reserva.
        ResultadoPreviewConsumoSap261 preview = GerarPreviewSap261(lancamento, DateTime.UtcNow);
        if (!preview.Sucesso || preview.Payload is null)
        {
            return ResultadoEnvioConsumoSap261.Falha(preview.Mensagem);
        }

        IConsumoMaterialSap261Servico sap261Servico = _criarSap261Servico();
        ResultadoEnvioConsumoSap261 prontidaoSap = sap261Servico.ValidarProntoParaEnvio();
        if (!prontidaoSap.Sucesso)
        {
            return prontidaoSap;
        }

        ResultadoEnvioConsumoSap261? bloqueioElegibilidade =
            await ValidarElegibilidadeEnvio261DiretoAsync(lancamento, cancellationToken);
        if (bloqueioElegibilidade is not null)
        {
            return bloqueioElegibilidade;
        }

        // CLAIM atomico (PENDENTE_SAP -> ENVIANDO_SAP) antes do POST e somente apos pre-condicao SAP OK.
        bool reservou = await repositorio.TentarReservarEnvioSapAsync(lancamento.Codigo, DateTime.UtcNow, cancellationToken);
        if (!reservou)
        {
            return ResultadoEnvioConsumoSap261.Falha(MensagemReservaFalhou);
        }

        string chaveNegocio = $"CONSUMO {lancamento.Codigo} OP {lancamento.NumeroOrdem}";
        ResultadoEnvioConsumoSap261 envio =
            await sap261Servico.EnviarConsumo261Async(preview.Payload, chaveNegocio, cancellationToken);
        if (!envio.Sucesso && MensagemIndicaLoteObrigatorioSap(envio.Mensagem))
        {
            envio = ResultadoEnvioConsumoSap261.Falha(
                MensagemSapExigeLote,
                envio.StatusHttp,
                envio.CorrelationId);
        }
        else if (!envio.Sucesso && MensagemIndicaReservaNaoPermiteMovimento(envio.Mensagem))
        {
            envio = ResultadoEnvioConsumoSap261.Falha(
                MensagemSapReservaNaoPermiteMovimento,
                envio.StatusHttp,
                envio.CorrelationId);
        }

        // Sucesso COM documento E ano: confirma (exige ENVIANDO_SAP). Erro de confirmacao = critico.
        if (envio.Sucesso
            && !string.IsNullOrWhiteSpace(envio.DocumentoMaterialSap)
            && !string.IsNullOrWhiteSpace(envio.ExercicioDocumentoMaterialSap))
        {
            try
            {
                await repositorio.MarcarConsumoConfirmadoSapAsync(
                    lancamento.Codigo,
                    envio.DocumentoMaterialSap!,
                    envio.ExercicioDocumentoMaterialSap!,
                    DateTime.UtcNow,
                    cancellationToken);
                return envio;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ResultadoEnvioConsumoSap261.Falha(MensagemConfirmacaoCritica, envio.StatusHttp, envio.CorrelationId);
            }
        }

        // Correcao 4: falha SAP apos a reserva -> FALHA_SAP (nao deixa eternamente ENVIANDO_SAP).
        await repositorio.MarcarFalhaSapAsync(lancamento.Codigo, cancellationToken);
        return envio;
    }

    public async Task<ResultadoEnvioConfirmacaoProducao> EnviarConfirmacaoProducaoAsync(
        long codigoLancamento,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        IConsumoMaterialRepositorio repositorio = _criarRepositorio();
        ConsumoMaterialLancamento? lancamento = await repositorio.ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemConsumoNaoEncontrado);
        }

        if (!string.IsNullOrWhiteSpace(lancamento.DocumentoMaterialSap)
            || string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusConfirmadoSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemReenvioBloqueado);
        }

        if (string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusEnviandoSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemJaEnviando);
        }

        if (string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusFalhaSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemFalhaSapBloqueado);
        }

        if (!string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal))
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemNaoPendente);
        }

        IConfirmacaoProducaoSapClient confirmacaoServico = _criarConfirmacaoProducaoServico();
        ResultadoEnvioConfirmacaoProducao prontidao = confirmacaoServico.ValidarProntoParaEnvio();
        if (!prontidao.Sucesso)
        {
            return prontidao;
        }

        ResultadoOperacaoConfirmacaoPrePost prePost =
            await PrepararPayloadConfirmacaoAsync(lancamento, confirmacaoServico, cancellationToken);
        if (!prePost.Sucesso || prePost.Payload is null)
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(prePost.Mensagem);
        }

        // Correcao 4 (Tarefa 17.11): confirmacao Backflush com yield ZERO nao gera movimento 261 (SAP cria
        // confirmacao vazia). Como o FugaPET nao aponta producao real, bloqueia ANTES de reservar/POST.
        if (ConfirmationYieldEhZero(prePost.Payload))
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemBackflushConfirmacaoYieldZero);
        }

        bool reservou = await repositorio.TentarReservarEnvioSapAsync(lancamento.Codigo, DateTime.UtcNow, cancellationToken);
        if (!reservou)
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemReservaFalhou);
        }

        string chaveNegocio = $"CONSUMO BACKFLUSH {lancamento.Codigo} OP {lancamento.NumeroOrdem}";
        ResultadoEnvioConfirmacaoProducao envio =
            await confirmacaoServico.EnviarConfirmacaoAsync(prePost.Payload, chaveNegocio, cancellationToken);

        if (envio.Sucesso
            && !string.IsNullOrWhiteSpace(envio.ConfirmationGroup)
            && !string.IsNullOrWhiteSpace(envio.ConfirmationCount)
            && !string.IsNullOrWhiteSpace(envio.ManufacturingOrder))
        {
            try
            {
                await repositorio.MarcarConsumoConfirmadoSapAsync(
                    lancamento.Codigo,
                    envio.DocumentoMaterialSap,
                    envio.ExercicioDocumentoMaterialSap,
                    DateTime.UtcNow,
                    cancellationToken);
                return ResultadoEnvioConfirmacaoProducao.Ok(
                    new ConfirmacaoProducaoSapResponse
                    {
                        Sucesso = true,
                        ConfirmationGroup = envio.ConfirmationGroup,
                        ConfirmationCount = envio.ConfirmationCount,
                        ManufacturingOrder = envio.ManufacturingOrder,
                        MaterialDocument = envio.DocumentoMaterialSap,
                        MaterialDocumentYear = envio.ExercicioDocumentoMaterialSap,
                        StatusHttp = envio.StatusHttp,
                        CorrelationId = envio.CorrelationId,
                        Mensagem = $"Operação SAP resolvida: {prePost.OperacaoResolvida} / sequência {prePost.SequenciaResolvida} / ID interno {prePost.OrderOperationInternalId}. {envio.Mensagem}"
                    });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ResultadoEnvioConfirmacaoProducao.Falha(MensagemConfirmacaoCritica, envio.StatusHttp, envio.CorrelationId);
            }
        }

        await repositorio.MarcarFalhaSapAsync(lancamento.Codigo, cancellationToken);
        return envio;
    }

    private async Task<ResultadoOperacaoConfirmacaoPrePost> PrepararPayloadConfirmacaoAsync(
        ConsumoMaterialLancamento lancamento,
        IConfirmacaoProducaoSapClient confirmacaoServico,
        CancellationToken cancellationToken)
    {
        ResultadoConsultaOrdemProducaoSap resultado =
            await _ordemProducaoServico.ConsultarOrdemAsync(lancamento.NumeroOrdem, cancellationToken);

        if (resultado.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || resultado.Ordem is null)
        {
            return ResultadoOperacaoConfirmacaoPrePost.Falha(
                "Não foi possível validar a OP no SAP antes da Confirmação de Produção. Envio não reservado.");
        }

        ResultadoConsultaOrdemConsumo ordemConsumo = Mapear(resultado.Ordem);
        if (ordemConsumo.Ordem is null)
        {
            return ResultadoOperacaoConfirmacaoPrePost.Falha(
                "Não foi possível validar a OP no SAP antes da Confirmação de Produção. Envio não reservado.");
        }

        List<ComponenteConsumoMaterial> componentes = [];
        foreach (ConsumoMaterialItem item in lancamento.Itens)
        {
            ComponenteConsumoMaterial? componente = EncontrarComponenteSapAtual(ordemConsumo.Ordem.Componentes, item);
            if (componente is null)
            {
                return ResultadoOperacaoConfirmacaoPrePost.Falha(
                    $"Componente não encontrado na OP atual do SAP. Reserva {FormatarReservaItem(item)}, material {item.CodigoMaterial}.");
            }

            if (!componente.BackflushSap)
            {
                return ResultadoOperacaoConfirmacaoPrePost.Falha(
                    $"Componente {item.CodigoMaterial} não é Backflush no SAP. Use o envio 261 direto quando elegível.");
            }

            if (string.IsNullOrWhiteSpace(componente.Lote) && !string.IsNullOrWhiteSpace(item.Lote))
            {
                componente.Lote = item.Lote.Trim();
            }

            componentes.Add(componente);
        }

        if (componentes.Count == 0)
        {
            return ResultadoOperacaoConfirmacaoPrePost.Falha("Lançamento sem componentes Backflush para confirmar.");
        }

        IReadOnlyList<OperacaoConfirmacaoSap> operacoesConfirmacao =
            await confirmacaoServico.ConsultarOperacoesConfirmacaoAsync(lancamento.NumeroOrdem, cancellationToken);

        List<OperacaoConfirmacaoSap> operacoesResolvidas = [];
        foreach (ComponenteConsumoMaterial componente in componentes)
        {
            OperacaoConfirmacaoSap? operacaoResolvida = ResolverOperacaoConfirmacao(
                componente,
                operacoesConfirmacao,
                out string motivoBloqueio);
            if (operacaoResolvida is null)
            {
                return ResultadoOperacaoConfirmacaoPrePost.Falha(motivoBloqueio);
            }

            operacoesResolvidas.Add(operacaoResolvida);
        }

        OperacaoConfirmacaoSap[] operacoesDistintas = operacoesResolvidas
            .GroupBy(op => $"{op.Sequence.Trim()}|{op.OrderOperation.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(grupo => grupo.First())
            .ToArray();
        if (operacoesDistintas.Length > 1)
        {
            return ResultadoOperacaoConfirmacaoPrePost.Falha(
                "Mais de uma operação corresponde ao lançamento. Valide os componentes Backflush no SAP antes de enviar.");
        }

        OperacaoConfirmacaoSap? operacaoConfirmacao = operacoesDistintas.FirstOrDefault();
        if (operacaoConfirmacao is null)
        {
            return ResultadoOperacaoConfirmacaoPrePost.Falha(
                ConfirmacaoProducaoSapPayloadBuilder.MensagemOrderOperationInternalIdAusente);
        }

        ResultadoPreviewConfirmacaoProducaoSap preview =
            new ConfirmacaoProducaoSapPayloadBuilder().MontarPreview(lancamento, operacaoConfirmacao, DateTime.UtcNow);
        return preview.Sucesso && preview.Payload is not null
            ? ResultadoOperacaoConfirmacaoPrePost.Ok(
                preview.Payload,
                operacaoConfirmacao.OrderOperation,
                operacaoConfirmacao.Sequence,
                operacaoConfirmacao.OrderOperationInternalId)
            : ResultadoOperacaoConfirmacaoPrePost.Falha(preview.ErrosValidacao.Count > 0
                ? preview.Mensagem + " " + string.Join(" ", preview.ErrosValidacao)
                : preview.Mensagem);
    }

    /// <summary>Tarefa 17.11: true se a quantidade APONTADA (yield) da confirmacao for zero — nao gera 261.</summary>
    private static bool ConfirmationYieldEhZero(ConfirmacaoProducaoSapRequest payload)
        => !decimal.TryParse(
               payload.ConfirmationYieldQuantity,
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture,
               out decimal yield)
           || yield <= 0m;

    private static OperacaoConfirmacaoSap? ResolverOperacaoConfirmacao(
        ComponenteConsumoMaterial componente,
        IReadOnlyCollection<OperacaoConfirmacaoSap> operacoesConfirmacao,
        out string motivoBloqueio)
    {
        motivoBloqueio = string.Empty;
        string operacaoComponente = (componente.Operacao ?? string.Empty).Trim();
        string sequenciaComponente = (componente.SequenciaOperacao ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(operacaoComponente))
        {
            motivoBloqueio =
                "Não foi possível determinar o OrderOperationInternalID." + Environment.NewLine
                + $"Componente: {componente.CodigoMaterial}" + Environment.NewLine
                + "Operação do componente: não informada.";
            return null;
        }

        List<OperacaoConfirmacaoSap> correspondencias = operacoesConfirmacao
            .Where(op => string.Equals(op.OrderOperation?.Trim(), operacaoComponente, StringComparison.OrdinalIgnoreCase)
                         && (string.IsNullOrWhiteSpace(sequenciaComponente)
                             || string.Equals(op.Sequence?.Trim(), sequenciaComponente, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (correspondencias.Count == 1)
        {
            OperacaoConfirmacaoSap operacao = correspondencias[0];
            if (string.IsNullOrWhiteSpace(operacao.OrderOperation))
            {
                motivoBloqueio =
                    "Operação SAP de confirmação não informada. Envio não será executado.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(operacao.Sequence))
            {
                motivoBloqueio =
                    "Sequência SAP da operação de confirmação não informada. Envio não será executado.";
                return null;
            }

            return operacao;
        }

        if (correspondencias.Count == 0)
        {
            motivoBloqueio =
                "Não foi possível determinar o OrderOperationInternalID." + Environment.NewLine
                + $"Componente: {componente.CodigoMaterial}" + Environment.NewLine
                + $"Operação do componente: {operacaoComponente}" + Environment.NewLine
                + $"Sequência do componente: {sequenciaComponente}" + Environment.NewLine
                + "Nenhuma operação correspondente foi encontrada em ProdnOrdConf2.";
            return null;
        }

        motivoBloqueio =
            "Mais de uma operação corresponde ao componente." + Environment.NewLine
            + $"Componente: {componente.CodigoMaterial}" + Environment.NewLine
            + $"Operação: {operacaoComponente}" + Environment.NewLine
            + $"Sequência: {sequenciaComponente}" + Environment.NewLine
            + "Valide a operação no SAP antes de enviar.";
        return null;
    }

    private sealed class ResultadoOperacaoConfirmacaoPrePost
    {
        public bool Sucesso { get; init; }
        public string Mensagem { get; init; } = string.Empty;
        public ConfirmacaoProducaoSapRequest? Payload { get; init; }
        public string OperacaoResolvida { get; init; } = string.Empty;
        public string SequenciaResolvida { get; init; } = string.Empty;
        public string OrderOperationInternalId { get; init; } = string.Empty;

        public static ResultadoOperacaoConfirmacaoPrePost Ok(
            ConfirmacaoProducaoSapRequest payload,
            string operacaoResolvida,
            string sequenciaResolvida,
            string orderOperationInternalId)
            => new()
            {
                Sucesso = true,
                Payload = payload,
                OperacaoResolvida = operacaoResolvida,
                SequenciaResolvida = sequenciaResolvida,
                OrderOperationInternalId = orderOperationInternalId
            };

        public static ResultadoOperacaoConfirmacaoPrePost Falha(string mensagem)
            => new() { Sucesso = false, Mensagem = mensagem };
    }

    private static bool MensagemIndicaLoteObrigatorioSap(string? mensagem)
        => !string.IsNullOrWhiteSpace(mensagem)
           && (mensagem.Contains("M7/018", StringComparison.OrdinalIgnoreCase)
               || mensagem.Contains("Enter Batch", StringComparison.OrdinalIgnoreCase));

    private static bool MensagemIndicaReservaNaoPermiteMovimento(string? mensagem)
        => !string.IsNullOrWhiteSpace(mensagem)
           && (mensagem.Contains("M7/509", StringComparison.OrdinalIgnoreCase)
               || mensagem.Contains("no movements can be posted", StringComparison.OrdinalIgnoreCase));

    private async Task<ResultadoEnvioConsumoSap261?> ValidarElegibilidadeEnvio261DiretoAsync(
        ConsumoMaterialLancamento lancamento,
        CancellationToken cancellationToken)
    {
        ResultadoConsultaOrdemProducaoSap resultado =
            await _ordemProducaoServico.ConsultarOrdemAsync(lancamento.NumeroOrdem, cancellationToken);

        if (resultado.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || resultado.Ordem is null)
        {
            return ResultadoEnvioConsumoSap261.Falha(
                "Não foi possível validar a elegibilidade SAP dos componentes antes do envio 261. Envio não reservado.");
        }

        ResultadoConsultaOrdemConsumo ordemConsumo = Mapear(resultado.Ordem);
        if (ordemConsumo.Ordem is null)
        {
            return ResultadoEnvioConsumoSap261.Falha(
                "Não foi possível validar a elegibilidade SAP dos componentes antes do envio 261. Envio não reservado.");
        }

        foreach (ConsumoMaterialItem item in lancamento.Itens)
        {
            ComponenteConsumoMaterial? componente = EncontrarComponenteSapAtual(ordemConsumo.Ordem.Componentes, item);
            if (componente is null)
            {
                return ResultadoEnvioConsumoSap261.Falha(
                    $"Componente não elegível para envio 261 direto. Reserva {FormatarReservaItem(item)}, material {item.CodigoMaterial}. Motivo: componente não encontrado na OP atual do SAP.");
            }

            if (string.IsNullOrWhiteSpace(componente.Lote) && !string.IsNullOrWhiteSpace(item.Lote))
            {
                componente.Lote = item.Lote.Trim();
            }

            // Backflush: classificado como REQUER_CONFIRMACAO_PRODUCAO. Bloqueia o 261 direto ANTES da
            // reserva/CSRF/POST (mantem PENDENTE_SAP, NAO marca FALHA_SAP) com orientacao operacional.
            if (componente.BackflushSap)
            {
                return ResultadoEnvioConsumoSap261.Falha(MensagemBackflushUseConfirmacao);
            }

            bool elegivel = AvaliarElegibilidadeMaterialDocument261Direto(componente, out string motivo);
            if (!elegivel)
            {
                return ResultadoEnvioConsumoSap261.Falha(
                    $"Componente não elegível para envio 261 direto. Reserva {FormatarReservaItem(item)}, material {item.CodigoMaterial}. Motivo: {motivo}");
            }
        }

        return null;
    }

    private static ComponenteConsumoMaterial? EncontrarComponenteSapAtual(
        IReadOnlyList<ComponenteConsumoMaterial> componentes,
        ConsumoMaterialItem item)
        => componentes.FirstOrDefault(componente =>
            MesmoTexto(componente.CodigoMaterial, item.CodigoMaterial)
            && MesmoTexto(componente.NumeroReserva, item.NumeroReserva)
            && MesmoTexto(componente.ItemReserva, item.ItemReserva)
            && MesmoTexto(componente.DepositoConsumo, item.DepositoConsumo));

    private static bool MesmoTexto(string? esquerda, string? direita)
        => string.Equals((esquerda ?? string.Empty).Trim(), (direita ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static string FormatarReservaItem(ConsumoMaterialItem item)
        => $"{(item.NumeroReserva ?? string.Empty).Trim()}/{(item.ItemReserva ?? string.Empty).Trim()}";

    private static ResultadoConsultaOrdemConsumo Mapear(OrdemProducaoSap sap)
    {
        bool liberada = sap.Liberada && !sap.Excluida;

        int componentesSap = sap.Componentes.Count;
        IReadOnlyList<ComponenteOrdemProducaoSap> comMaterial = sap.Componentes
            .Where(componente => !string.IsNullOrWhiteSpace(componente.Material))
            .ToList();

        IReadOnlyList<ComponenteConsumoMaterial> componentes = comMaterial
            .Select(componente => MapearComponente(componente, liberada))
            .ToList();

        OrdemProducaoConsumo ordem = new()
        {
            NumeroOrdem = sap.NumeroOrdem,
            TipoOrdem = sap.TipoOrdem,
            MaterialProduzido = sap.MaterialProduzido,
            Planta = sap.Centro,
            QuantidadePrevista = sap.QuantidadePrevista,
            Unidade = sap.Unidade,
            ItemOrdem = sap.Itens.Count > 0 ? sap.Itens[0].ItemOrdem : string.Empty,
            DepositoConsumo = sap.Deposito,
            LoteProdutoProduzido = ObterLoteProdutoProduzido(sap),
            Lote = ObterLoteProdutoProduzido(sap),
            DataOrdem = sap.DataOrdem,
            OrigemDataOrdem = sap.OrigemDataOrdem,
            Liberada = liberada,
            Componentes = componentes,
            Operacoes = sap.Operacoes.Select(MapearOperacao).ToList()
        };

        int pesaveis = componentes.Count(componente => componente.PesagemLiberada);

        // Diagnostico tecnico SANITIZADO (apenas contagens). O fallback ja foi resolvido no client.
        string diagnostico =
            $"OP {sap.NumeroOrdem}: consulta OK; componentes SAP (expand+fallback)={componentesSap}; "
            + $"com material={comMaterial.Count}; elegiveis para pesagem={pesaveis}.";
        string alertaLoteProdutoSemLoteComponente = DeveAlertarLoteProdutoSemLoteComponente(ordem)
            ? " OP possui lote do produto produzido, mas o lote dos componentes não foi retornado pelo SAP. Para consumo 261, informe o lote do componente."
            : string.Empty;

        if (pesaveis > 0)
        {
            RegistrarDiagnosticoConsulta(diagnostico + alertaLoteProdutoSemLoteComponente);
            return ResultadoConsultaOrdemConsumo.Carregada(
                ordem,
                $"OP {ordem.NumeroOrdem} carregada. Selecione o componente para consumo.{alertaLoteProdutoSemLoteComponente}",
                diagnostico + alertaLoteProdutoSemLoteComponente);
        }

        // Nenhum componente pesavel: grid NUNCA fica vazio em silencio â€” sempre ha mensagem clara.
        if (!liberada)
        {
            RegistrarDiagnosticoConsulta(diagnostico + " motivo=ordem nao liberada.");
            return ResultadoConsultaOrdemConsumo.NaoLiberada(ordem, MensagemNaoLiberada, diagnostico);
        }

        if (componentesSap == 0)
        {
            RegistrarDiagnosticoConsulta(diagnostico + " motivo=SAP sem componentes.");
            return ResultadoConsultaOrdemConsumo.SemComponentes(ordem, MensagemSemComponentes, diagnostico);
        }

        string motivo = DeterminarMotivoSemElegivel(comMaterial.Count, componentes);
        RegistrarDiagnosticoConsulta(diagnostico + $" motivo={motivo}.");
        return ResultadoConsultaOrdemConsumo.SemComponentes(
            ordem,
            $"OP encontrada, mas nenhum componente consumível foi liberado para pesagem. Motivo: {motivo}.",
            diagnostico);
    }

    /// <summary>Motivo tecnico quando ha componentes no SAP mas NENHUM elegivel para pesagem.</summary>
    private static string DeterminarMotivoSemElegivel(
        int comMaterial,
        IReadOnlyList<ComponenteConsumoMaterial> componentes)
    {
        if (comMaterial == 0)
        {
            return "componentes sem material";
        }

        if (componentes.All(c => c.QuantidadePendente <= 0m))
        {
            return "todos os componentes já consumidos (quantidade pendente zerada)";
        }

        if (componentes.Any(c => !string.Equals(c.UnidadeMedida, UnidadePesavel, StringComparison.OrdinalIgnoreCase)))
        {
            return "unidade diferente de KG";
        }

        if (componentes.Any(c => string.IsNullOrWhiteSpace(c.DepositoConsumo)))
        {
            return "depósito de consumo ausente";
        }

        return "nenhum componente elegível para pesagem";
    }

    private static string ObterLoteProdutoProduzido(OrdemProducaoSap sap)
        => sap.Itens.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Lote))?.Lote
           ?? sap.Lote
           ?? string.Empty;

    private static bool DeveAlertarLoteProdutoSemLoteComponente(OrdemProducaoConsumo ordem)
        => !string.IsNullOrWhiteSpace(ordem.LoteProdutoProduzido)
           && ordem.Componentes.Count > 0
           && ordem.Componentes.All(componente => string.IsNullOrWhiteSpace(componente.Lote));

    private static void RegistrarDiagnosticoConsulta(string mensagem)
        => System.Diagnostics.Trace.TraceInformation($"[Consumo][ConsultaOP] {mensagem}");

    private static ComponenteConsumoMaterial MapearComponente(
        ComponenteOrdemProducaoSap componente,
        bool ordemLiberada)
    {
        decimal pendenteBruto = componente.QuantidadeNecessaria - componente.QuantidadeRetirada; // pode ser negativo
        decimal pendente = Math.Max(0m, pendenteBruto);
        bool consumido = pendente <= 0m;

        ComponenteConsumoMaterial consumo = new()
        {
            CodigoMaterial = componente.Material,
            DescricaoMaterial = string.Empty, // SAP do componente nao traz texto do material no GET.
            Centro = componente.Centro,
            DepositoConsumo = componente.Deposito,
            NumeroReserva = componente.Reserva,
            ItemReserva = componente.ItemReserva,
            Lote = componente.Lote,
            QuantidadePrevista = componente.QuantidadeNecessaria,
            QuantidadeConsumida = componente.QuantidadeRetirada,
            QuantidadePendente = pendente,
            QuantidadePendenteSapOriginal = pendenteBruto,
            QuantidadeDisponivelConfirmada = componente.QuantidadeDisponivelConfirmada,
            UnidadeMedida = componente.UnidadeBase,
            TipoMovimento = string.IsNullOrWhiteSpace(componente.TipoMovimento)
                ? TipoMovimentoConsumoPadrao
                : componente.TipoMovimento,
            ItemBOM = componente.ItemBOM,
            CategoriaItemBOM = componente.CategoriaItemBOM,
            ReservaFinalizada = componente.ReservaFinalizada,
            MarcadoParaEliminacao = componente.MarcadoParaEliminacao,
            MaterialGranel = componente.MaterialGranel,
            BackflushSap = componente.BackflushSap,
            TipoSplitLote = componente.TipoSplitLote,
            Operacao = componente.Operacao,
            OrderOperationInternalId = componente.OrderOperationInternalId,
            SequenciaOperacao = componente.SequenciaOperacao,
            Status = consumido
                ? ComponenteConsumoMaterial.StatusConsumido
                : ComponenteConsumoMaterial.StatusPendente
        };

        // Pesagem liberada SOMENTE com OP liberada E pela regra central (deposito/saldo/unidade/status).
        bool regraLiberou = AvaliarLiberacaoPesagem(consumo, out string motivoBloqueio);
        bool liberadoPesagem = ordemLiberada && regraLiberou;
        consumo.PesagemLiberada = liberadoPesagem;
        consumo.MotivoBloqueioPesagem = liberadoPesagem
            ? string.Empty
            : (ordemLiberada ? motivoBloqueio : "ordem não liberada para consumo no SAP.");

        consumo.ElegivelMaterialDocument261Direto =
            AvaliarElegibilidadeMaterialDocument261Direto(consumo, out string motivo);
        consumo.MotivoInelegibilidadeMaterialDocument261 = motivo;
        consumo.ClassificacaoEnvio = ClassificarEnvioConsumo261(consumo);
        return consumo;
    }

    public static bool AvaliarElegibilidadeMaterialDocument261Direto(
        ComponenteConsumoMaterial componente,
        out string motivo)
    {
        ArgumentNullException.ThrowIfNull(componente);

        if (!string.Equals(componente.TipoMovimento, TipoMovimentoConsumoPadrao, StringComparison.OrdinalIgnoreCase))
        {
            motivo = $"tipo de movimento SAP diferente de {TipoMovimentoConsumoPadrao}.";
            return false;
        }

        if (componente.ReservaFinalizada)
        {
            motivo = "reserva/item finalizado no SAP; não permite novo lançamento manual.";
            return false;
        }

        if (componente.MarcadoParaEliminacao)
        {
            motivo = "componente marcado para eliminação no SAP.";
            return false;
        }

        if (componente.MaterialGranel)
        {
            motivo = "componente marcado como material a granel no SAP.";
            return false;
        }

        if (componente.BackflushSap)
        {
            motivo = "componente marcado como backflush no SAP; SAP pode exigir consumo via confirmação de produção.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            motivo = "depósito de consumo ausente.";
            return false;
        }

        if (componente.QuantidadePendente <= 0m)
        {
            motivo = "quantidade pendente zerada.";
            return false;
        }

        if (!string.Equals(componente.UnidadeMedida, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
        {
            motivo = $"unidade {componente.UnidadeMedida} não suportada para envio 261 direto; esperado KG.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(componente.Lote))
        {
            motivo = "lote do componente obrigatório para envio 261 direto.";
            return false;
        }

        motivo = string.Empty;
        return true;
    }

    /// <summary>
    /// Classifica o caminho de envio do componente: Backflush -> Confirmacao de Producao;
    /// elegivel -> 261 direto; senao -> bloqueado. Backflush tem prioridade (mesmo que outros
    /// criterios falhem, o SAP exige consumo via confirmacao). Apenas classificacao — sem POST.
    /// </summary>
    public static ClassificacaoEnvioConsumo261 ClassificarEnvioConsumo261(ComponenteConsumoMaterial componente)
    {
        ArgumentNullException.ThrowIfNull(componente);

        if (componente.BackflushSap)
        {
            return ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao;
        }

        return AvaliarElegibilidadeMaterialDocument261Direto(componente, out _)
            ? ClassificacaoEnvioConsumo261.MaterialDocument261Direto
            : ClassificacaoEnvioConsumo261.Bloqueado;
    }

    /// <summary>
    /// Tarefa 16: classifica a ROTA de envio do consumo a partir dos componentes consumidos (agregacao).
    /// Bloqueado se vazio ou se algum item nao tiver deposito/saldo/lote ou for inelegivel; Backflush se
    /// TODOS forem Backflush; 261 direto se TODOS forem elegiveis; Misto caso contrario. Sem POST/persistencia.
    /// </summary>
    public static RotaEnvioConsumo ClassificarRotaEnvio(IReadOnlyCollection<ComponenteConsumoMaterial> componentesConsumidos)
    {
        if (componentesConsumidos is null || componentesConsumidos.Count == 0)
        {
            return RotaEnvioConsumo.Bloqueado;
        }

        List<ClassificacaoEnvioConsumo261> classificacoes = [];
        foreach (ComponenteConsumoMaterial componente in componentesConsumidos)
        {
            if (string.IsNullOrWhiteSpace(componente.DepositoConsumo)
                || componente.QuantidadePendente <= 0m
                || string.IsNullOrWhiteSpace(componente.Lote))
            {
                return RotaEnvioConsumo.Bloqueado;
            }

            ClassificacaoEnvioConsumo261 classificacao = ClassificarEnvioConsumo261(componente);
            if (classificacao == ClassificacaoEnvioConsumo261.Bloqueado)
            {
                return RotaEnvioConsumo.Bloqueado;
            }

            classificacoes.Add(classificacao);
        }

        if (classificacoes.All(c => c == ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao))
        {
            return RotaEnvioConsumo.BackflushConfirmacao;
        }

        if (classificacoes.All(c => c == ClassificacaoEnvioConsumo261.MaterialDocument261Direto))
        {
            return RotaEnvioConsumo.Direto261;
        }

        return RotaEnvioConsumo.Misto;
    }

    /// <summary>
    /// PREVIEW TECNICO (NAO enviado ao SAP) do caminho de Confirmacao de Producao para componente
    /// Backflush. Montagem PURA; sem POST/CSRF/PATCH/client.
    /// </summary>
    public ResultadoPreviewConfirmacaoProducao GerarPreviewConfirmacaoProducao(
        OrdemProducaoConsumo ordem,
        ComponenteConsumoMaterial componente,
        decimal quantidadeConsumidaLocal)
        => ConfirmacaoProducaoPreviewBuilder.Montar(ordem, componente, quantidadeConsumidaLocal);

    private static OperacaoOrdemConsumo MapearOperacao(OperacaoOrdemProducaoSap operacao)
        => new()
        {
            Operacao = operacao.Operacao,
            OrderOperationInternalId = operacao.OrderOperationInternalId,
            Sequencia = operacao.Sequencia,
            CentroTrabalho = operacao.CentroTrabalho,
            Planta = operacao.Centro,
            Descricao = operacao.Descricao,
            QuantidadePrevista = operacao.QuantidadePrevista,
            Unidade = operacao.Unidade
        };

    /// <summary>
    /// Normaliza a OP digitada ou lida por codigo de barras: remove caracteres de controle
    /// (prefixo/sufixo do leitor) e espacos das pontas, preservando zeros significativos.
    /// </summary>
    public static string NormalizarNumeroOrdem(string? numeroOrdem)
    {
        if (string.IsNullOrEmpty(numeroOrdem))
        {
            return string.Empty;
        }

        string semControle = new(numeroOrdem.Where(caractere => !char.IsControl(caractere)).ToArray());
        return semControle.Trim();
    }

    /// <summary>
    /// Chave COMPOSTA do componente (material + reserva + item reserva + lote + deposito). Evita
    /// somar pesagens de materiais iguais em reservas/itens/lotes diferentes na mesma OP.
    /// </summary>
    public static string ChaveComponente(ComponenteConsumoMaterial componente)
    {
        ArgumentNullException.ThrowIfNull(componente);
        return Chave(
            componente.CodigoMaterial,
            componente.NumeroReserva,
            componente.ItemReserva,
            componente.Lote,
            componente.DepositoConsumo);
    }

    /// <summary>Chave composta a partir de uma PESAGEM (mesma composicao do componente).</summary>
    public static string ChavePesagem(PesagemConsumoMaterial pesagem)
    {
        ArgumentNullException.ThrowIfNull(pesagem);
        return Chave(
            pesagem.CodigoMaterial,
            pesagem.NumeroReserva,
            pesagem.ItemReserva,
            pesagem.Lote,
            pesagem.DepositoConsumo);
    }

    private static string Chave(string? material, string? reserva, string? itemReserva, string? lote, string? deposito)
    {
        static string N(string? v) => (v ?? string.Empty).Trim().ToUpperInvariant();
        return string.Join('|', N(material), N(reserva), N(itemReserva), N(lote), N(deposito));
    }

    /// <summary>
    /// Recalcula o status local do componente a partir do total ja pesado localmente: completo
    /// (>= pendente) bloqueia a pesagem (CONSUMIDO); abaixo do pendente reabre (PENDENTE). Permite
    /// reabrir a pesagem ao excluir uma pesagem que havia completado o componente.
    /// </summary>
    public static void AtualizarStatusComponentePorTotalLocal(
        ComponenteConsumoMaterial componente,
        decimal totalLocalKg)
    {
        ArgumentNullException.ThrowIfNull(componente);
        // Completo quando o total local atinge o pendente (inclui pendente == 0: ja consumido no SAP).
        bool completo = totalLocalKg >= componente.QuantidadePendente;
        componente.Status = completo
            ? ComponenteConsumoMaterial.StatusConsumido
            : ComponenteConsumoMaterial.StatusPendente;
        // REGRA CENTRAL: nunca reabilita componente sem deposito/unidade/saldo apenas por estar pendente.
        bool regraLiberou = AvaliarLiberacaoPesagem(componente, out string motivo);
        bool liberado = !completo && regraLiberou;
        componente.PesagemLiberada = liberado;
        componente.MotivoBloqueioPesagem = liberado
            ? string.Empty
            : (completo ? "componente já consumido." : motivo);
    }

    /// <summary>
    /// REGRA CENTRAL de liberacao de pesagem de um componente (Ajuste 4/5). Bloqueia sem deposito,
    /// pendente &lt;= 0 (inclui saldo SAP negativo), unidade != KG ou ja consumido. Pura/testavel.
    /// </summary>
    public static bool AvaliarLiberacaoPesagem(ComponenteConsumoMaterial componente, out string motivo)
    {
        ArgumentNullException.ThrowIfNull(componente);

        if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            motivo = "depósito de consumo ausente.";
            return false;
        }

        if (componente.QuantidadePendente <= 0m)
        {
            motivo = "quantidade pendente zerada ou negativa.";
            return false;
        }

        if (!string.Equals(componente.UnidadeMedida, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
        {
            motivo = $"unidade {componente.UnidadeMedida} não suportada para pesagem; esperado KG.";
            return false;
        }

        if (string.Equals(componente.Status, ComponenteConsumoMaterial.StatusConsumido, StringComparison.OrdinalIgnoreCase))
        {
            motivo = "componente já consumido.";
            return false;
        }

        motivo = string.Empty;
        return true;
    }

    /// <summary>
    /// Parser SEGURO de peso em KG (Ajuste 3). Aceita virgula/ponto decimal, sufixo "kg" e espacos;
    /// rejeita vazio/zero/negativo. NUNCA remove o separador decimal (o ultimo separador e o decimal;
    /// os anteriores sao milhar). Ex.: "0,4"=>0.4, "10 kg"=>10, "1.234,5"=>1234.5.
    /// </summary>
    public static bool TryParsePesoConsumoKg(string? texto, out decimal peso)
    {
        peso = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpo = texto.Trim();
        limpo = System.Text.RegularExpressions.Regex.Replace(limpo, "(?i)kg", string.Empty);
        limpo = new string(limpo.Where(caractere => !char.IsWhiteSpace(caractere)).ToArray());
        if (limpo.Length == 0)
        {
            return false;
        }

        int posDecimal = Math.Max(limpo.LastIndexOf(','), limpo.LastIndexOf('.'));
        string normalizado;
        if (posDecimal < 0)
        {
            normalizado = limpo;
        }
        else
        {
            string parteInteira = limpo[..posDecimal].Replace(",", string.Empty).Replace(".", string.Empty);
            string parteDecimal = limpo[(posDecimal + 1)..].Replace(",", string.Empty).Replace(".", string.Empty);
            normalizado = $"{parteInteira}.{parteDecimal}";
        }

        if (!decimal.TryParse(
                normalizado,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal valor))
        {
            return false;
        }

        if (valor <= 0m)
        {
            return false;
        }

        peso = valor;
        return true;
    }

    /// <summary>
    /// Valida e registra (em memoria) uma pesagem LOCAL de consumo do componente. Sem banco/SAP/impressao.
    /// Regras: unidade KG; peso bruto &gt; 0; tara &gt;= 0; liquido = bruto - tara &gt; 0; e o novo total
    /// pesado localmente nao pode exceder a quantidade pendente do componente.
    /// </summary>
    public ResultadoPesagemConsumo RegistrarPesagemLocal(
        ComponenteConsumoMaterial componente,
        string numeroOrdem,
        decimal pesoBrutoKg,
        decimal pesoTaraKg,
        string origem,
        decimal totalJaPesadoLocalKg,
        int sequencia)
    {
        ArgumentNullException.ThrowIfNull(componente);

        // Camada A (Tarefa 14.1): regra central de liberacao ANTES de qualquer validacao de peso/tara/excesso.
        // RegistrarPesagemLocal NUNCA aceita componente sem deposito/saldo/unidade/consumido (mesmo se a tela falhar).
        if (!AvaliarLiberacaoPesagem(componente, out string motivoBloqueio))
        {
            return ResultadoPesagemConsumo.Bloqueada(
                CenarioPesagemConsumo.ComponenteNaoLiberado,
                $"Componente não liberado para pesagem: {motivoBloqueio}");
        }

        // Unidade: nesta tarefa so KG (sem conversao automatica).
        if (!string.Equals(componente.UnidadeMedida, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoPesagemConsumo.Bloqueada(
                CenarioPesagemConsumo.UnidadeNaoSuportada,
                $"Componente com unidade {componente.UnidadeMedida}. Conversão para pesagem ainda não implementada.");
        }

        if (pesoBrutoKg <= 0m)
        {
            return ResultadoPesagemConsumo.Bloqueada(
                CenarioPesagemConsumo.PesoBrutoInvalido, MensagemPesoBrutoInvalido);
        }

        decimal liquido = pesoBrutoKg - pesoTaraKg;
        if (pesoTaraKg < 0m || liquido <= 0m)
        {
            return ResultadoPesagemConsumo.Bloqueada(
                CenarioPesagemConsumo.PesoLiquidoInvalido, MensagemPesoLiquidoInvalido);
        }

        // Excesso contra a quantidade pendente do SAP (sem tolerancia inventada). Compara pelo LIQUIDO
        // (ja descontada a tara). Tarefa 18.2: mensagem detalhada com saldo/previsto/utilizado.
        decimal saldoDisponivel = componente.QuantidadePendente - totalJaPesadoLocalKg;
        decimal novoTotal = totalJaPesadoLocalKg + liquido;
        if (novoTotal > componente.QuantidadePendente)
        {
            return ResultadoPesagemConsumo.Bloqueada(
                CenarioPesagemConsumo.ExcedePendente,
                $"Peso líquido informado ({liquido:0.###} KG) ultrapassa o saldo previsto do componente ({saldoDisponivel:0.###} KG). "
                + $"Peso previsto: {componente.QuantidadePendente:0.###} KG. Já utilizado: {totalJaPesadoLocalKg:0.###} KG.");
        }

        string origemNormalizada = string.Equals(origem, PesagemConsumoMaterial.OrigemManual, StringComparison.OrdinalIgnoreCase)
            ? PesagemConsumoMaterial.OrigemManual
            : PesagemConsumoMaterial.OrigemBalanca;

        PesagemConsumoMaterial pesagem = new()
        {
            Sequencia = sequencia,
            NumeroOrdem = numeroOrdem,
            CodigoMaterial = componente.CodigoMaterial,
            DescricaoMaterial = componente.DescricaoMaterial,
            DepositoConsumo = componente.DepositoConsumo,
            NumeroReserva = componente.NumeroReserva,
            ItemReserva = componente.ItemReserva,
            Lote = componente.Lote,
            PesoBrutoKg = pesoBrutoKg,
            PesoTaraKg = pesoTaraKg,
            PesoLiquidoKg = liquido,
            UnidadeMedida = componente.UnidadeMedida,
            // timestamptz exige UTC (DateTime.Now seria Local). Pesagem nasce em UTC.
            PesadoEm = DateTime.UtcNow,
            Origem = origemNormalizada,
            StatusLocal = PesagemConsumoMaterial.StatusRegistradaLocalmente
        };

        return ResultadoPesagemConsumo.Registrada(
            pesagem,
            $"Pesagem de {liquido:0.###} KG registrada localmente para {componente.CodigoMaterial}.");
    }
}
