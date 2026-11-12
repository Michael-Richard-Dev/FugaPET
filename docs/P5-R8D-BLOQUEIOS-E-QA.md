# R8D — bloqueios operacionais e evidências de QA

GATE=FUGAPET-ENTRADA-MP-P5-R8D-TYPED-SQL-ABI-ADAPTERS
Branch: `codex/entrada-mp-p5-r8d-adaptadores`.
Base: `e7ffb98a69ebfda611adab2eb089146c99a5c453`.

## Plano focal e arquivos

1. Conferir base/hash e assinaturas; criar worktree isolada sem tocar R8B/R8C.
2. Escrever testes antes do comportamento; preparar comandos e leitores fiéis.
3. Verificar estado de COMMIT sem retry, regressões justificadas e Release.
4. Criar um commit e ZIP consolidado com TRX, manifesto e referência SQL original.

Criados, sem alterar fontes existentes:

- `Servicos/Entrada/P5/ContratosRecibosProdutoSapR8D.cs`.
- `AcessoDados/Repositorio/ComandosRecibosProdutoSapR8D.cs`.
- `AcessoDados/Repositorio/LeitorRecibosProdutoSapR8D.cs`.
- `Servicos/Entrada/P5/ConfirmacaoCommitProdutoSapR8D.cs`.
- `FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R8DAbiTests.cs`.
- `docs/P5-R8D-MATRIZ-ABI.md` e este relatório.

Não há implementação/composição nova de backend, IBackendP5, ingestor, sessão
SQL, controller, writer ou rotina de envio. Comandos Npgsql não recebem conexão
e não são executados. Não há SQL ad hoc para contornar projeções/restrições.
O contrato `ISessaoAbiProdutoSapR8C` continua sem implementação: composição
futura deve usar a MESMA NpgsqlConnection/NpgsqlTransaction para resolver,
reservar P5, fixar vínculo e reservar legado. Este incremento não comprova essa
atomicidade física nem a substitui por duas transações independentes.

## Resultado de função ≠ confirmação de COMMIT

`ConfirmacaoCommitProdutoSapR8D<T>` guarda o resultado tipado não nulo e começa
em ResultadoDaFuncao. Não infere COMMIT por bigint, texto, quantidade de linhas,
elegivel_agora, estado VALIDADA/RESERVADO ou pelo próprio recibo.

A confirmação requer chamada explícita ao callback do futuro executor confiável
que deverá aguardar NpgsqlTransaction.CommitAsync da mesma transação. O estado
muda atomicamente para CommitIndeterminado ANTES de chamar o callback, no máximo
uma vez. Sucesso assíncrono do callback registra CommitConfirmado; exceção,
cancelamento ou perda de resposta mantém CommitIndeterminado. Nova chamada,
inclusive concorrente, não repete o callback. Não existem callback de mutação,
POST, compensação automática ou laço de retry neste controle.

CommitConfirmado é o estado do protocolo C# informado pelo callback concluído,
não atestação criptográfica de WAL/servidor/backend real. Nos testes o callback é
simulado; por isso NÃO se afirma COMMIT PostgreSQL durável, autenticação SAP ou
atomicidade runtime. Não há implementação real do callback neste incremento.
Se o contrato do executor não for confiável, esse estado também não é autoridade.

Em COMMIT indeterminado, a composição futura deve reconsultar por identidade
exata sob interface privilegiada e decisão homologada. Não repetir mutação, não
trocar tentativa, não presumir rollback e não executar novo POST. Nenhum estado
deste controle fornece capability HTTP; as barreiras R8C permanecem fechadas.

## Confiança, datas e pendências

- Backend corporativo confiável continua não identificado na base; não foi criado
  um backend fictício. DTOs desktop não atestam ProductType nem origem do GET.
- Login/session_user PostgreSQL, SHA-256 e elegibilidade momentânea são metadados
  e controles internos, não provas independentes de GET autenticado SAP.
- SQL_QA=IN_PROGRESS_HERMES. Este incremento compara o candidato pinado pelo
  SHA-256, mas não declara PASS do SQL, instalação, privilégios ou deployment.
- Se HERMES identificar defeito material/alterar a ABI, manter bloqueio e registrar
  a divergência para HERA; não corrigir SQL ou atualizar o hash silenciosamente.
- Não foi identificado requisito de nova assinatura SQL para os quatro mappings
  deste escopo. Continuam externas a ele: aprovação QA SQL, backend, roles/grants,
  política temporal/reclassificação, origem SAP e coordenação/fencing B5 completo.
- O início/fim real do GET e raw de LastChangeDate não são campos dessa ABI.
  Não são completados a partir do desktop ou do horário DB de ingestão.
