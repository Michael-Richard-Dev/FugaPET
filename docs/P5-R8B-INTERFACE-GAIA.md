# R8B — interface C#/SQL e divergências para Gaia/HERA

Status: **BLOCKED_BACKEND_AND_SQL_DEPENDENCY**.
Contrato de preparação; nenhuma função SQL é declarada instalada/aprovada.
Não alterar R6E.1, a baseline R8A ou o 04_R6B reprovado.

## Assinaturas C# preparadas

Em `Servicos/Entrada/P5/ContratosEvidenciaProdutoSapR8B.cs`:

```csharp
Task<EvidenciaProdutoSapR8B?> ColetarAsync(
    string material, CancellationToken cancellationToken);

Task<ReciboEvidenciaProdutoSapR8B> RegistrarEvidenciaClassificacaoSapV1Async(
    NpgsqlConnection conexao, NpgsqlTransaction transacao,
    EvidenciaProdutoSapR8B evidencia, CancellationToken cancellationToken);

Task<ReciboEvidenciaProdutoSapR8B?> ResolverClassificacaoPersistidaP5V1Async(
    NpgsqlConnection conexao, NpgsqlTransaction transacao,
    EscopoP5 escopo, CancellationToken cancellationToken);

Task<VinculoEvidenciaTentativaP5R8B> VincularEvidenciaTentativaAsync(
    NpgsqlConnection conexao, NpgsqlTransaction transacao,
    IdentidadeItemProdutoSapR8B identidade, ClaimP5 tentativa,
    ReciboEvidenciaProdutoSapR8B evidencia, CancellationToken cancellationToken);
```

Primeiro método: `IColetorBackendProdutoSapR8B`, sem implementação.
Demais métodos: `IContratoEvidenciaProdutoSapGaiaR8B`, sem implementação.
Não há NpgsqlCommand, SQL ad hoc, implementação de autenticação, conexão aberta
ou registro DI novo. Estas são interfaces de capacidade necessária, não ABI
PostgreSQL descoberta por reflexão ou função executável. A assinatura final e
sua disponibilidade devem ser verificadas quando Gaia entregar SQL R8B.

## Registro da evidência: entradas disponíveis

Os nomes abaixo são nomes lógicos propostos, não colunas criadas. Os tipos
preservam o que foi observado; não convertem string SAP sem fuso em timestamptz.

| Parâmetro lógico | Tipo PostgreSQL a contratar | Campo C# / origem |
| --- | --- | --- |
| sistema_origem | text | Contexto.Ambiente; backend atestado, QAS110 no contexto informado |
| mandante_origem | text | Contexto.Mandante; backend atestado, 110 no contexto informado |
| origem_tecnica | text | Contexto.OrigemTecnica = API_PRODUCT_SRV/A_Product |
| produto | text | Campos.Product, valor da resposta SAP |
| product_type | text | Campos.ProductType, valor da resposta SAP |
| last_change_datetime_original | text nullable | Campos.LastChangeDateTimeOriginal, exatamente o texto recebido |
| last_change_datetime_offset_original | text nullable | Marcador lexical explícito, se reconhecido; não offset calculado |
| last_change_date_original | text nullable | Campos.LastChangeDateOriginal, sem conversão implícita |
| last_changed_by_user | text nullable | Campos.LastChangedByUser; metadado SAP, não identidade do coletor |
| etag_origem | text nullable | ETagSap, somente header recebido; NOT_AVAILABLE nesta baseline |
| consulta_iniciada_em | timestamptz | Contexto.ConsultaIniciadaEm, relógio do backend |
| consulta_concluida_em | timestamptz | Contexto.ConsultaConcluidaEm, relógio do backend; equivale ao instante final da obtenção, não à alteração SAP |
| correlacao_coleta | uuid | Contexto.CorrelacaoColetaBackend, gerada pelo backend; não SAP |

`IdentidadeBackend` identifica o serviço na DTO, mas a identidade persistida do
coletor deve ser derivada/atestada pela sessão/role do servidor. Não aceitar essa
string do desktop como autenticação. A função deve recusar APP/PROVIDER/RECOVERY
como ingestor quando não possuírem autoridade de coleta expressamente aprovada.
Proveniência do ambiente/client também deve ser validada contra essa identidade.

Resultado necessário: referência opaca e versão positiva geradas pela persistência,
data de registro gerada pelo banco, estado de revogação e os campos controlados
da evidência. `ReciboEvidenciaProdutoSapR8B` modela esses dados, não os autentica.
Não usar ETag null como revisão vazia aprovada, nem LastChangeDateTime como revisão
monotônica/imutável presumida. Gaia deve definir referência/revisão e retenção.

Transação de ingestão: do coletor atestado para evidência+procedência somente,
sem POST/reserva. A consulta GET ocorre antes dessa transação, nunca sob lock
de tentativa durante HTTP. Ausência de COMMIT comprovado não produz recibo durável.

## Resolução empresarial e vínculo da tentativa

ResolverClassificacaoPersistidaP5V1 mantém as entradas propostas no R8A:
numero_pedido text, numero_item text canônico de cinco dígitos,
codigo_lancamento bigint positivo e material_esperado text.
Retorno C# é provisório: inclui somente o recibo de evidência; a função e o vínculo
devem verificar/atestar também as identidades locais abaixo. Sem esse contrato
não se pode implementar `IContratoLegadoP5R8` a partir deste recibo.

