============================================================
PACOTE 039 - CONTROLE DE APONTAMENTOS DE PRODUCAO
Projeto: FugaPET_Dev   |   Schema alvo: desenvolvimento   |   Autor logico: GAIA
============================================================

STATUS: NAO EXECUTADO. Entregue apenas como pacote versionado. Nenhum SQL foi rodado contra
qualquer banco. Ambiente: SOMENTE DEV. NAO promover para HML/PRD nesta tarefa.

NUMERACAO: a numeracao foi VERIFICADA antes de escolher. 035, 036, 037 e 038 ja estao em uso
(038 = 038_cancelamento_lancamento_semi_acabado_GAIA). O proximo numero livre e o 039.

------------------------------------------------------------
1. O QUE ESTE PACOTE CRIA
------------------------------------------------------------
  desenvolvimento.operacao_producao_configuracao
     Liga uma operacao SAP a uma tela do FugaPET. O roteamento e feito POR DADOS
     (centro + tipo_ordem + sequencia + operacao + suboperacao), NUNCA pela descricao da
     operacao e nunca por if/switch de OP/produto no codigo.
     tipo_processo: CONSUMO_MATERIA_PRIMA | CONSUMO_QUIMICOS | SEMI_ACABADO | PRODUTO_ACABADO |
                    SEM_DESTINO_CONFIGURADO

  desenvolvimento.operacao_producao_apontamento
     Um apontamento por operacao. NASCE no evento de inicio (01) e e CONCLUIDO pela leitura do
     codigo de termino (02) - o termino NUNCA cria um apontamento novo.

  desenvolvimento.operacao_producao_evento
     Auditoria de CADA leitura: codigo original (como lido), formato interpretado, OP/operacao/
     evento extraidos, usuario, estacao, data/hora, status anterior/novo, resultado, mensagem e
     correlation_id. O codigo original NUNCA e substituido pelo codigo regenerado.

------------------------------------------------------------
2. ARQUIVOS (ordem de uso)
------------------------------------------------------------
  039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql   Somente leitura. Valida TODAS as colunas
                                                     (nome + tipo + nullability) e ABORTA em
                                                     estrutura parcial incompativel/duplicidades.
  039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql    DDL idempotente + grants + proposta de permissoes.
  039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql   Confere objetos, roda testes funcionais dos
                                                     checks/indices (rollback, sem residuo) e valida
                                                     privilegios (inclui AUSENCIA de DELETE).
  039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql    Remove as tabelas + permissoes. Protegido:
                                                     recusa se houver dados (fugapet.forcar_drop_039).

Sequencia: PREFLIGHT -> (revisar saida) -> PROPOSTA -> VALIDACAO.

------------------------------------------------------------
3. ESTADOS DO APONTAMENTO (o que EXISTE de fato)
------------------------------------------------------------
Estados PERSISTIDOS (os unicos gravados, e os unicos aceitos pelo CHECK):
  EM_ANDAMENTO, AGUARDANDO_FINALIZACAO, CONCLUIDA, CANCELADA

Estados CALCULADOS somente na tela (operacao da OP SEM apontamento) - NUNCA gravados:
  PENDENTE, BLOQUEADA, LIBERADA
ERRO nao e um estado persistido: falha de SAP na tela operacional mantem o apontamento EM_ANDAMENTO
(recuperavel por retomada), e a causa e exibida ao operador.

TRANSICOES IMPLEMENTADAS (as unicas que existem):
  (nenhum)               --inicio confirmado------> EM_ANDAMENTO
  EM_ANDAMENTO           --retomada--------------> EM_ANDAMENTO   (so evento; iniciado_em INALTERADO)
  EM_ANDAMENTO           --atividade concluida---> AGUARDANDO_FINALIZACAO
  AGUARDANDO_FINALIZACAO --termino---------------> CONCLUIDA
  EM_ANDAMENTO           --recusa/cancelamento---> CANCELADA

Fechar a tela operacional NAO conclui a operacao (o apontamento continua EM_ANDAMENTO e pode ser
RETOMADO lendo novamente o codigo de inicio). Erro/divergencia SAP NAO liberam o termino.
Operador que nega a confirmacao do inicio nao gera apontamento algum.

------------------------------------------------------------
4. RESTRICOES DE CONCORRENCIA/IDEMPOTENCIA
------------------------------------------------------------
  uq_apontamento_ativo_por_operacao      (numero_ordem, sequencia, operacao, suboperacao)
                                         WHERE status IN (EM_ANDAMENTO, AGUARDANDO_FINALIZACAO)
                                         -> impede DOIS apontamentos ativos da mesma operacao
                                            (dois usuarios / duas estacoes / inicio duplicado).
  uq_apontamento_idempotency_inicio      (idempotency_key)  -> codigo de inicio usado 2x: proibido.
  uq_apontamento_idempotency_termino     (idempotency_key_termino) WHERE NOT NULL
                                         -> codigo de termino usado 2x: proibido.
  ck_apontamento_termino_completo        CONCLUIDA exige terminado_em + usuario + codigo + chave.
  ck_apontamento_resultado_operacional   AGUARDANDO_FINALIZACAO **e** CONCLUIDA exigem
                                         resultado_operacional IS NOT NULL
                                         + resultado_operacional IN ('ConcluidoLocalmente','ConfirmadoSap')
                                         + concluido_operacional_em IS NOT NULL. Valores = nomes do enum
                                         ResultadoExecucaoProcessoApontamento (ToString()).
                                         -> ErroSap/DivergenciaSap/NaoConcluido NUNCA sustentam esses estados.

