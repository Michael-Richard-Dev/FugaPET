# R8A — interface necessária à Gaia para classificação confiável B5-01

Estado: **BLOCKED_SQL_DEPENDENCY**. Proposta de ABI e invariantes, não SQL
executável/aprovado, migration, nova tabela ou autorização de implantação.
Complementa `P5-R8-INTERFACE-GAIA.md`; não modifica R6E.1 nem instala 04_R6B.

## Fronteira existente e insuficiente

Assinatura C# existente:

```csharp
Task<ClassificacaoDuravelP5R8?> ValidarEscopoPersistidoAsync(
    NpgsqlConnection conexao, NpgsqlTransaction transacao,
    EscopoP5 escopo, CancellationToken cancellationToken);
```

`EscopoP5` contém ChaveP5(Pedido, Item), Lancamento e Material. O resultado atual
contém Escopo, CodigoSapPedidoCompraItem, ProductType e ReferenciaAutoritativa.
Não há implementação concreta homologada. Criar esse record com strings do APP
não é prova de banco. O futuro adaptador só poderá mapear resposta privilegiada
verificada; deverá preservar referência/versão de evidência entre validação e
mutação. O record atual não representa sozinho um recibo completo de procedência.

## 1. Origem e registro de evidência — fora do papel do APP

Fonte necessária: A_Product.ProductType, chave Product, serviço
API_PRODUCT_SRV, no sistema/client SAP aprovado. Gaia/Hera devem definir coletor
confiável, credencial/role separada de APP/PROVIDER/RECOVERY e fluxo de ingestão.
Não há tal contrato de ingestão implementado nesta entrega.

Contrato lógico proposto `RegistrarEvidenciaClassificacaoSapV1` (nome de ABI,
não função existente). A chamada deve ser permitida SOMENTE ao coletor atestado:

| Parâmetro | Tipo PostgreSQL | Obrigatoriedade / validação |
| --- | --- | --- |
| sistema_origem | text | Identificador lógico SAP aprovado, não URL/credencial; limitado ao contexto da role |
| mandante_origem | text | Identidade do client, preservar zeros; igual ao contexto atestado |
| produto | text | Product retornado, não vazio; Trim sem conversão numérica |
| product_type | text | ProductType retornado; domínio ROH/HIBE e demais códigos explicitamente contratados; desconhecido não autoriza envio |
| referencia_origem | text | Referência opaca verificável da coleta/resposta, não payload bruto ou string livre do APP |
| revisao_origem | text | ETag/revisão quando disponível; se SAP não fornecer, protocolo de evidência/revisão imutável a homologar, sem preencher valor inventado |
| obtida_em | timestamptz | Instante integral da leitura, obrigatório, não futuro; validado pelo coletor e relógio homologado |
| valida_ate | timestamptz | Posterior a obtida_em e dentro da política de validade aprovada, sem TTL presumido |
| correlacao_coleta | uuid | Correlação imutável da ingestão; não confundir com correlação da tentativa de envio |

Identidade do coletor e registrado_em devem ser atestados pelo servidor, não
aceitos como identidade livre fornecida no APP. Retorno: referencia_evidencia text
opaca, versao_evidencia bigint positiva, registrado_em timestamptz e os campos
normalizados acima. Repetição idempotente da mesma coleta pode retornar a mesma
evidência; divergência da mesma chave/revisão/correlação deve falhar. Evidência
versionada precisa preservar a história; atualização de cache não pode alterar
retroativamente a classificação usada por uma tentativa.

Transação de ingestão: uma única transação do coletor para evidência+procedência;
nenhuma reserva ou POST. A forma física de armazenamento, proteção contra UPDATE
por APP, validação da referência e política temporal serão definidas pela Gaia.
O parâmetro product_type é aceitável somente nessa fronteira de ingestão atestada,
jamais como decisão autoritativa entregue pelo aplicativo operacional.

## 2. Resolução e vínculo empresarial na transação P5

Contrato lógico proposto `ResolverClassificacaoPersistidaP5V1` (não instalado):

| Parâmetro de entrada | Tipo PostgreSQL | Origem C# |
| --- | --- | --- |
| numero_pedido | text | EscopoP5.Chave.Pedido, 1–10 dígitos, preservar zeros |
| numero_item | text | EscopoP5.Chave.Item, cinco dígitos canônicos |
| codigo_lancamento | bigint | EscopoP5.Lancamento, positivo |
| material_esperado | text | EscopoP5.Material; só comparação, nunca prova de origem |

Sistema/client devem ser resolvidos a partir do contexto persistido/role
atestado, não de valor padrão ou parâmetro APP. Resolver sob locks e exigir
exatamente um item local de `entrada_produto_item` do lançamento informado,
mesmo PO/item canônico e material. Exigir FK local positiva, existente e igual à
PK do único item SAP correspondente no cache; material desse item deve coincidir.
Não obter a FK por fallback do cache; não escolher último lançamento/maior PK.
FK nula (inclusive por ON DELETE SET NULL), duplicidade, item de outro lançamento,
material divergente ou contexto SAP ausente devem produzir bloqueio integral.

