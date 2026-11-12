# R8A — D2 FK e procedência B5-01

GATE=FUGAPET-ENTRADA-MP-P5-R8A-D2-FK-TRUSTED-CLASSIFICATION

Base imutável: `fc3020f76c08af335d8bd35d74ad0d9681bb7684`.
Branch: `codex/entrada-mp-p5-r8a-d2-classificacao`.
Escopo: correção de uma projeção D2, testes offline e contrato documental.
O checkout contém `FugaPET_Dev`; não foi renomeado para atender ao nome do gate.

## Plano focal executado

1. Reproduzir a omissão usando o comando SQL realmente emitido pelo repositório.
2. Acrescentar somente a FK local ao SELECT final; preservar a validação C#.
3. Rastrear ProductType nas fronteiras existentes e explicitar a dependência SQL.
4. Verificar Release e somente os novos testes; entregar commit e evidências QA.

## D2: causa e correção

`Servicos/Entrada/P5/Contratos/D2LocalR5B.sql` já selecionava
`i.codigo_sap_pedido_compra_item` na CTE `local_item`, mas não a projetava na
saída. Foi acrescentado exclusivamente `li.codigo_sap_pedido_compra_item,` ao
SELECT final. Não há cast, COALESCE, alias alternativo, subconsulta ou substituição
pela PK do cache. Nenhum JOIN, filtro, agrupamento, ordenação ou categoria mudou.

O nome resultante é `codigo_sap_pedido_compra_item`. A definição de referência
está em `BancoDados/000_baseline/Banco_Homologacao_V1_1_Geral/000_execucao_completa_homologacao_v1_1.sql`:
linha 4272, FK nullable **bigint** de `entrada_produto_item` para
`sap_pedido_compra_item` (PK bigint na linha 1143). Esses arquivos foram somente
lidos; não são comprovação de instalação no PostgreSQL.

`EntradaMpP5D2Repositorio.LerRecurso` incorpora a consulta a partir de WITH;
`CriarComando` parametriza PO/item/lançamento; `ConsultarLinhasAsync` usa o nome
da coluna e `GetValue`, que representa bigint como `Int64`. A validação existente
`Classificar`/`MaterialECacheCoerentes` exige FK local `long > 0`, igualdade com
`sap_item_id` do único item do cache e material correspondente no lançamento atual.
FK ausente/nula/tipo inválido/zero/negativa/divergente continua bloqueando.
Não se atribui a FK a partir do cache. Corrigir a projeção não prova recebimento
negativo SAP nem libera bootstrap, reserva, fronteira HTTP ou segundo POST.

## Matriz da classificação SAP

