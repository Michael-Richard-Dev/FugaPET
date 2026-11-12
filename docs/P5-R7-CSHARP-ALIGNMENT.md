# Adequação focal C# R7 — SOURCE ONLY

Gate: FUGAPET-ENTRADA-MP-P5-R7-CSHARP-ALIGNMENT.
Base: dc999a5182f363f45242f2d7ab9b3141f75a3697.
Branch: codex/entrada-mp-p5-r7-csharp-alignment.
Worktree: C:\Users\TI\Documents\FugaPET\.worktrees\entrada-mp-p5-r7-csharp-alignment.
Revisão: C:\Users\TI\Downloads\CLAUDE_FUGAPET_P5_D3_REVIEW_CD01_CD10.txt.

## Correções e limites

CD-1: IBackendP5 passa a exigir ReservaLegadaCoordenadaDisponivel, false por padrão.
ReservarAsync é o único ponto de aquisição do coordenador P5: um futuro backend aprovado
deve adquirir exclusividade por PO/item e reserva legada na mesma transação, preservando
o status anterior efetivamente consumido. Não encadear uma segunda reserva legada no controller.
CancelarReservaPreHttpCommitAsync deve compensar ambas na mesma transação, com o mesmo
escopo/tentativa/token/correlação, somente antes da fronteira, preservando transições concorrentes.
Seu retorno true significa commit comprovado. Nenhum desses contratos ganhou implementação
SQL neste incremento. O adaptador real herda false e continua bloqueado.
O vocabulário FINALIZADO_LOCAL/ERRO_SAP do legado foi preservado; a incompatibilidade SQL
apontada por Claude permanece para Gaia.

A compensação C# usa CancellationToken.None após cancelamento anterior à solicitação da
fronteira. Falha, exceção ou false da compensação conserva estado incerto; não declara
liberação. Após solicitar MarcarEnvioCommitAsync, inclusive se houver exceção ou commit
ambíguo, não cancela a reserva. No controller legado, recarga pós-reserva, snapshot divergente
e bloqueio da leitura fresca agora usam a mesma compensação R3B; cancelamento solicitado
durante a recarga continua sendo cancelamento mesmo se a compensação falhar.

CD-7: MarcarEnvioCommitAsync sozinho deixou de autorizar dispatch. É obrigatória
ConfirmarEnvioDuravelAsync(claim) == EnvioIniciado. O futuro adaptador deve fazer uma
leitura independente, após encerramento da transação de marcação, vinculada à mesma
tentativa, correlação e escopo e com validação da identidade autorizada. Nunca devolver
o estado por inferência da string da função ou de um objeto em memória. O método padrão
retorna Bloqueado, sem abrir conexão. Commit ou leitura não comprovados impedem dispatch.
O guard SQL de txid continua pendente; a preparação C# não prova aprovação de D3.

O callback de dispatch agora recebe ClaimP5, preservando escopo, tentativa, correlação e
token interno da aquisição original. ClaimP5 permanece internal com ToString redigido.
Não transportar esse claim em JSON SAP, mensagens, payload controlado ou logs. A ligação
ao writer real continua desativada: esta mudança prepara a identidade no boundary interno
e não cria uma API pública nem uma autorização para ultrapassar o bloqueio ROH/ZZ1.

CD-2: recovery exige RecoveryR6BAprovado, false por padrão. O adaptador real não o habilita.
Os testes de protocolo anteriores usam apenas backend sintético que declara essa capacidade.
Limpeza/invalidação de lease, geração e janela de acomodação HTTP exigem R6B aprovado;
nenhuma regra temporal alternativa foi inventada. Uma prova negativa isolada continua
insuficiente para habilitar recovery operacional.

Falhas HTTP, timeout e falha de persistência continuam sem loop de retry. Depois da fronteira,
resultado desconhecido exige reconciliação; não compensa reserva. Os testes existentes exercitam
segunda invocação bloqueada e confirmação remota sem persistência local.

## Cobertura de caminhos

Entrada MP: ProcessoEntradaProdutoForm → EntradaProdutoController →
IntegracaoEntradaSapServico → MaterialDocumentSapGovernadoServico →
MaterialDocumentSapServico → MaterialDocumentSapApiClient → SendAsync(post).
Writer e cliente mantêm os bloqueios ROH/ZZ1, inclusive chamadas diretas e documentos mistos.
O coordenador P5 permanece preparatório, sem callback conectado ao HTTP real.

O mesmo cliente atende ProdutoAcabadoMovimentoAdapters e o writer atende SemiAcabadoServico.
Esses processos não receberam alterações. HIBE puro mantém o fluxo legado e a fixture
de HTTP falso da closure. Ausência de ZZ1 no DTO não é prova autenticada de HIBE:
classificação autenticada até o writer continua requisito da integração futura.
Não foi localizado dispatcher de outbox da Entrada MP no código C# auditado.
Este inventário não certifica jobs externos ao repositório.

## Arquivos

Alterados: Controle/Processo/EntradaProdutoController.cs,
Servicos/Entrada/P5/ContratosP5R7.cs, Servicos/Entrada/P5/CoordenadorP5R7.cs,
FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R7BackendTests.cs e
FugaPET_Dev.Tests/IntegracaoSap/EnvioControladoSapEntradaC12Tests.cs.
Criado: este relatório. Nenhum SQL, baseline, Designer ou configuração operacional alterado.

## Validação offline e teste manual

Na nova worktree, executar:

    dotnet test FugaPET_Dev.Tests/FugaPET_Dev.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EntradaMpP5R7|FullyQualifiedName~EnvioControladoSapEntradaC12Tests"

A restauração utilizou exclusivamente a configuração NuGet offline existente e cache local.
Os testes substituem banco e SAP por fixtures; não executar aplicativo com configuração real.
TRX ficam em TestResults/Alignment, ignorados pelo Git.

Resultado final: build Release do projeto de testes concluído, zero erros e três avisos
preexistentes nos testes de Produto Acabado. Execução focal final: 159 aprovados,
zero falhas e zero ignorados (alignment-verified.trx). Não foi executada suíte global.

Roteiro manual seguro para o QA no Test Explorer:

1. Executar ReservaSemCoordenacaoLegadaNaoMutaNemDespacha: zero aquisição e zero dispatch.
2. Executar SemConfirmacaoDuravelNaoDespachaNemCompensaCommitPossivel: ausência/estado
   diferente de ENVIO_INICIADO bloqueia ambas as invocações e não libera commit potencial.
3. Executar CancelamentoAntesDeMarcarEnvioCompensaComTokenIndependente e
   RevalidacaoCanceladaCompensaSemMascararCancelamento: compensação sem token cancelado,
   uma tentativa, zero dispatch, cancelamento preservado.
4. Executar DispatchRecebeIdentidadeDaMesmaTentativaAposConfirmacaoDuravel: identidade
   original chega ao callback somente após confirmação.
5. Executar RecoverySemR6BNaoLiberaResultadoNegativo, TimeoutOu500NaoPermiteSegundoEnvio
   e FalhaPersistenciaDepoisDoHttpMantemEnvioBloqueante: nenhuma liberação/repetição indevida.
6. Executar a closure HIBE/ROH: HIBE usa apenas HttpMessageHandler falso; ROH/misto não chama HTTP.

Pronto para QA independente de fonte C#. Ativação continua bloqueada por R6B, atomicidade
real P5+legado, confirmação durável implementada, classificação autenticada e contrato
multi-item. Sem migration, DML, SAP real, deploy, promoção ou push. Nenhuma credencial adicionada.