Resolver a evidência autoritativa de Product do MESMO material e contexto SAP;
validar validade/revisão/identidade do coletor. Vincular duravelmente evidência e
revisão ao `codigo_entrada_produto_item` e lançamento antes da reserva: não basta
ler um cache mutável por material. Não inferir HIBE por ausência de guarda, ZZ1,
default, erro de consulta ou ausência de evidência. Sem evidência válida, bloquear
sem mudar status legado e sem reservar. ROH exige P5; HIBE deve preservar o fluxo
legado com classificação positiva, sem ampliação implícita de P5 a HIBE.

Resultado bem-sucedido necessário: exatamente UMA linha, com:

| Campo | Tipo PostgreSQL | Garantia necessária |
| --- | --- | --- |
| numero_pedido, numero_item, material | text cada | Iguais ao escopo validado |
| codigo_lancamento, codigo_item_local | bigint cada | Positivos, pertencimento comprovado |
| codigo_sap_pedido_compra_item | bigint | FK local comprovada, não derivada do cache |
| product_type | text | Código derivado da evidência; ROH/HIBE não inferidos |
| referencia_evidencia, revisao_origem | text cada | Identidade verificável e imutável da fonte |
| versao_evidencia | bigint | Positiva; CAS/fencing obrigatório nas mutações |
| sistema_origem, mandante_origem | text cada | Contexto atestado pelo servidor |
| obtida_em, valida_ate, vinculado_em | timestamptz cada | Precisão integral; vínculo durável e temporalidade válida |

Ausência/multiplicidade/erro ou resultado incompleto não gera classificação
default nem recibo de sucesso. Não retornar payload SAP, credencial, headers,
token ou detalhes internos desnecessários. A aplicação deve tratar tudo como
bloqueio operacional sanitizado. Se não for possível atestar revisão/procedência
ou binding durável, este contrato permanece NÃO IMPLEMENTÁVEL até decisão Gaia.

## 3. Atomicidade e autorização das mutações existentes propostas

Reserva, compensação e resultado continuam exigindo a identidade comum R8:
PO text, item text, lançamento bigint, tentativa bigint, claim_token uuid,
versao_guarda_esperada bigint, correlation_id uuid, material text e usuario bigint.
Adicionar ao futuro contrato privilegiado a referência/versão de evidência e a
identidade do item local resolvidos pelo servidor, não substituir a tentativa por
ORDER BY data. Guard version e recovery generation permanecem independentes.

Executar resolução, validação e mutação conjunta P5+legado usando a MESMA
NpgsqlConnection/NpgsqlTransaction fornecida pelo executor. Locks: guarda por
PO/item → tentativa exata → cabeçalho → único conjunto de itens; incluir locks/
CAS sobre vínculo/evidência em ordem determinística aprovada pela Gaia. A ordem
adicional precisa ser homologada para evitar inversões entre ingestão e reserva.
Nada pode validar em uma conexão e reservar em outra. Nenhum COMMIT interno ou
janela de cache lido antes da transação pode substituir fencing sob lock.

O recibo deve guardar o vínculo classificação/evidência/item na mesma transação
da reserva P5+legada. Divergência da evidência entre validação e mutação deve
rejeitar/rollback, nunca reclassificar silenciosamente. Ausência do binding deve
bloquear inclusive tentativas anteriores não migradas, sem presumir HIBE.

Preservar compensação R3B somente antes de ENVIO_INICIADO durável, com tentativa,
token, versão e correlação exatos. COMMIT incerto exige reconciliação e não
autoriza novo dispatch. Resultado exige cobertura integral dos itens associados
ao documento material; ROH multi-item/misto continua bloqueado. Nenhum estado
ENVIANDO_SAP artificial de lote. Não alterar contratos R6E.1 unilateralmente.

## Lacunas para Gaia / aceite futuro (não executado)

1. Definir coleta confiável, contexto SAP e revisão/validade; atestar permissões.
2. Definir persistência versionada e binding imutável item/lançamento/evidência.
3. Entregar resolver privilegiado e mutadores atômicos compatíveis com a ABI,
   incluindo comparação de revisão/versão e proteção do fluxo HIBE sem inferência.
4. Testar ausência, FK nula/divergente, material/PO/item/contexto divergentes,
   evidência expirada/futura/revogada, duplicidade, spoof por APP, troca concorrente
   de classificação, rollback e COMMIT ambíguo em ambiente autorizado.
5. Só então implementar e homologar o adaptador C#, sem ativação automática.

`READY_FOR_GAIA_B5_SQL_DELTA=YES` significa interface entregue para Gaia preparar
o delta; não significa SQL aprovado/instalável. `B5_01_STATUS=BLOCKED_SQL_DEPENDENCY`.
`READY_FOR_OPERATIONAL_ACTIVATION=NO`.
