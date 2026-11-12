# R8D — adaptadores tipados da ABI SQL R8C

GATE=FUGAPET-ENTRADA-MP-P5-R8D-TYPED-SQL-ABI-ADAPTERS

Base C#: `e7ffb98a69ebfda611adab2eb089146c99a5c453`.
Pacote candidato: `FUGAPET_ENTRADA_MP_P5_R8C_SQL_ABI_CANDIDATO_NAO_APLICAR.zip`.
SHA256: `099ABCE0F27298CEAE9BE24F4D0B305D74C6A9D2E2D8B94453F8177C92F7FB0B`.
SQL interno: `01_R8C_ABI_DELTA_ADITIVO_NAO_APLICAR.sql`.
SHA256 interno: `5714F8B9381AAF00ADE5650F432D95DA3BAA0A0922968B77366B842FB33464DE`.
SQL_QA=IN_PROGRESS_HERMES conforme autorização recebida. Não é SQL homologado,
instalado ou executado por este incremento. Arquivo original não alterado.

## Entradas das quatro funções

Comandos em `AcessoDados/Repositorio/ComandosRecibosProdutoSapR8D.cs`.
São comandos desconectados, sem executor SQL novo ou registro de DI.

| Função candidata | Parâmetros posicionais, ordem exata | Origem |
| --- | --- | --- |
| fn_p5_r8c_obter_evidencia_recibo | p_evidencia bigint; p_pedido text; p_item text; p_lancamento bigint; p_item_local bigint; p_fk_sap_item bigint; p_material text | ID positivo devolvido pela ingestão e identidade local explícita |
| fn_p5_r8c_obter_vinculo_recibo | p_vinculo bigint; p_evidencia bigint; p_versao bigint; p_pedido text; p_item text; p_lancamento bigint; p_item_local bigint; p_fk_sap_item bigint; p_material text | IDs/versão positivos e identidade local explícita |
| fn_p5_r8c_resolver_recibo | p_pedido text; p_item text; p_lancamento bigint; p_material text | EscopoP5, preservando zeros e item canônico |
| fn_p5_r8c_vincular_tentativa_recibo | p_pedido text; p_item text; p_lancamento bigint; p_tentativa bigint; p_claim uuid; p_correlacao uuid; p_vinculo_id bigint; p_versao_vinculo bigint; p_evidencia_esperada bigint; p_versao_guarda_esperada bigint | Claim exato + recibo de resolução validado; somente ROH |

Todos os parâmetros são obrigatórios no preparo; não há fallback, NULL fictício,
inferência de FK pelo cache ou escolha de maior PK/data. Npgsql usa Text, Bigint
e Uuid explicitamente. IDs e versões não são convertidos entre papéis:
evidence_id ≠ vinculo_id; versao_vinculo ≠ versao_guarda ≠ recovery_generation;
correlation_id da tentativa ≠ correlation_id_coleta_backend.

A ingestão R8B.1 de 15 parâmetros e a criação de vínculo de oito parâmetros
continuam intactas. Futuro backend deverá usar seus resultados apenas como
identificadores para consultar os novos recibos na mesma conexão/transação,
sem tratar scalar bigint ou retorno textual como comprovante de persistência.
WinForms não foi transformado em ingestor autorizado.

## Identidade comum — valores efetivamente retornados

| Campo SQL | Tipo / nulabilidade | Propriedade tipada / verificação |
| --- | --- | --- |
| numero_pedido | text NOT NULL | Identidade.NumeroPedido, igualdade textual exata, zeros preservados |
| numero_item | text NOT NULL | Identidade.NumeroItem, deve ser o item canônico solicitado, sem pad do retorno |
| codigo_lancamento | bigint NOT NULL | Identidade.CodigoLancamento, igual ao lançamento esperado |
| codigo_item_local | bigint NOT NULL | Identidade.CodigoItemLocal, igual à PK local esperada |
| codigo_sap_item | bigint NOT NULL | Identidade.CodigoSapItem, igual à FK LOCAL esperada |
| material | text NOT NULL | Identidade.Material, igual ao material esperado |

`IdentidadeReciboProdutoSapR8D` é construída exclusivamente das colunas retornadas.
Não se copia o EscopoSolicitado de R8C para completar retorno R8D incompleto.
Na tentativa, a projeção não contém material: o DTO não inventa esse campo;
a correspondência material/ROH é exigida do recibo de resolução anterior.

## Dados de evidência — parte compartilhada por 25 e 26 campos

As seis colunas de identidade acima + as dezesseis abaixo formam
`DadosEvidenciaSqlR8D` (22 campos SQL). Todas as propriedades têm ToString redigido.

| Campo SQL | Tipo / nulabilidade | Destino / significado |
| --- | --- | --- |
| evidencia_id | bigint obrigatório | EvidenciaId positivo, não referência opaca nem autorização |
| product | text obrigatório | Product, igualdade exata com material |
| producttype | text obrigatório | ProductType ROH/HIBE explícito, outro/ausência bloqueia |
| origem_servico | text obrigatório | OrigemServico, API_PRODUCT_SRV/A_Product e contexto esperado |
| sap_sistema | text obrigatório | SapSistema, igualdade com ambiente esperado |
| sap_mandante | text obrigatório | SapMandante, igualdade com mandante esperado |
| last_change_datetime_raw | text nullable | LastChangeDateTimeRaw original, sem reformatar |
| last_change_datetime_utc | timestamptz nullable | DateTime UTC nullable; não inferir fuso ausente |
| offset_comprovado | boolean obrigatório | OffsetComprovado; false exige UTC null, true exige raw/UTC inequívocos e coerentes |
| last_change_date | date nullable | DateOnly nullable, sem inventar raw de data civil |
| last_changed_by_user | text nullable | LastChangedByUser, metadado SAP, não usuário local/SQL |
| referencia_coleta_backend | text obrigatório | ReferenciaColetaBackend não vazia, não ETag ou correlação SAP |
| correlation_id_coleta_backend | uuid obrigatório | CorrelacaoColetaBackend não vazia, geração interna do coletor |
| registrado_no_postgresql_em | timestamptz obrigatório | RegistradoNoPostgresqlEm, instante DB, NÃO início/fim do GET |
| validade_ate | timestamptz obrigatório para aceitação | ValidadeAte UTC posterior ao instante de checagem fornecido; nenhum TTL calculado no aplicativo |
| versao_politica | bigint obrigatório para aceitação | VersaoPolitica positiva, não versão da guarda/vínculo |