- LastChangeDateTime raw é preservado. Sem offset comprovado, UTC deve permanecer
  null. Com offset comprovado, a conversão ISO explícita precisa concordar com o
  UTC retornado; representação ambígua não é reparada. Sem novo TTL ou regra de
  reclassificação. A semântica e precisão runtime PostgreSQL continuam pendentes.
- `FencingAtomicoDisponivel=false` e `MutacoesHomologadas=false` intocados;
  D3/POST ROH desativados. HIBE, ManufacturingOrder, ZZ1, FK D2 e multi-item/misto
  não foram modificados. Nada do 04_R6B foi incorporado.

## Testes focais

Fixtures são construídas dos campos declarados nas quatro funções do ZIP SQL
original (SHA pinado), via DataTableReader. Leitura é de arquivo local; nenhum SQL
é interpretado/executado. Testes de comandos confrontam nomes/tipos/ordem reais
7/9/4/10; leitores validam 25/18/26/14 campos, nulos e cardinalidade/identidade.

Também são cobertos: tokens só em parâmetros, metadados de coleta distintos da
tentativa, null receipt, divergência de material/tipo/FK/versões/correlação,
revogação, datas contraditórias, tipos inválidos, COMMIT incerto/cancelado e
concorrência sem segundo callback. Fingerprint/login sem elegibilidade não libera.

Red inicial com métodos sem comportamento: 58 casos, 14 falhas e 44 aprovados.
Primeiro green: mesmos 58 aprovados. Um teste adicional reproduziu a necessidade
de rejeitar recibo nulo no controle de COMMIT antes da correção. TRX final cobre
os casos adicionais; red/stack traces de falhas não são empacotados.

Regressões focais justificadas (não são as suítes completas R8B/R8C):

- R7/R7B: HIBE, ManufacturingOrder, ZZ1, ROH/misto/multi-item, origem desconhecida.
- R8A D2: projeção da FK local e ausente/nula/inválida/divergente.
- R7Backend: timeout/500 e crash/COMMIT indeterminado não provocam segundo POST.
- R8: compensação R3B pré-HTTP, trava do executor real, fronteira durável com selo
  separado e rejeição de compensação após fronteira. São 31 regressões simuladas.

Os transportes HTTP e callbacks de commit desses testes são locais simulados.
Nenhum resultado simulado comprova autenticação SAP, privilégios/locks, WAL ou
persistência física PostgreSQL. Build Release e TRX reais acompanham o pacote QA.

Verificação final: **106 aprovados, zero falhas/ignorados** (75 novos e 31
regressões focais). Rebuild Release: **zero erros, 10 avisos preexistentes em
arquivos intocados**. Evidências: `TestResults/R8D/r8d-verified.trx` e
`TestResults/R8D/release-build.log`. Revisão estática focal independente:
nenhum achado material. Isso não altera o estado IN_PROGRESS_HERMES do QA SQL.

## Teste manual — apenas offline

1. Conferir no manifesto do ZIP QA fontes/diff e ZIP SQL original em `reference`.
   Não executar nem aplicar SQL.
2. Na worktree R8D, definir somente no processo PowerShell:
   `$env:FUGAPET_R8D_SQL_PACKAGE='C:\Users\TI\Downloads\FUGAPET_ENTRADA_MP_P5_R8C_SQL_ABI_CANDIDATO_NAO_APLICAR.zip'`
   (ou caminho do mesmo arquivo em reference). Esse parâmetro serve só aos testes.
3. Executar `dotnet test FugaPET_Dev.Tests/FugaPET_Dev.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~EntradaMpP5R8DAbiTests`.
   Conferir os quatro schemas reais e bloqueio de falta/duplicidade/divergência.
4. Conferir ResultadoDaFuncao antes do callback; falha/cancelamento mantém
   CommitIndeterminado; chamadas repetidas/concorrentes não geram novo callback.
   O recibo de função sozinho nunca confirma COMMIT.
5. Conferir nos testes os dez parâmetros e os quatorze campos: versão da guarda
   não é versão de vínculo; correlação da coleta não é da tentativa; tokens não
   são retornados. Não iniciar aplicativo conectado nem realizar GET/POST ou DB.

Nenhuma credencial, token operacional, URL sensível ou payload SAP real incluído.
SAP_GET_REAL/SAP_POST/SAP_WRITE/DB_READ/DB_WRITE/SQL_EXECUTION/DDL_EXECUTION/
MIGRATION/DEPLOY/GIT_PUSH=0. READY_FOR_OPERATIONAL_ACTIVATION=NO.
NEXT_OWNER=HERA V6.
