# Entrada MP P5 R7 — candidato SOURCE ONLY, NÃO ATIVAR

Base R6: `6cdecd1e016bde3e66bfc8a4efa166b84516b6b1`.

## Proveniência

Pacotes originais em `C:\Users\TI\Downloads`, preservados:

| Pacote | SHA-256 |
| --- | --- |
| FUGAPET_ENTRADA_MP_P5_R5B_CONTRATO_CODEX_PREPARADO.zip | A5E498959742BE1D5DE0CD1EB49862A864502DB6F4FC1CE899D0FCBA72293802 |
| FUGAPET_ENTRADA_MP_P5_R6A_COMPLEMENTO_D3_CODEX_NAO_APLICAR.zip | 208CCB08F535CA181D8E86AEE16CBEC597411B2E764A9F0116FD0F0189F85F19 |
| FUGAPET_ENTRADA_MP_P5_R6A1_RECOVERY_COMPLEMENTO_CODEX_NAO_APLICAR.zip | 65A281BB746D3DFB09854A34B30CBDC0C1F166895C52ADB5294F7354BCD18C6E |

Os dois SELECTs incorporados são cópias byte a byte: D2LocalR5B.sql SHA 7E416642612BD36D946E7143C0812ED8CCE40F424B40DC7002215D5AD402D46D; D2RecuperacaoR6A1.sql SHA FB4ADF716850F797D11371986CBB3254DC764A89F6ABD8E95B14590BD6B616C6. Nenhuma DDL ou migration foi criada ou executada. O adaptador remove somente o invólucro PREPARE do D2 R5B ao construir o comando Npgsql, conforme instrução do próprio contrato.

## Limites deliberados

`BloqueioP5R7.MutacoesHomologadas` é sempre false, sem JSON/ENV/controle UI para habilitar. Mesmo uma HomologacaoP5 artificialmente completa não permite ao adaptador real abrir conexão de mutação. Todas as classes novas são internas e nenhum token é entregue à Form, ao controller ou ao provider público R6. Não há código novo para obter credenciais ou autenticar um backend: esse binding depende de aprovação futura.

O provider padrão R6 continua com suas fontes indisponíveis e tri-state bloqueado. D2/D3/recovery novos NÃO estão conectados a esse provider nesta etapa; conectá-los no processo GUI violaria o isolamento exigido pelos contratos. O orquestrador e o recovery são preparação do backend e os caminhos positivos só são exercitados com interfaces falsas em testes offline.

HomologacaoP5 não comprova segurança/autoridade SAP por si só. Não aceitar flags vindas de UI/operador como prova. Um backend autenticado deve produzir e validar a evidência antes de qualquer integração operacional. Nenhum papel, mandante, TTL ou lease foi homologado pelo código. Tempos usados nos testes são exclusivamente massa sintética. Os valores 60/90 s dos templates R6A não foram adotados como política C#.

## D2

`EntradaMpP5D2Repositorio` usa parâmetros Text/Text/Bigint, uma transação REPEATABLE READ + SET TRANSACTION READ ONLY e rollback ao final, lendo o histórico R5B e a VIEW segura R6A.1 no mesmo snapshot. Não lê diretamente tabelas P5 nem tokens. Exceções viram código fixo sem detalhes/segredos. Cancelamento propaga.

O mapeamento é conservador. Documento local exige reconciliação, não vira ausência. Sem guarda, status desconhecido, histórico incerto, outro lançamento, cardinalidade/cache ambíguos ou consulta indisponível bloqueiam. `SemRegistro` é apenas classificação local; nunca prova negativa SAP nem autorização P5=FALSE.

Lacuna material do contrato original: o CTE local R5B contém `codigo_sap_pedido_compra_item`, mas seu SELECT final não projeta a coluna. Não é possível comprovar a FK de um item preparatório no C# usando somente essa projeção. O adaptador bloqueia esse caso (não supõe FK NULL ou compatível). Outra lacuna: a query segura retorna bloqueio quando não existe guarda; o bootstrap da primeira guarda deve ser adjudicado junto à reserva transacional antes de qualquer liberação. Nenhum SQL original foi reconstruído para contornar essas limitações.

## D3 e recovery

`EntradaMpP5BackendRepositorio` prepara exclusivamente as funções R6A de reservar/marcar-envio/resultado e os três wrappers R6A.1. Cada chamada tem transação explícita e só retorna depois do COMMIT. Resultado ambíguo do COMMIT bloqueia; não há retry. A função legada `fn_entrada_mp_p5_reconciliar` não é chamada pelo C#.

