# R8C — bloqueios, revisão Gaia e QA focal

GATE=FUGAPET-ENTRADA-MP-P5-R8C-CSHARP-SQL-ABI-CONVERGENCE
Branch: `codex/entrada-mp-p5-r8c-abi`.
Resultado: **PREPARAÇÃO_ABI_PARCIAL_FAIL_CLOSED**, não integração operacional.

## Plano e arquivos

1. Validar hash do pacote, ler assinaturas/corpos e criar worktree da base R8B.
2. Testar antes de implementar; construir comandos tipados e ler retornos reais.
3. Comparar as quatro assinaturas diretamente com o ZIP imutável e testar limites.
4. Build Release, testes focais, commit único e pacote QA com manifesto.

Criados: `Servicos/Entrada/P5/ContratosAbiProdutoSapR8C.cs`,
`Servicos/Entrada/P5/MapeadorDatasProdutoSapR8C.cs`,
`AcessoDados/Repositorio/ComandosEvidenciaProdutoSapR8C.cs`,
`AcessoDados/Repositorio/LeitorResultadosEvidenciaProdutoSapR8C.cs`,
`FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R8CAbiTests.cs`,
`docs/P5-R8C-MATRIZ-ABI.md` e este relatório. Nenhum arquivo R8B ou SQL alterado.

## Alterações de contrato necessárias — para Gaia/HERA, NÃO implementadas

As funções candidatas não estão instaladas. A proposta abaixo registra exigências
para convergência completa, não altera SQL nem declara novo gate intermediário.

| Função atual | Assinatura/retorno atual | Contrato requerido e justificativa |
| --- | --- | --- |
| fn_p5_r8b_ingestar_producttype | 15 entradas da matriz; retorna bigint | Retorno privilegiado completo ou nova projeção privilegiada por evidence_id que devolva identidade empresarial, Product/Type/raw, contexto/coleta, referência, timestamps gerados pelo servidor, política/validade e estado. ID sozinho não permite construir ReciboEvidenciaProdutoSapR8B sem fabricar metadados |
| fn_p5_r8b_vincular_classificacao | 8 entradas; retorna (vinculo_id bigint,versao bigint,producttype text) | Projetar evidence_id e identidade completa do vínculo, estado e timestamps/procedência necessários; distinguir vinculo.versao de versão de evidência/guarda. A projeção de três campos não é recibo durável |
| fn_p5_r8b_resolver_classificacao | 4 entradas; retorna sete campos da matriz | Acrescentar PO/item/material/lançamento, Product, referência/correlação de coleta, contexto, timestamps, política/validade, estado/revogação explícitos e identificador/versão com semântica homologada. Alternativamente fornecer projeção privilegiada conjunta por evidência/vínculo sob mesma tx. Não conceder SELECT direto às tabelas como fallback |
| fn_p5_r8b_vincular_tentativa | 8 entradas; retorna text | Propor acrescentar p_evidencia_esperada bigint e p_versao_guarda_esperada bigint às oito entradas, validar ambos sob o mesmo lock/CAS, devolver tentativa/evidência/vínculo/versões/correlação e estado exatos em retorno tipado. Texto genérico não permite verificar identidade do resultado nem fencing da guarda |

Para a proposta de retorno da vinculação de tentativa, campos mínimos nomeados:
numero_pedido text, numero_item text, codigo_lancamento bigint,
codigo_item_local bigint, codigo_sap_item bigint, tentativa_id bigint,
evidencia_id bigint, vinculo_id bigint, versao_vinculo bigint,
versao_guarda bigint, correlation_id uuid, estado_tentativa text e
vinculada_em timestamptz. Não é nova função SQL criada neste incremento.
O token de claim continua validado no servidor privilegiado, não exposto em view
D2 nem substituído por correlação. Recovery generation não substitui versão.
Retorno tipado também NÃO prova COMMIT; exigir confirmação durável/reconsulta
privilegiada por identidade exata após resultado de commit indeterminado.

Metadados adicionais sem equivalência atual:

- Se exigida a preservação durável da coleta backend, contratar parâmetros
  `consulta_iniciada_backend_em timestamptz` e
  `consulta_concluida_backend_em timestamptz`, com procedência atestada; não renomear
  clock_timestamp de ingestão PostgreSQL como instante do GET.
- Se exigida a representação original da data civil, contratar
  `last_change_date_raw text` separado da coluna date; não inventar data para
  preencher uma representação OData ainda sem conversão aprovada.
- A identidade de ingestor/session_user e fingerprint SHA-256 não provam GET SAP
  autenticado. Host confiável e atestação de resposta são dependências reais,
  não resolvidas com novos parâmetros de texto fornecidos pelo WinForms.

Gaia pode propor nomes/resultados equivalentes, mas o adaptador deve ser revisto
contra a ABI aprovada, nunca completar defaults ou alterar SQL unilateralmente.
Mudanças de assinatura acima permanecem **REQUIRED_FOR_COMPLETE_RECEIPTS_AND_FENCING**.