| Etapa | Arquivo / método existente | Identidade e dados disponíveis | Persistência / limite de confiança |
| --- | --- | --- | --- |
| Fonte técnica | `Servicos/IntegracaoSap/ProductMasterSapApiClient.cs`, `MontarUrlProduto`, linhas 14–32 | GET `API_PRODUCT_SRV/A_Product`, chave Product, seleção Product/ProductType/ProductGroup/BaseUnit; contexto SAP da configuração validada | Origem autoritativa **pretendida** é A_Product.ProductType no sistema/client SAP homologado. Não houve GET nesta tarefa nem atestação operacional |
| Transporte real | `Servicos/IntegracaoSap/ProductMasterSapServico.cs`, `ObterProdutoAsync` / `ObterJsonProdutoAsync`, linhas 35/86 | Valida destino, faz GET, propaga CancellationToken; falta de tipo/erro retorna null | Não grava a resposta de Product Master nem recibo verificável no banco; não devolve versão/ETag/instante de obtenção/identidade do coletor |
| DTO → domínio | `Modelo/IntegracaoSap/ProdutoSapMestre.cs`, `SapProductMasterDto` / `DeDto`, linhas 7/49 | Product → CodigoProduto; ProductType → TipoMaterialSap; Consultado=true | Objeto mutável em memória. Consultado não é assinatura, prova de origem ou homologação; DTO pode ser construído pela aplicação |
| Seleção de serviço | `Servicos/IntegracaoSap/FabricaProductMasterSapServico.cs`, `Criar` | Serviço real ou mock governado por banco/demo/ambiente | Mock também produz Consultado=true; não usar esse boolean para atestar procedência PostgreSQL |
| Pedido empresarial | `Servicos/IntegracaoSap/PedidoCompraSapApiClient.cs`, `MapearItens`, linha 545; `Modelo/IntegracaoSap/PedidoCompraSapItem.cs` | PurchaseOrder + PurchaseOrderItem + Material; CodigoItem torna-se a PK do cache na leitura do repositório | TipoMaterialSap/ClassificacaoEntrada são enriquecidos depois, não persistidos pelo caminho do cache |
| Enriquecimento visual | `Controle/Processo/EntradaProdutoController.cs`, `EnriquecerEClassificarItensAsync`, linha 1762 | Consulta por material, enriquece PedidoCompraSapItem com TipoMaterialSap e classificação | Só memória; esse enriquecimento não valida por si só o vínculo Product retornado/material do item nem serve como recibo SQL |
| Cache de pedido/item | `AcessoDados/Repositorio/SapPedidoCompraRepositorio.cs`, `ObterPedidoAgregadoAsync` / `ListarItensPorNumeroPedidoAsync` / `UpsertItensAsync`, linhas 248/427/549 | PO/item, codigo_produto, PK bigint, campos de compra, sincronizado_em e payload_original | Upsert não grava ProductType nem vincula codigo_sap_produto; payload_original é do pedido/item de compra, não prova controlada de A_Product |
| Item → lançamento | `Tela/Processo/ProcessoEntradaProdutoForm.cs`, `MontarLancamentoDoGrid`, linha 2573; `Modelo/Entrada/EntradaProdutoItem.cs`; `EntradaProdutoRepositorio.InserirItemAsync`, linha 706 | PK do cache, número do item, material, centro, depósito, unidade; FK ao lançamento | Persistidos item/lançamento/material/FK, mas não ProductType, origem, referência de evidência ou revisão da classificação |
| Reconstrução para envio | `AcessoDados/Repositorio/EntradaProdutoRepositorio.cs`, `ListarItensParaEnvioSapAsync`, linha 1051; `Modelo/Entrada/EntradaProdutoItemEnvioSap.cs` | Lançamento como argumento, PO/item/material/lotes e pesos persistidos | DTO de envio não carrega PK local do item, FK SAP nem procedência do mestre; não é vínculo B5 durável |
| Pré-dispatch | `Controle/Processo/PreparadorPayloadMaterialDocumentEntrada.cs`, `PrepararAsync`; `EntradaProdutoController.MontarItemMaterialDocument` | Nova consulta Product Master por material; posição com PO/item/material; lançamento e lotes no contexto | `ClassificacaoMaterialDocumentP5.Registrar` só associa tipo se Consultado e Product/material coincidem; ConditionalWeakTable privada por instância, não persistida nem serializada |
| Política | `Servicos/Entrada/P5/ClassificacaoMaterialDocumentP5.cs`, `Bloqueia`; `Servicos/Entrada/P5/ContratosP5R7.cs`, `ExigeP5` | ROH exige P5 e continua bloqueado; tipo desconhecido bloqueia pedido; HIBE mantém fluxo legado sem ZZ1 | Ausência de ZZ1/guarda não atribui HIBE. Preservados ManufacturingOrder e demais tipos/regras existentes |
| Fronteira DB R8 | `AcessoDados/Repositorio/TransacaoEntradaMpP5R8.cs`, `Sessao.AplicarLegadoAsync`; `Servicos/Entrada/P5/ContratoLegadoP5R8.cs` | EscopoP5(PO/item, lançamento, material), ClaimP5 e recibo com FK/ProductType/referência | Interface `IContratoLegadoP5R8` **sem implementação concreta**. `CorrespondeRoh` valida forma/igualdade, não autentica a referência. Executor real permanece desabilitado antes de abrir conexão |

## Persistência encontrada e lacunas

A baseline possui `sap_produto.tipo_material varchar(50)` (linha 1036),
`sincronizado_em`, `valido_ate`, `payload_original`, e FK opcional
`sap_pedido_compra_item.codigo_sap_produto` (linha 1145). Portanto, seria incorreto
afirmar que não existe coluna de tipo em todo o modelo SQL.