`CoordenadorP5R7` prepara prova confiável + D2 + reserva + COMMIT ENVIO_INICIADO antes de um único dispatch injetado. Nunca mantém transação aberta durante o dispatch. Timeout, exceção e HTTP não conclusivo persistem resultado indeterminado; falha da persistência conserva a necessidade de reconciliação. Nenhum callback é ligado ao HTTP real.

`RecoveryP5R7` não possui dispatcher. Usa token separado/geração dos wrappers, exige prova completa, escopo idêntico e horário estritamente posterior ao início de envio. Reconsulta a VIEW depois do COMMIT antes de declarar persistência comprovada. Cancelamento só antes da fronteira HTTP. O servidor continua sendo a autoridade final de fencing e lease.

## Cobertura de POST/outbox

Fluxo observado: ProcessoEntradaProdutoForm → EntradaProdutoController.EnviarPesoEntradaParaSapHomologacaoAsync → ValidarDuplicidadeRecebimentoItemPedidoAsync → reserva legada por lançamento → IntegracaoEntradaSapServico.CriarDocumentoMaterialEntradaAsync → MaterialDocumentSapGovernadoServico → MaterialDocumentSapServico → MaterialDocumentSapApiClient → HTTP.

O controller faz P5 nas posições com ZZ1PesoEntradaMmi, mas acesso direto ao writer/cliente contornava esse seam. R7 bloqueia payload com qualquer um dos dois campos ZZ1 preenchido no writer (antes de capability) e no cliente (antes de CSRF/HTTP), independentemente de ENV write=true. Isso mantém P5 fechado também para chamadas diretas/outbox que usem esses pontos. HIBE e PA/sem referência P5 permanecem no caminho anterior. Não é prova de classificação de material: payload sem ZZ1 não pode ser reconhecido como ROH apenas pelo tipo atual do request; qualquer futura mudança de classificação deve ser homologada, não inferida.

Busca focal em AcessoDados/Servicos/Controle não encontrou dispatcher/outbox operacional de envio Entrada MP. Há somente proteção de exclusão em ExclusaoPesagemRepositorio.ObterGuardCountsAsync, que chama fn_entrada_produto_sap_guard_counts por correlation. A chave legada do writer é pedido/lançamento, não pedido/item. Não reutilizar essa chave como guarda D3, nem inventar chave de outbox. Jobs externos/SQL do banco não foram auditados neste gate sem acesso DB.

## Pendências para promoção

- Veredito Hermes R6 e revisão independente de segurança R5B/R6A/R6A.1.
- Roles reais e isolamento do backend confiável, ACL/owner e funções implantadas atomicamente em gate próprio.
- Política temporal homologada; prova SAP autoritativa, cobertura de movimentos/estornos externos e idempotência do destino.
- Adjudicação das projeções D2/FK e bootstrap sem guarda, sem modificar unilateralmente scripts Gaia.
- Binding backend/provider/HTTP, cobertura multi-item do POST (um documento pode conter vários itens P5) e persistência atômica com o legado. O coordenador preparado trata um item por operação; NÃO dividir um POST legado em novos POSTs por item.
- Teste real de locks, constraints, permissões e concorrência PostgreSQL após autorização; simulação offline não substitui essas provas.

Sem essas condições, P5 permanece NULL/BLOCK e não há promoção, DB, SAP, deploy ou push.

## Evidência offline R7

Baseline focal R5/R6: 40/40. Dois testes novos de bypass reproduziram falhas antes dos bloqueios. Revisão independente encontrou validade da prova na fronteira HTTP e reconsulta de recovery com COMMIT ambíguo; três testes reproduziram essas falhas antes da correção. O coordenador agora revalida a prova após D2/reserva e após COMMIT, antes do dispatch; o recovery reconsulta uma vez após fechamento ambíguo, sem repetir mutação.

Execução final Release: 146/146 PASS, dos quais 51 casos R7, 40 casos P5 R5/R6 e 55 de compatibilidade. Sem skipped. Build MAIN e testes com zero erros; warnings históricos permanecem, sem warning novo do R7. Isso NÃO constitui regressão completa nem prova PostgreSQL/SAP real.

TRX/LOG: `C:\Users\TI\Documents\FugaPET\EVIDENCE\P5_R7\r7_final_reviewed.trx` e `r7_final_reviewed.log`. Baseline, ciclos negativos e logs de build também foram preservados nessa pasta.