O SQL pode devolver validade/versão de política nullable em evidência histórica;
esse recibo não é aceito como apto ao próximo passo. NULL não vira valor default.
O instante de checagem é argumento explícito, sem DateTime.Now oculto. O servidor
deverá revalidar sob seus locks; a checagem local não atesta política homologada.

### Evidência: 25 campos

22 campos compartilhados +:

| Campo | Tipo | Propriedade / validação |
| --- | --- | --- |
| fingerprint_sha256 | text | FingerprintSha256, 64 caracteres hexadecimais minúsculos; forma não é autenticação |
| coletor_login | name | ColetorLogin string não vazia; session_user não prova GET autenticado |
| elegivel_agora | boolean | ElegivelAgora efetivamente lido; false bloqueia; true é apenas snapshot da função |

`ReciboEvidenciaSqlR8D` não deriva de um ID escalar e não é convertido num recibo
completo R8B com campos fictícios. Fingerprint/login/elegibilidade isolados nunca
liberam dispatch ou comprovam origem SAP/COMMIT.

### Resolução: 26 campos

22 compartilhados + `vinculo_id bigint`, `versao_vinculo bigint`,
`estado_vinculo text` e `vinculado_em timestamptz`, todos obrigatórios.
`ReciboResolucaoSqlR8D` exige IDs/versão positivos e estado VALIDADA exato.
Não adiciona revogado_em, fingerprint, coletor_login ou elegivel_agora ausentes
dessa projeção. Não presume data de vínculo a partir de outro timestamp.

## Vínculo: 18 campos

| Campos SQL | Tipos | Destino / validação |
| --- | --- | --- |
| vinculo_id / evidencia_id / versao_vinculo | bigint cada | IDs/versão exatos solicitados e referência ao recibo de evidência anterior |
| estado_vinculo | text | VALIDADA; qualquer outro bloqueia |
| numero_pedido / numero_item / codigo_lancamento / codigo_item_local / codigo_sap_item / material | text/text/bigint/bigint/bigint/text | Identidade retornada, comparação completa |
| product / producttype | text cada | Iguais ao recibo da evidência |
| referencia_coleta_backend / correlation_id_coleta_backend | text/uuid | Iguais ao recibo de evidência, sem misturar com a tentativa |
| vinculado_em | timestamptz | VinculadoEm, valor retornado |
| revogado_em | timestamptz nullable | RevogadoEm; se presente, bloqueia |
| validade_ate / versao_politica | timestamptz/bigint | Iguais aos da evidência e validade ainda futura |

## Tentativa: 14 campos

| Campo SQL | Tipo obrigatório | Propriedade / comparação |
| --- | --- | --- |
| numero_pedido | text | NumeroPedido igual ao claim/resolução |
| numero_item | text | NumeroItem igual ao claim/resolução |
| codigo_lancamento | bigint | CodigoLancamento exato |
| codigo_item_local | bigint | CodigoItemLocal igual ao recibo anterior |
| codigo_sap_item | bigint | CodigoSapItem igual à FK anterior |
| tentativa_id | bigint | TentativaId igual ao claim |
| evidencia_id | bigint | EvidenciaId igual ao esperado da resolução |
| vinculo_id | bigint | VinculoId exato |
| versao_vinculo | bigint | VersaoVinculo exata, não versão da guarda |
| versao_guarda | bigint | VersaoGuarda igual ao CAS do claim |
| correlation_id | uuid | CorrelationId da tentativa, não coleta |
| estado_tentativa | text | EstadoTentativa RESERVADO, não ENVIO_INICIADO |
| tentativa_iniciada_em | timestamptz | TentativaIniciadaEm retornado; NÃO é data de vínculo, selo ou envio |
| recovery_generation | bigint | RecoveryGeneration exata e não negativa, separada da versão da guarda |

Nenhum recibo contém claim_token/recovery_token. Token só existe no parâmetro
UUID do comando, nunca no texto SQL, ToString ou DTO de apresentação.

## Tipos, cardinalidade e estado transacional

`LeitorRecibosProdutoSapR8D` exige nomes/ordem/tipos do candidato e uma única linha.
NULL obrigatório, coluna faltante/extra/trocada, coerção int→bigint ou texto→UUID,
estado/identidade divergentes e duplicidade são bloqueantes. Timestamp SQL aceita
somente DateTime UTC ou DateTimeOffset de offset zero; date aceita DateOnly ou
DateTime civil Unspecified à meia-noite. Não aplica timezone da máquina.

O recibo é sempre **resultado da função na transação corrente**. Não contém
campo Duravel/PodeEnviar, nem altera flags P5. Protocolo de COMMIT, limites e
roteiro manual: `P5-R8D-BLOQUEIOS-E-QA.md`.