TRANSICAO ESTRITA DE TERMINO: o UPDATE de conclusao exige status = 'AGUARDANDO_FINALIZACAO'
(nao mais IN ('EM_ANDAMENTO','AGUARDANDO_FINALIZACAO')). O repository NAO conclui um EM_ANDAMENTO
direto; combinado ao check acima, CONCLUIDA sempre tem vinculo do processo e termino completo.
"Termino sem apontamento ativo" e impossivel: nao ha INSERT em CONCLUIDA.
  ck_apontamento_ordem_cronologica       terminado_em >= iniciado_em.
  uq_operacao_config_ativa               uma unica configuracao ATIVA por combinacao.

idempotency_key = "<FORMATO>|<codigo lido>" (ex.: OP_OPERACAO_EVENTO_V1|000001001710005001).

------------------------------------------------------------
5. PERMISSOES
------------------------------------------------------------
App (fugapet_dev_app obrigatório no PREFLIGHT, na PROPOSTA e na VALIDAÇÃO):
  operacao_producao_configuracao -> SELECT
  operacao_producao_apontamento  -> SELECT, INSERT, UPDATE
  operacao_producao_evento       -> SELECT, INSERT
  SEM DELETE em nenhuma delas (historico preservado; cancelamento e logico via status).
  USAGE/SELECT nas 3 sequences identity (resolvidas por pg_get_serial_sequence).

Matriz de acesso (idempotente). Colunas REAIS da tabela desenvolvimento.permissao:
  modulo_permissao / rotina_permissao / acao_permissao / descricao_permissao / situacao_permissao
  (a versao anterior deste pacote usava modulo/rotina/acao - ERRADO; corrigido.)

MVP: SOMENTE as 3 acoes com EFEITO FUNCIONAL REAL sao criadas.
  Modulo PROCESSO_PRODUCAO / Rotina CONTROLE_APONTAMENTOS (com descricao e situacao ativa):
    VISUALIZAR -> abre a tela / consulta a OP
    INICIAR    -> evento 01 (validado em ProcessoControleApontamentosServico, ANTES da confirmacao)
    FINALIZAR  -> evento 02 (idem)

NAO criadas nesta versao (nao ha fluxo que as consuma - permissao ativa sem efeito e falso controle):
  CONSULTAR_HISTORICO, CANCELAR, REABRIR, IGNORAR_SEQUENCIA.
  As constantes C# permanecem em PermissoesSistema.Acoes para evolucao futura, mas NAO sao
  funcionalidades entregues. Serao criadas no incremental que trouxer suas telas/fluxos.

Vinculo idempotente das 3 permissoes ao perfil 'Administrador' em perfil_permissao.
Os DEMAIS perfis NAO recebem automaticamente: exige decisao funcional documentada.

FALLBACK (documentado) - ControleApontamentosAutorizacaoServico:
  VISUALIZAR cai para LEITURA_PRODUCAO enquanto a rotina propria nao existir (mesmo padrao de Consumo
  e Semiacabado para ABRIR tela).
  INICIAR e FINALIZAR **nunca** usam o fallback: sem a rotina propria permanecem NEGADOS. Isso e seguro
  porque, sem o 039 aplicado, a estrutura tambem esta ausente (inicio/termino ja bloqueados); e, depois
  de aplicado, as acoes proprias passam a valer sem que o fallback conceda nada.

------------------------------------------------------------
6. LIMITACAO ENQUANTO O PACOTE NAO FOR APLICADO
------------------------------------------------------------
A tela ABRE e funciona em modo consulta:
  - o codigo e interpretado (parser V1);
  - a OP e consultada no SAP;
  - as operacoes sao exibidas no grid.
O inicio operacional fica BLOQUEADO com:
  "A configuracao da operacao ainda nao foi aplicada no banco DEV."
Nenhum mapeamento e inventado pela descricao para contornar a estrutura ausente.

------------------------------------------------------------
7. PENDENCIA IMPORTANTE (FORMATO DO CODIGO)
------------------------------------------------------------
O formato V1 (12 posicoes de OP + 4 de operacao + 2 de evento; 01=inicio, 02=termino) esta
validado SOMENTE para o processo analisado. A regra oficial ABAP (SmartForm/programa de impressao/
tabela Z, variacao por centro/tipo de ordem, demais eventos) ainda NAO foi confirmada. As definicoes
vivem em ConfiguracaoFormatoCodigoOperacao (classe unica) e a parametrizacao por banco esta prevista.
============================================================


------------------------------------------------------------
8. CORRECAO FINAL GAIA - 2026-07-16
------------------------------------------------------------
Correcoes incorporadas antes de qualquer execucao SQL:
  - ck_apontamento_resultado_operacional passou a exigir explicitamente resultado_operacional IS NOT NULL.
  - PREFLIGHT rejeita definicao antiga sem resultado_operacional IS NOT NULL e rejeita estados persistidos
    calculados/indevidos: PENDENTE, BLOQUEADA, LIBERADA e ERRO.
  - PREFLIGHT valida a FK evento -> apontamento via pg_constraint, exigindo confdeltype = 'r'
    (ON DELETE RESTRICT), tabela/coluna local e tabela/coluna referenciada corretas.
  - PREFLIGHT, PROPOSTA e VALIDACAO passam a exigir o papel fugapet_dev_app; a checagem de privilegios
    nao e mais ignorada quando o papel estiver ausente.
  - VALIDACAO isola o teste de idempotency_key_termino duplicada para alcançar unique_violation do indice
    uq_apontamento_idempotency_termino, e falha se cair em check_violation.
  - VALIDACAO isola o teste cronologico para confirmar especificamente ck_apontamento_ordem_cronologica.
  - ZIP canonico 039 recriado com exatamente os cinco arquivos do pacote.
  - ZIP DEV final deve ser gerado fora da arvore do projeto, sem ZIPs limpos aninhados, sem bin/obj/.git/.vs
    e sem configuracoes reais.
