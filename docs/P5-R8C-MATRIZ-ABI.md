# R8C — matriz C# × PostgreSQL R8B.1

GATE=FUGAPET-ENTRADA-MP-P5-R8C-CSHARP-SQL-ABI-CONVERGENCE

Base C#: `5eaffba27d5437814ba8399ea70dc0d679b1ee0b`.
Pacote SQL candidato, não instalado/executado:
`FUGAPET_ENTRADA_MP_P5_R8B1_SQL_V_ATIVOS_FIX_NAO_APLICAR.zip`.
SHA256: `9D12BCD1000D7487582717B08B401BAC8966D8B779D9E3B995EF9B2DD5FB1CC8`.
SQL interno `02_R8B_INCREMENTAL_ATOMICO_NAO_APLICAR.sql`, SHA256:
`21EEC3345F72069F719B820F81A62D802B1B2D2682BD1E85A2364FB113F2F002`.
Leitura estática do corpo completo das quatro funções; SQL Gaia não alterado.

## Resultado e separação de responsabilidades

Os comandos preparados convergem com as **assinaturas de entrada reais** do
candidato (15/8/4/8 parâmetros). Retornos são representados como escalares ou
projeções parciais. **Não existe conversão permissiva para os recibos completos
R8B**, implementação de `IContratoEvidenciaProdutoSapGaiaR8B`, executor SQL novo,
composição no controller/writer ou backend corporativo inventado.

`NpgsqlCommand` é construído sem Connection/Transaction e nunca executado neste
incremento. `ISessaoAbiProdutoSapR8C` exige conexão/transação existentes, mas não
tem implementação. O futuro backend deve atribuir a mesma conexão/tx a todos os
comandos da coordenação; os contratos/executor R8 permanecem intactos e bloqueados.
Leitura de retorno via DataTableReader nos testes não é leitura PostgreSQL.

## A — fn_p5_r8b_ingestar_producttype

Fonte: SQL candidato, linhas 215–283; retorno **bigint**, ID da evidência.
Não há parâmetros com DEFAULT ou função STRICT. Nulabilidade efetiva vem do
corpo/constraints, não do simples tipo na assinatura.

| # | Parâmetro SQL / tipo | C# / origem | Nulabilidade e decisão |
| --- | --- | --- | --- |
| 1 | p_pedido text | Identidade.Escopo.Chave.Pedido | Obrigatório, dígitos preservados, não integer |
| 2 | p_item text | Identidade.Escopo.Chave.Item | Obrigatório, cinco dígitos canônicos |
| 3 | p_lancamento bigint | Identidade.Escopo.Lancamento | Positivo, vínculo empresarial verificado no servidor |
| 4 | p_item_local bigint | Identidade.CodigoItemLocal | Positivo, não inferido do cache |
| 5 | p_fk_sap_item bigint | Identidade.CodigoSapPedidoCompraItem | FK LOCAL positiva; nula/divergente bloqueia |
| 6 | p_material text | Escopo.Material | Obrigatório; igualdade com Product |
| 7 | p_product text | Evidencia.Campos.Product | Texto original, igualdade exata; não normalizado |
| 8 | p_producttype text | Campos.ProductType | Resposta SAP pretendida, ROH/HIBE explícitos; não prova de autenticação |
| 9 | p_lastchange_datetime_raw text | LastChangeDateTimeOriginal | Nullable se offset não comprovado; raw preservado |
| 10 | p_lastchange_datetime_utc timestamptz | DateTime UTC convertido somente com offset atestado | Nullable; obrigatoriamente null se p_offset_comprovado=false |
| 11 | p_offset_comprovado boolean | Pedido.OffsetComprovadoPeloBackend | Obrigatório explícito; marcador lexical sozinho não atesta a origem |
| 12 | p_lastchange_date date | DateOnly de LastChangeDateOriginal | Nullable; só conversão civil ISO yyyy-MM-dd inequívoca |
| 13 | p_lastchanged_by_user text | Campos.LastChangedByUser | Nullable; metadado SAP, não usuário local/SQL/backend |
| 14 | p_referencia_coleta text | Pedido.ReferenciaColetaBackend | Obrigatória, fornecida pelo futuro backend; não criada de UUID/ETag/hash |
| 15 | p_correlation_coleta uuid | Contexto.CorrelacaoColetaBackend | Obrigatório e não vazio; correlação interna do backend, não SAP |

Tipos Npgsql: Text, Bigint, TimestampTz, Boolean, Date e Uuid. Nulos opcionais
usam DBNull com tipo explícito. `ComandosEvidenciaProdutoSapR8C.Registrar` recebe
um wrapper porque EvidenciaProdutoSapR8B sozinha NÃO contém a identidade local,
referência opaca de coleta ou atestação do offset. Nada disso recebe fallback.

Metadados sem parâmetro nesta ABI:

- Ambiente/mandante: SQL usa `v_pol.sap_sistema/sap_mandante`, não o contexto C#.
  Não declarar o ambiente confirmado pelo simples nome da sessão.
- Origem técnica: constraint/default fixo do SQL. C# exige origem pretendida,
  mas isso é validação de forma, não prova de GET.
- IdentidadeBackend: SQL persiste `session_user`; NÃO é identidade/autenticação
  SAP nem atesta a string do desktop.
- ConsultaIniciadaEm/ConsultaConcluidaEm: não transmitidas. O SQL preenche
  `consulta_backend_em` com clock_timestamp do **servidor PostgreSQL** na ingestão,
  não com instante real de início/fim do GET. Não atribuir essa procedência falsa.
