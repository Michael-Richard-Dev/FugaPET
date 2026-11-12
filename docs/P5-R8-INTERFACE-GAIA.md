# R8 — interfaces necessárias à Gaia (proposta, NÃO SQL homologado)

Não é migration. Não há função nova executável neste documento nem autorização
de DDL. Os nomes abaixo são nomes de interface C#, não funções existentes.

## Contrato tipado que já existe no R8

`IContratoLegadoP5R8.ValidarEscopoPersistidoAsync(NpgsqlConnection,
NpgsqlTransaction, EscopoP5, CancellationToken)` retorna classificação durável
ou ausência bloqueante. `ExecutarNaTransacaoAsync` recebe a MESMA conexão/tx e
PedidoLegadoP5R8, retornando recibo de versão/cobertura/documento.
Não há implementação concreta enquanto a Gaia não aprovar a API privilegiada.

## Parâmetros propostos para API PostgreSQL privilegiada futura

Para as três mutações legadas, identidade comum obrigatória, nesta ordem:

| Posição | Nome conceitual | Tipo PostgreSQL | Origem |
| --- | --- | --- | --- |
| 1 | numero_pedido | text | ChaveP5.Pedido, dígitos SAP |
| 2 | numero_item | text | ChaveP5.Item, cinco dígitos |
| 3 | codigo_lancamento | bigint | EscopoP5.Lancamento, positivo |
| 4 | codigo_tentativa | bigint | ClaimP5.Tentativa, positivo |
| 5 | claim_token | uuid | ClaimP5.Token, secreto |
| 6 | versao_guarda_esperada | bigint | ClaimP5.VersaoGuarda; NÃO recovery_generation |
| 7 | correlation_id | uuid | ClaimP5.Correlacao imutável |
| 8 | material | text | EscopoP5.Material |
| 9 | codigo_usuario | bigint | ator capturado explicitamente, positivo |

Classificação NÃO pode ser aceita como boolean/string livre enviada pelo APP.
A função deve resolver e verificar a evidência persistida autoritativa associada
à PK de item SAP, material, PO/item e lançamento. Falta à Gaia definir sua fonte,
armazenamento inviolável e vínculo. Não foi inventada tabela/coluna para isso.

### ReservarLegadoP5Async

Identidade comum + `status_anterior text`, domínio FINALIZADO_LOCAL/ERRO_SAP.
Retorno necessário: nova versão bigint, tentativa/correlação, estado RESERVADO,
status legado ENVIADO_SAP, quantidade de itens associados e cobertos bigint.
Validações sob lock: classificação ROH positiva, um único item total, FK exata,
claim atual/não expirado, versão exata, elegibilidade remota e ausência de envio.
Pós-condição: carimbo P5 e cabeçalho legado na mesma transação do chamador.
Nenhum COMMIT interno. Zero/múltiplos matches, versão alterada ou status divergente
rejeitam toda a operação, sem fallback a maior PK/data.

### CompensarLegadoPrePostAsync

Identidade comum + `status_anterior text`, `motivo_codigo text` de domínio fixo.
Retorno: nova versão bigint, estado CANCELADO_SEM_ENVIO e legado restaurado, ou
resultado bloqueante RESERVA_LEGADA_INCERTA sem elegibilidade para dispatch.
Exigir RESERVADO e ausência tanto de envio_iniciado_em quanto de selo de fronteira.
Verificar titularidade e versão sob os mesmos locks. UPDATE legado e cancelamento
P5 devem ser atômicos. Não aceitar GUC livre como licença de compensação.

### ConfirmarResultadoP5ELegadoAsync

Identidade comum + `http_status integer`, `documento_material text`,
`exercicio text`, `documento_item text`, `resultado text` de domínio aprovado.
Retorno: nova versão bigint, estado final, documento/ano/item, contagens associadas
e cobertas, recibo durável verificável da conclusão conjunta P5+legado.
Exigir selo de fronteira, claim/tentativa/versão atuais e documento correspondente.
ROH multi-item/misto rejeitado antes da mutação. Exatamente 1/1 nesta fase.
Persistir documento_material_item e usuário do item/cabeçalho; tudo ou nada.
Nenhum sucesso por EXISTS de uma guarda. Documento divergente bloqueia.

