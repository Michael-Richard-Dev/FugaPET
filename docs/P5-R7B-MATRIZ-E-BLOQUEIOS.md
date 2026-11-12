# R7B — matriz focal sobre b64dc00

Gate: FUGAPET-ENTRADA-MP-P5-R7B-TARGETED-SOURCE-FIX.
Base exata: b64dc00fc804e1964016ded95fde92170bc0d29e.
Branch: codex/entrada-mp-p5-r7b-targeted.
Worktree: C:\Users\TI\Documents\FugaPET\.worktrees\entrada-mp-p5-r7b-targeted.

A lista R7-01 a R7-10 do handoff HERA foi confrontada com b64dc00, não presumida a partir
da revisão antiga aac9726/dc999a5. REVIEW_SOURCE foi fornecido como identificador
CLAUDE_R7_INDEPENDENT_SOURCE_REVIEW, sem um novo arquivo local; esta matriz usa os dez
achados transcritos integralmente no pedido e o código da base.

| Achado | Presente em b64dc00 | Já corrigido na base | Correção R7B | Dependência |
| --- | --- | --- | --- | --- |
| R7-01 | Sim: writer/cliente inferiam bloqueio por ZZ1 | Preparador já consultava ProductType | Classificação interna vinculada por referência ao item imutável criado após consulta A_Product. ROH bloqueia mesmo sem ZZ1; PO sem evidência/tipo conhecido bloqueia. HIBE confiável segue legado. Clone não herda evidência. | Fonte SAP/autorização operacional e QA ainda necessários; classificação não persistida nem inferida do payload. |
| R7-02 | Sim: bloqueio final do writer podia ocorrer depois da reserva | b64dc00 já centralizou compensação R3B | Preflight da mesma política do writer antes da reserva legada. Indicador tri-state PostTentado: false comprovado compensa status anterior, sem ERRO_SAP; null é desconhecido. Falha remota sem resposta/5xx preserva reserva e exige reconciliação. | Reserva conjunta P5+legado e mutações D3 continuam desabilitadas. |
| R7-03 | Sim: D2 exige guarda antes da primeira reserva | Ausência já bloqueava de forma segura | Nenhuma liberação por ausência. Circularidade não foi contornada no C#. | R6B deve definir aquisição inicial sob lock e prova autoritativa; exige contrato/SQL aprovado. |
| R7-04 | Parcial: coluna ausente/conflitante bloqueava, valor NULL passava | Validação de ausência, cardinalidade e divergência já existia | FK agora deve ser bigint positivo, presente e igual ao ID do único item de cache; material local não vazio. | Projeção da FK ainda falta no SELECT final R5B. Nenhum SQL alterado; D2 real permanece bloqueado nesse caso. |
| R7-05 | Parcial: marcação/resultado com commit incerto não reconsultavam | Recovery já reconsultava; b64dc00 exigia confirmação durável e não compensava fronteira ambígua | Reconsulta pelo mesmo escopo/tentativa/correlação após marcação ou resultado com resposta perdida. Reserva com identidade já obtida do reader conserva claim em exceção interna redigida e reconsulta. Nunca faz novo dispatch. | Reserva sem identidade retornada permanece indeterminada; correlação antecipada e contrato durável definitivo dependem de R6B. |
| R7-06 | Dependência operacional | b64dc00 já condicionava recovery a RecoveryR6BAprovado=false; D2 e backend têm fábricas próprias | Mantido fail-closed; nenhum papel, senha ou credencial ativado. Não há novo ponto de composição de credenciais. | Separação/atestação de roles provider/backend/recovery exige Gaia/segurança; adapter mutável segue desativado. |
| R7-07 | Parcial: multi-item/misto bloqueado, mas a identificação dependia de ZZ1 | Closure dc999a5 já cobria ROH/misto | Bloqueio também para ROH/misto sem customizados por classificação confiável, em preflight e fronteiras reais. Não divide documento em vários POSTs. | Protocolo multi-chave atômico continua pendente. |
| R7-08 | Sim: categoria confirmada era sempre Pendente | Motor puro R5 já distinguia Confirmado e Pendente | D2 devolve Confirmado quando há status CONFIRMADO_SAP na categoria de recebimento; documento sem confirmação continua Pendente. Ambos impedem envio. | Sem promoção de cache a prova negativa e sem alteração SQL. |
| R7-09 | Sim: backend usava DateTimeOffset.UtcNow | Coordenador/recovery já tinham TimeProvider | Backend recebe TimeProvider opcional e usa GetUtcNow na validação de reserva/recovery. Teste fixa 2040, sem relógio global. | Composição futura deve compartilhar a mesma instância de relógio entre componentes. |
| R7-10 | Parcial: legado real e SQL proposto têm vocabulários incompatíveis | C# já usava FINALIZADO_LOCAL/ERRO_SAP, ENVIADO_SAP e CONFIRMADO_SAP | Estados persistidos preservados, sem novos literais de banco. Abort comprovadamente pré-POST restaura exatamente o status consumido; incerteza não vira rollback/ERRO_SAP. | Gaia deve alinhar a elegibilidade e a semântica de ERRO_SAP no contrato R6B; nenhum SQL homologado/proposto modificado. |