## Fronteira de confiança e transação

Backend confiável continua **não encontrado na base aprovada**. Não há host ou
executor corporativo novo. Os construtores aceitam DTOs para preparação offline,
não autenticam seu chamador e não tornam a declaração do desktop autoritativa.
Nenhum resultado novo entra em `IContratoLegadoP5R8`, controller ou writer.

O fluxo a homologar permanece: resolver sob guarda → reservar P5 → fixar vínculo
da tentativa → reservar legado na MESMA conexão/transação → COMMIT comprovado →
ENVIO_INICIADO durável e selo próprio antes de qualquer HTTP. Os adaptadores não
abrem transação, não comitam e não executam comandos. O contrato de sessão recebe
conexão/tx existentes e CancellationToken, sem implementação real nesta entrega.
Nunca chamar ingestão como fallback por falha de resolver no desktop.

Recibos parciais não comprovam atomicidade física. COMMIT indeterminado não vira
rollback presumido ou autorização de repetição. FencingAtomicoDisponivel e
MutacoesHomologadas continuam false, D3/POST ROH desativados e retry inexistente.
Persistem as dependências B5 de reserva/compensação/resultado legados, backend,
roles/grants, revisão/temporalidade/reclassificação, implantação e QA runtime.

## Testes e evidências

Classe nova: `EntradaMpP5R8CAbiTests`. Comandos reais são inspecionados sem conexão;
DataTableReader simula apenas projeções de resultado, não PostgreSQL. Quatro casos
leem as assinaturas diretamente do ZIP SQL com hash fixado. SHA é verificação de
integridade de arquivo, não autenticação de SAP. Nenhum script SQL foi executado.

Red inicial: 42 casos, 23 falhas / 19 aprovados com os métodos ainda sem
comportamento. Depois: 42 aprovados. Casos adicionais verificam assinatura do ZIP,
tipos/projeções, cardinalidade, nulabilidade, CAS e rejeição de validade sem UTC.

Regressões justificadas pela nova fronteira de ABI:

- R7/R7B: HIBE, ManufacturingOrder, ZZ1, ROH multi-item/misto e desconhecido.
- R8A D2: projeção FK local e FK ausente/nula/inválida/divergente (oito casos).
- R7Backend: timeout/500 sem segundo POST e COMMIT/crash ambíguo bloqueante.
- R8: compensação R3B pré-HTTP na coordenação simulada e executor real fechado
  antes de abrir conexão, quando falta homologação/fencing.

Não se repetiram as suítes completas R8/R8B. GET/POST dos testes legados existem
somente em handlers locais simulados. Nenhum teste comprova autenticação SAP,
ACL/roles, locks ou atomicidade física PostgreSQL. TRX final e build Release
acompanham o ZIP; falhas históricas com stack trace não são empacotadas.

Verificação final: **84 aprovados, zero falhas/ignorados** (55 novos e 29
regressões focais). Rebuild Release: **zero erros, 10 avisos preexistentes em
arquivos intocados**. Evidências: `TestResults/R8C/r8c-verified.trx` e
`TestResults/R8C/release-build.log`. Revisão estática focal independente: nenhum
achado material; não é homologação PostgreSQL runtime ou autenticação SAP.

## Teste manual — offline somente

1. Extrair o ZIP QA e conferir seu manifesto SHA256. O subdiretório `reference`
   contém o ZIP SQL candidato original, sem alterações; NÃO aplicar/executar SQL.
2. Na worktree R8C, definir apenas no processo PowerShell a variável
   `$env:FUGAPET_R8C_SQL_PACKAGE='C:\Users\TI\Downloads\FUGAPET_ENTRADA_MP_P5_R8B1_SQL_V_ATIVOS_FIX_NAO_APLICAR.zip'`
   (ou caminho absoluto do mesmo ZIP em reference). Não é configuração operacional.
3. Executar `dotnet test FugaPET_Dev.Tests/FugaPET_Dev.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~EntradaMpP5R8CAbiTests`.
   Conferir parâmetros reais e que nulos/identidades divergentes/projeções
   incompletas bloqueiam, enquanto texto genérico não confirma vínculo durável.
4. Conferir datas: sem offset atestado, UTC fica null; quando conversão necessária
   não for inequívoca, o comando deve bloquear. Raw não é substituído por UTC.
5. Conferir o TRX das regressões e a matriz: nenhuma nova chamada de controller/
   writer, nenhum backend fabricado, nenhuma flag operacional habilitada.
   Não abrir aplicativo conectado, não executar SQL, migrations ou GET/POST real.

Nenhuma credencial, token operacional, URL sensível ou payload SAP real incluído.
SAP_GET_REAL/SAP_POST/SAP_WRITE/DB_READ/DB_WRITE/SQL_EXECUTION/DDL_EXECUTION/
MIGRATION/DEPLOY/GIT_PUSH=0.
READY_FOR_OPERATIONAL_ACTIVATION=NO. NEXT_OWNER=HERA V6.