### ValidarAntesDePost101

Identidade comum (ator quando aplicável), versão atualizada retornada pela etapa
anterior e referência da prova SAP autoritativa. Contrato deverá aceitar e comparar
a versão atomically sob lock, não confiar numa view lida antes do mutador.
Retorno: selo novo, tentativa, versão e autorização de UM dispatch; selo já emitido
significa zero novas autorizações. COMMIT encerra a transação antes do HTTP.
Perda de resposta de COMMIT: nunca executar/reexecutar POST pela leitura de estado.

### RegistrarIncertezaEReconciliarAsync

Identidade explícita comum; recuperação acrescenta `recovery_token uuid` e
`recovery_generation bigint` separados. Prova SAP: referência/fonte text,
obtida_em/valida_ate timestamptz, escopo PO/item, contagens de movimentos/estornos
integer e documento quando presente, conforme funções R6E.1 existentes.
Não reutilizar claim como recovery token. Evidência deve ser posterior ao envio.
Resultados tardios vão ao append-only existente, nunca reabrem POST.
Falta contrato de sincronização privilegiada do resultado recovery com legado.

## Ordem de locks e transações

Reserva/compensação/resultado: guarda por PO/item → tentativa exata → cabeçalho
legado → único conjunto de itens. Classificação e versão validadas sob locks.
Não escrever cabeçalho antes de bloquear guarda. API privilegiada recebe transação
do executor; não abre segunda transação nem confirma parcialmente.
Marcar ENVIO_INICIADO e COMMIT; confirmar fronteira em transação nova e COMMIT;
só então transporte. Não manter transação/lock aberto durante HTTP.
Multi-chave requer protocolo futuro com ordem global, não ORDER BY atualizado_em.

## Contratos R6E.1 efetivamente usados pelo adaptador preparado

| Função homologada estaticamente | Parâmetros posicionais | Retorno tratado |
| --- | --- | --- |
| fn_entrada_mp_p5_reservar | text,text,bigint,text,text,timestamptz | tentativa_id,claim,correlacao,estado_resultado=RESERVADO |
| fn_entrada_mp_p5_marcar_envio | bigint,uuid,uuid | ENVIO_INICIADO_COMMIT_E_CONFIRMAR_FRONTEIRA_ANTES_DO_HTTP |
| fn_entrada_mp_p5_confirmar_fronteira | bigint,uuid,uuid | FRONTEIRA_CONFIRMADA_UM_UNICO_POST_AUTORIZADO; JA_CONFIRMADA nunca habilita novo POST |
| fn_entrada_mp_p5_cancelar_pre_post | bigint,uuid,uuid,text | CANCELADO_SEM_ENVIO_REQUIRES_NEW_SAP_PROOF_FOR_RETRY; resultado diferente não libera |
| fn_entrada_mp_p5_resultado_http | bigint,uuid,uuid,text,integer,text,text,text,text | CONFIRMADO_SAP ou RESULTADO_INDETERMINADO; variante FORCADO mantém bloqueio |
| fn_entrada_mp_p5_registrar_retorno_tardio | bigint,uuid,uuid,text,text,text,integer,text,text,text,text | evidência append-only; sem confirmação legada |

Nenhuma dessas assinaturas existentes recebe a versão esperada. Não estender
assinatura localmente nem alegar fencing atômico por leitura seguida de chamada.
Recovery R6E.1 permanece preparado na baseline, mas R8 o bloqueia até a API conjunta.

## D2 e segurança

Projetar `codigo_sap_pedido_compra_item bigint` da FK LOCAL no SELECT final R5B,
preservando todas as demais colunas/semânticas, e atestar sua correspondência
com o único registro de cache. Não produzir FK copiando cache.sap_item_id.
Bootstrap sem guarda precisa de decisão conjunta D2+fn_reservar sob lock e prova
SAP, não de retorno permissivo em D2.
Roles reais PROVIDER/RECOVERY, grants mínimos, fonte SAP, TTL/lease/quarentena,
política de estornos e prova de runtime PostgreSQL permanecem pendentes.