Entradas adicionais necessárias ao vínculo transacional:

| Grupo / parâmetro | Tipo PostgreSQL | Origem e exigência |
| --- | --- | --- |
| numero_pedido / numero_item | text cada | ChaveP5; validar igualdade e contexto SAP sob lock |
| codigo_lancamento | bigint | EscopoP5.Lancamento; pertence ao conjunto local exato |
| codigo_item_local | bigint | IdentidadeItemProdutoSapR8B.CodigoItemLocal, validar pertencimento |
| codigo_sap_pedido_compra_item | bigint | FK LOCAL positiva/existente, sem copiar valor do cache |
| material_esperado | text | EscopoP5.Material; coincide com material local/cache e Product atestado |
| codigo_tentativa | bigint | ClaimP5.Tentativa, nunca escolher a última tentativa |
| claim_token | uuid | ClaimP5.Token; não logar/serializar em pacotes |
| versao_guarda_esperada | bigint | ClaimP5.VersaoGuarda; CAS sob lock, não recuperação |
| geracao_recovery | bigint | ClaimP5.GeracaoRecovery, campo distinto; não reutilizar token/geração de envio como recovery |
| correlation_id | uuid | ClaimP5.Correlacao, imutável e diferente da coleta |
| referencia_evidencia / versao_evidencia | text / bigint | Recibo do armazenamento atestado, sem valor inventado pelo APP |
| correlacao_coleta | uuid | Igual à coleta associada à referência/versão no banco |

ProductType vinculado deve ser resolvido da evidência privilegiada, não parâmetro
de decisão do desktop. `VinculoEvidenciaTentativaP5R8B.ProductType` representa o
valor devolvido pelo banco; `VinculadaEmBanco` vem do servidor. Todas as chaves
devem ser devolvidas/comparadas no recibo, com referência/versão e estado durável.
Zero/múltiplos matches, FK nula, divergência de material/contexto, evidência
revogada, tentativa diferente ou mudança de tipo bloqueiam sem nova classificação.

Receber os mesmos objetos NpgsqlConnection/NpgsqlTransaction da reserva P5 e
legada, sem segunda transação nem COMMIT interno para o vínculo. Validar versão,
evidência não revogada e identidade sob locks/CAS até o commit conjunto. A ordem
guarda → tentativa → cabeçalho → itens é preservada; Gaia deve homologar a ordem
adicional dos locks de evidência/vínculo e concorrência com revogação/coleta.
O resultado precisa provar lançamento+item+tentativa+evidência associados, não
apenas a existência de uma guarda. COMMIT incerto bloqueia dispatch e retry.

## Matriz de divergências ABI — decisão HERA obrigatória

| Tema | Proposta R8A / situação recebida | R8B preparado | Decisão pendente |
| --- | --- | --- | --- |
| revisao_origem | R8A exigia referência/revisão verificável; ETag indisponível | Nenhum ETag/revisão fabricado; metadados SAP originais separados | Gaia/HERA definir revisão sem ETag; não substituir por data/hash/correlação por suposição |
| valida_ate | Obrigatória na proposta R8A; política temporal NOT_APPROVED | Sem campo calculado, TTL, clock de expiração ou default | Homologar contrato temporal e só então representar/migrar o parâmetro |
| referência da coleta | R8A exigia referencia_origem verificável | CorrelacaoColetaBackend é somente correlação local; não a promove a referência SAP | Definir recibo/atestado verificável e resolver sua referência |
| LastChange* / LastChangedByUser | Não constavam da ABI resumida R8A | Campos string nullable por allowlist | Gaia contratar preservação textual/metadados sem inferir versão/fuso |
| estado de revogação | Persistência não entregue | Recibo tem RevogadaEmBanco, comparações locais conservadoras | Definir revogação autoritativa e revalidação sob lock na fronteira |
| vínculo de tentativa | R8A exigia binding durável, sem função entregue | Interface tipada separada, sem SQL instalado | Gaia definir assinatura, resultados, roles, atomicidade e idempotência |
| código_usuario / auditoria | R8 exige ator positivo no executor das mutações legadas | Contrato novo não altera o executor/ator; coletor exige identidade própria de serviço | Gaia definir auditoria da ingestão e vínculo; não usar LastChangedByUser como usuário local |

**Não é possível afirmar compatibilidade de ABI SQL executável.** Não há R8B SQL
fornecido neste gate; os métodos são preparação tipada, não adaptador instalado.
Quando entregue, comparar nomes, schema, overloads, ordem/tipo/nulabilidade dos
parâmetros, nomes/tipos das projeções, versões, locks, transações, roles e retornos.
Diferença não é resolvida alterando SQL Gaia ou inventando defaults C#.

## Condições de ativação — todas externas a este incremento

- Backend confiável autorizado; autenticação/coletor e armazenamento atestados.
- SQL R8B aprovado e comparado com esta ABI, inclusive revisão/temporalidade.
- Roles/grants de coleta, provider e recovery separados e homologados.
- Testes reais autorizados de revogação, concorrência, idempotência e COMMIT incerto.
- Decisão HERA explícita para composição/ativação; sem ativação por DTO ou flag.

`FencingAtomicoDisponivel` e `MutacoesHomologadas` permanecem false.
`READY_FOR_OPERATIONAL_ACTIVATION=NO`. Nenhuma escrita SQL, migration ou GET/POST
real foi executado para produzir estas interfaces ou os testes.