Entretanto, na cadeia examinada não existe carga de A_Product para essa tabela,
vínculo obrigatório preenchido pelo upsert, revisão imutável da evidência,
atestação de sistema/client/coletor confiável ou binding da classificação ao item
local/lançamento. Cache mutável e timestamp de sincronização não suprem esses
requisitos. As migrations versionadas não estabelecem esse contrato de procedência.
O R6E.1 consultado estaticamente é contrato de tentativa/fencing/recovery, não
persistência autoritativa de ProductType. O B5 é bloqueante, não SQL instalável.
Não incorporar `04_R6B_FENCING_LEGADO_NAO_EXECUTAR.sql`.

Conclusão: `CLASSIFICATION_PERSISTED=NOT_PROVEN`; `B5_01_STATUS=BLOCKED_SQL_DEPENDENCY`.
Não foi criado adaptador fictício, tabela ou coluna. A interface exata necessária,
com chaves, validações, resultado e transação, está em `P5-R8A-CONTRATO-GAIA-B5-01.md`.

## Evidência e limitações de QA

Teste novo: `FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R8AD2Tests.cs`.
O teste da projeção inspeciona o comando emitido usando o recurso SQL embarcado,
isolando o SELECT final: ocorrência apenas na CTE não satisfaz o contrato.
Conexão Npgsql construída sem connection string, nunca aberta. Os demais testes
exercitam o classificador real com fixtures sintéticas, parse OData, ligação por
material e perda da evidência em memória após serialização. Nenhum POST/GET ou DB.

Red verificado: 22 testes, 21 aprovados e 1 falha esperada na projeção, antes da
correção. Green verificado: 22 aprovados, zero falhas/ignorados. Rebuild Release
da solução: zero erros, 10 avisos preexistentes em arquivos intocados.
Um erro inicial de nome do enum no teste foi corrigido antes desse red;
não é apresentado como prova da falha de produção. TRX final e build Release
acompanham o pacote QA. Não se repetiram os 54 testes R8 ou a suíte completa.
Esses testes não atestam SQL em execução, catálogo real, locks, privilégios ou
procedência SAP instalada. Homologação runtime depende da Gaia e de autorização.

## Bloqueios preservados

- D3 real, bootstrap atômico e fencing legado B5 sem homologação: desativados.
- Provider/recovery, compensação R3B, COMMIT incerto e proibição de retry: intocados.
- ROH multi-item/misto e ausência/divergência de classificação: bloqueados.
- Projeção corrigida não habilita flags de dependências, DI, credenciais ou worker.
- HIBE, ManufacturingOrder, ZZ1, status e SQL de baseline não foram alterados.
- Sem execução de SQL, DB_WRITE/SAP_WRITE/SAP_POST/MIGRATION/DEPLOY/PUSH=0.
- Nenhuma credencial, token operacional, URL sensível ou payload SAP real incluído.

## Teste manual — somente offline

1. Na worktree R8A, conferir `git show --stat HEAD` e o diff da consulta: somente
   a linha `li.codigo_sap_pedido_compra_item,` deve ser adicionada ao SQL.
2. Executar `dotnet test FugaPET_Dev.Tests/FugaPET_Dev.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~EntradaMpP5R8AD2Tests`.
   Esperado: 22 aprovados, incluindo FK ausente/nula/inválida/divergente bloqueada,
   identidade coerente sem impedimento D2 e guarda ausente ainda bloqueante.
3. Conferir os casos ProductType: ROH e desconhecido bloqueiam; HIBE coerente
   mantém política legada; material divergente/não consultado bloqueia; serializar
   e reidratar o item perde a evidência em memória e não libera dispatch.
4. Ler o contrato Gaia e conferir que não existe adaptação SQL implementada nem
   ativação por flags. Não iniciar aplicativo conectado, não testar envio SAP,
   não executar o SQL nem migrations neste roteiro.

Próximo dono: HERA V6. Pronto para QA focal e especificação do delta SQL pela Gaia,
**não pronto para ativação operacional**.