- ETag: inexistente no caso ARES, nenhum parâmetro; não fabricado.
- Offset textual e LastChangeDate original: só o datetime raw é preservado pelo
  SQL; a coluna date não preserva a representação original de LastChangeDate.

`CodigoRegistro` aceita somente Int64 positivo. Um ID não prova COMMIT, contexto,
validade, versão, revogação ou conteúdo. Não montar ReciboEvidenciaProdutoSapR8B
copiando o candidato/horário do aplicativo e alegando que vieram do banco.

## B — fn_p5_r8b_vincular_classificacao

Fonte: linhas 287–353. Entradas: mesmos seis parâmetros empresariais acima +
`p_evidencia bigint` e `p_versao_anterior bigint`, total oito. Ambos são fornecidos
explicitamente; versão anterior zero não é fallback, apenas o valor CAS explícito
que o servidor pode aceitar quando não há vínculo corrente. C# não cria política
de reclassificação nem escolhe a última evidência por PK/data.

Retorno real: `vinculo_id bigint, versao bigint, producttype text`.
`VinculoClassificacao` exige uma linha, IDs positivos, versão anterior+1 e tipo
exato ROH/HIBE. Trata-se de projeção sem COMMIT comprovado; faltam evidência,
identidades, contexto, estado, timestamps e recibo durável. Nenhum campo ausente é
completado a partir do pedido e apresentado como resposta SQL.

## C — fn_p5_r8b_resolver_classificacao

Fonte: linhas 356–410. Entradas exatas: `p_pedido text, p_item text,
p_lancamento bigint, p_material text`.

| Coluna real | Tipo | Mapeamento / validação |
| --- | --- | --- |
| codigo_item_local | bigint | Igual à PK local esperada |
| codigo_sap_item | bigint | Igual à FK LOCAL esperada, sem copiar cache |
| producttype | text | ROH/HIBE positivo, outro/null bloqueia |
| evidencia_id | bigint | ID positivo, não convertido em referência opaca |
| vinculo_id | bigint | ID positivo, diferente de evidence_id |
| versao_vinculo | bigint | Positiva; não é versão da guarda nem da evidência |
| validade_ate | timestamptz | UTC explícito, não nulo, posterior ao instante de checagem fornecido |

`Classificacao` usa as sete colunas e uma única linha. Tipos inesperados,
resultado vazio/duplicado, FK/PK divergentes e validade ausente/vencida ou sem UTC
bloqueiam. Instante de checagem é recebido como argumento: sem relógio local
oculto nem TTL escolhido pelo C#. A checagem local não substitui revalidação
transacional do servidor.

`ProjecaoClassificacaoSqlR8C.EscopoSolicitado` é o contexto do **pedido enviado**,
não campos devolvidos pelo SQL. PO/item/material/lançamento não são projetados.
O corpo SQL valida esse escopo sob locks, mas o resultado não contém toda a
procedência pedida por ReciboEvidenciaProdutoSapR8B. Não inferir estado VALIDADA,
revogação ausente, Product original, timestamps ou referência de coleta pelo fato
de a função retornar sete valores. A projeção parcial não libera operação.

## D — fn_p5_r8b_vincular_tentativa

Fonte: linhas 414–475. Entradas exatas:

| # | Nome / tipo | Origem |
| --- | --- | --- |
| 1–3 | p_pedido text / p_item text / p_lancamento bigint | Identidade.Escopo |
| 4 | p_tentativa bigint | ClaimP5.Tentativa |
| 5 | p_claim uuid | ClaimP5.Token; nunca logar |
| 6 | p_correlacao uuid | ClaimP5.Correlacao; não correlação de coleta |
| 7 | p_vinculo_id bigint | ID retornado na classificação |
| 8 | p_versao_vinculo bigint | Versão da classificação, não ClaimP5.VersaoGuarda |

O construtor compara escopo/PK/FK, exige ROH, claim válido, versão da guarda
positiva e geração não negativa. Não adquire claim ROH para HIBE. Envia os
parâmetros efetivos, sem acrescentar parâmetro SQL inexistente.

Retorno real: text `CLASSIFICACAO_VINCULADA_SEM_AUTORIZAR_POST`.
O leitor retorna somente **TextoSemReciboDuravel**. Qualquer outro texto/null
é inválido; nenhum dos dois resultados confirma persistência ou autoriza POST.
Faltam tentativa/evidência/vínculo/versões/correlação projetados e confirmação de
COMMIT. A assinatura também não recebe versão esperada da guarda.
Não implementar VinculoEvidenciaTentativaP5R8B com valores ecoados do pedido.

## E — datas e representação original

`MapeadorDatasProdutoSapR8C` conserva datetime raw e só produz UTC quando:
atestação explícita do backend + formato ISO com Z/offset + marcador idêntico +
parse de calendário válido. Ausência de offset ou atestação mantém UTC null,
offset_comprovado=false e raw inalterado. Não usar timezone da máquina/sessão.

Representação OData /Date(...)/ é mantida apenas como raw quando não é necessária
a conversão; se a conversão UTC for pedida, o caso bloqueia até contrato de
semântica/offset aprovado. LastChangeDate só aceita data civil ISO; OData epoch,
datetime sem contrato, data inválida e formato cultural bloqueiam o comando, não
são silenciosamente enviados como NULL. Datetime raw preserva a precisão original;
precisão e arredondamento PostgreSQL precisam de QA runtime antes da ativação.

Não há inferência de ETag, revisão SAP, correlação SAP, TTL ou reclassificação.
Adaptação completa de retornos e demais lacunas: `P5-R8C-BLOQUEIOS-E-QA.md`.