## Identidade e fronteiras de envio

O preparador usa a consulta Product Master já existente; exige Consultado e código do produto
igual ao material da posição para registrar evidência interna. ConditionalWeakTable vincula
a evidência à referência exata do record, sem propriedade pública ou serializada. Alterar
material/pedido por with cria outro record, sem evidência, que bloqueia. Writer e cliente
copiam a lista de itens antes de avaliar/serializar. Não há flag pública para declarar HIBE.
Os tipos conhecidos VERP/HALB também preservam o fluxo legado existente. ProductType
desconhecido em Entrada/PO nunca é presumido HIBE.

Itens por ManufacturingOrder continuam nos processos PA/semiacabado existentes; não são
reclassificados como Entrada por PO. Customizados ZZ1 nessa rota são rejeitados como
payload incoerente, não usados como prova de ProductType. PO/item presentes exigem a
evidência do preparador, inclusive em chamadas diretas ao cliente ou ao writer.

Fluxo real de Entrada: Form → controller → integração → serviço governado → writer →
cliente → SendAsync(post). Preflight ocorre antes da reserva; writer e cliente reaplicam
a política independentemente. O teste double de Material Document dos testes históricos
R2/R3/R3B explicita seu preflight sintético para exercitar isoladamente essas validações;
esse double não faz HTTP. Os novos testes do controller usam o writer real e verificam
zero reservas, dispatches e alterações de status para ROH/desconhecido. Não foi introduzido
override permissivo em implementação produtiva nem ligada a coordenação D3 ao HTTP.

PostTentado=false é evidência explícita de bloqueio anterior à tentativa de POST. O cliente
marca true imediatamente antes de SendAsync(post); ausência de marcação é UNKNOWN, não false.
Falhas antes de invocar o cliente, de configuração e de governança marcam false.
Timeout/exceção cujo limite não pode ser comprovado mantém a reserva e exige reconciliação.
Nenhum retry automático foi adicionado. Confirmar commit por leitura não autoriza novo POST.

## Bloqueios de ativação

- MutacoesHomologadas continua false; ResultadoLegadoAtomicoDisponivel,
  ReservaLegadaCoordenadaDisponivel e RecoveryR6BAprovado permanecem false no backend real.
- R6B, bootstrap sob lock, correlação antecipada em reserva sem retorno, grant/roles,
  transação conjunta P5+legado, prova SAP e protocolo multi-chave continuam pendentes.
- SQL R5B/R6A/R6A.1 e baseline foram preservados byte a byte. Nenhuma migration, DDL,
  DML, chamada SAP real, deploy, promoção ou push foi executado.
- Correlação/código de tentativa permanecem internos; exceção de commit redige ToString.
  Nenhuma credencial foi adicionada. Configurações operacionais e binários não integram o ZIP.
- Pronto para QA independente de fonte/evidência; não pronto para ativação operacional.

## Validação e arquivos

Criados: ClassificacaoMaterialDocumentP5.cs, EntradaMpP5R7BTests.cs e este relatório.
Alterados: contratos/coordenador P5, adapters backend/D2, preparador/controller de Entrada,
contrato/integração/governança/writer/cliente Material Document, DTO de resultado e seus
testes impactados. O inventário exato é o diff do commit e changed-files.txt no ZIP.
Nenhum Designer foi alterado. Os testes exercitam falhas com HTTP e banco substituídos
por fixtures; nenhum teste de integração com rede/banco foi selecionado.

O build Release e o TRX focal verdadeiros ficam no pacote QA, com hash e commit de origem.
Verificação final: 301 aprovados, zero falhas e zero ignorados; build Release com zero
erros e dez avisos preexistentes de nulabilidade/analyzers. TRX: r7b-verified.trx.
Não foi executada suíte global. A configuração NuGet de restore remove todas as fontes
remotas e utiliza somente cache local.

## Teste manual seguro para Hermes

1. Conferir parent b64dc00 e o manifesto SHA256; abrir o TRX no Test Explorer.
2. Executar R7B_PoliticaRealPrecedeReservaESomenteHibeSegue: ROH, vazio e desconhecido
   devem gerar zero reservas e zero alterações de status; HIBE usa dispatch falso.
3. Executar PedidoSemClassificacaoNaoDespachaMesmoSemZz1,
   TipoOuIdentidadeAusenteBloqueiaMesmoSemCustomizados e MultiItemRohOuMistoSemZz1NaoDespacha:
   zero HTTP para desconhecido, ROH sem ZZ1, clone sem evidência e documentos mistos.
4. Executar R7B_WriterBloqueadoAntesHttpCompensaSemErroSap e os R3B:
   status anterior preservado, compensação sem token cancelado e zero POST real.
5. Executar CommitIncertoReconsultaCorrelacaoSemRepetirDispatch e
   ResultadoCommitadoComRespostaPerdidaReconsultaSemReenvio: reconsulta da mesma identidade,
   sem liberar reserva nem repetir envio.
6. Executar FkNulaNaoEquivaleAIdentidadeConfirmada, ConfirmadoLocalDistingueDocumentoSemConfirmacao
   e BackendUsaRelogioInjetadoNaReservaERecovery.
7. Não executar aplicativo com configurações operacionais para validar este pacote.
