# R8F — Percurso ROH transacional: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** preparar uma única entrada C# ROH, sem fallback para a vinculação R8B.1 de oito parâmetros ou para o writer legado, mantendo toda execução real desabilitada.

**Architecture:** adaptador ROH interno e dedicado, com executor Npgsql que compõe operações na mesma conexão/transação e separa resultado de função de confirmação de COMMIT. Operações SQL privilegiadas ainda ausentes serão representadas por contratos C# bloqueantes, não por assinaturas SQL inventadas. A futura composição backend/HTTP continuará ausente; testes utilizarão transporte e sessões explicitamente simulados.

**Tech Stack:** C#, .NET 10 Windows, Npgsql 9.0.3, xUnit 2.9.3.

**Spec:** contrato aprovado no pacote `C:\Users\TI\Downloads\FUGAPET_ENTRADA_MP_P5_R8E_GLOBAL_LOCK_CONTRATO_BLOQUEANTE_NAO_APLICAR.zip`, SHA-256 `C71AFCF79487AE06E3C0054081C8E339132AC351D8A8553F5A5E4C37BF1E1E60`, especialmente membros 02, 03 e matriz 04; missão R8F fornecida pela HERA.

## Base e isolamento

- Gate existente: FUGAPET-ENTRADA-MP-P5-R8F-ROH-CSHARP-TRANSACTION-PATH.
- Parent obrigatório: f7eadde9ccf0a2783121d63ed2c9de832ea9fb02.
- Branch: codex/entrada-mp-p5-r8f-roh.
- Worktree: C:\Users\TI\Documents\FugaPET\.worktrees\entrada-mp-p5-r8f-roh.
- A worktree R8D e as demais baselines não serão editadas.
- Implementação autorizada pela HERA em 2026-10-21; execução inline, sem novo ciclo de aprovação.
- Ruling: o caminho informado sem o ponto antes de worktrees não existe. Usar exclusivamente a worktree já preparada acima, branch e parent conferidos; não criar outra ou alterar a R8D.
- Contrato R8F recebido: ZIP FUGAPET_ENTRADA_MP_P5_R8F_ROH_SQL_PRIVILEGED_CONTAINMENT_CONTRATO_NAO_APLICAR.zip, SHA-256 75F1AE57FEF90247BB5082FF05300A12E03E330A947D3940A9EB0151E833963A, conferido antes da primeira alteração de código.
- Ruling: as cinco assinaturas R8F são documentais. Não emitir comandos SQL para elas. O writer dedicado solicitará resultado integral privilegiado, com ordem L→I obrigatória; o DML real permanece bloqueado até GAIA implementar e homologar a operação. Não simular fechamento físico com updates separados.
- Ruling: os recibos documentais R8F não projetam toda a identidade, geração, cobertura e metadados exigidos no C#. Exigir recibo completo por interface tipada e registrar a lacuna; não completar resposta a partir do request.
- Pre-flight Tasks 2–4: reserva produz titular/evidência/versões para resultado, compensação e fronteira; todos devem consumir os mesmos IDs. Recovery não recebe dispatcher e continuará separado. A interface de consulta/fronteira integral ainda é pendência GAIA.

## Global Constraints

- DB_READ=0; DB_WRITE=0; SQL_RUNTIME_EXECUTION=0; MIGRATION=0.
- SAP_GET_REAL=0; SAP_POST=0; DEPLOY=0; GIT_PUSH=0.
- FencingAtomicoDisponivel=false; MutacoesHomologadas=false.
- Uma única entrega/commit focal R8F; não criar commits intermediários de planejamento.
- Não modificar SQL, baseline, roles, grants, TTL, Forms ou Designer.
- HIBE, ManufacturingOrder, ZZ1, FK D2 e bloqueios multi-item/documento misto preservados.
- Recibo SQL e sucesso de mock não comprovam autenticação SAP, atomicidade física ou COMMIT durável PostgreSQL.

## Review Focus

1. Perda do ACK de COMMIT: distinguir de rollback confirmado; não compensar automaticamente nem despachar.
2. Evidência/FK/vínculo divergente após classificação: bloquear antes de qualquer autorização HTTP, sem completar campos do recibo com o request.
3. Retorno SAP positivo sem cobertura integral 1/1 ou com identidade diferente: não confirmar P5/legado.
4. Duas execuções/recovery sobre a mesma tentativa: consumo local não substitui fencing SQL; nenhuma segunda chamada HTTP.
5. Falha/cancelamento entre mutação e COMMIT ou entre commits da fronteira: preservar incerteza e impedir reenvio.

## Arquivos previstos

Todos os caminhos abaixo são relativos exclusivamente à worktree R8F identificada acima.

Alterar:
- AcessoDados/Repositorio/ComandosEvidenciaProdutoSapR8C.cs: impedir construção da chamada antiga em VincularTentativa.
- FugaPET_Dev.Tests/IntegracaoSap/EntradaMpP5R8CAbiTests.cs: atualizar somente expectativas da chamada antiga descontinuada.

Criar:
- Servicos/Entrada/P5/ContratosRohR8F.cs: pedidos/recibos tipados, identidades integrais e portas de operações privilegiadas pendentes.
- AcessoDados/Repositorio/TransacaoRohR8F.cs: coordenação de conexão/transação física e estado do COMMIT, bloqueada antes de abrir conexão.
- Servicos/Entrada/P5/PercursoRohR8F.cs: entrada ROH dedicada, validações, resultado, compensação e fronteira sem fallback legado.
- FugaPET_Dev.Tests/IntegracaoSap/EntradaMpRohR8FTests.cs: testes focais de comportamento e falhas simuladas.
- docs/P5-R8F-INTERFACE-GAIA.md: capacidades/validações/recibos necessários, sem ABI SQL definitiva.
- docs/P5-R8F-CALLERS-E-QA.md: mapa de callers, matriz R8E Q01–Q24, bloqueios e roteiro manual offline.

## Task 1: interditar entrada histórica

**Interfaces:** manter a assinatura C# histórica para não eliminar evidência de testes, mas fazer VincularTentativa rejeitar incondicionalmente a criação de NpgsqlCommand. Não encaminhar automaticamente à assinatura nova: faltam evidência e versão exatas exigidas pelo contrato novo.

- [ ] Escrever teste que exige bloqueio inclusive com todos os argumentos históricos válidos; verificar falha na base atual.
- [ ] Remover a construção da chamada fn_p5_r8b_vincular_tentativa de oito parâmetros; lançar erro sanitizado e explícito.
- [ ] Ajustar os testes históricos apenas nesse comportamento; conservar testes de ingestão, resolução e classificação.
- [ ] Executar testes focais de ABI R8C/R8D impactados, sem banco e sem suíte completa.

## Task 2: contrato e executor transacional ROH

**Interfaces:** preparar IContratoSqlRohR8F com operações sem nomes SQL definitivos: ReservarIntegralAsync, MarcarEnvioAsync, SelarFronteiraAsync, ConsultarTentativaAsync, ConfirmarResultadoIntegralAsync e CompensarPreHttpIntegralAsync. Cada operação recebe NpgsqlConnection/NpgsqlTransaction existentes, pedido tipado e CancellationToken. Ausência de implementação homologada bloqueia; nenhuma implementação de backend corporativo será criada.

**Identidade:** pedido/item/material, lançamento, item local, FK SAP, tentativa quando existente, claim, geração, versão da guarda, vínculo/versão, evidência e correlação. Tokens não aparecem em ToString, logs ou retorno de apresentação. Tipos de domínio existentes são reutilizados quando semanticamente equivalentes.

- [ ] Escrever testes de ausência de contrato/backend e recibos incompletos/divergentes; demonstrar RED antes da implementação.
- [ ] Implementar validações de cardinalidade 1/1 e identidade sem preenchimento permissivo.
- [ ] Implementar executor com uma conexão e uma transação por unidade atômica; validar que a transação pertence à conexão recebida.
- [ ] Validar pós-condições antes de CommitAsync; erro da operação não é sucesso parcial; erro durante COMMIT é indeterminado, sem repetição.
- [ ] Manter bloqueios reais antes da abertura da conexão, inclusive quando forem fornecidos contratos simulados.
- [ ] Testar fail-closed sem acesso a PostgreSQL. A referência física connection/transaction será inspecionada no código; teste mock não será descrito como prova runtime.

## Task 3: reserva e resultado dedicados

**Interfaces:** PercursoRohR8F consome exclusivamente o executor ROH e recibos privilegiados. O writer de resultado usa ConfirmarResultadoIntegralAsync, sem chamar AtualizarStatusAposEnvioSapAsync. As transações integrais exigem G → V → E → T → L → I e atualização L → I no contrato SQL pendente, sem executar DML ad hoc no desktop.

- [ ] Testar classificação desconhecida, HIBE indevidamente encaminhado ao ROH, FK divergente, evidência revogada, mudança de vínculo/versão e múltiplos itens.
- [ ] Testar reserva integral e falha em etapa legada simulada sem COMMIT; declarar que isso comprova apenas o protocolo C#.
- [ ] Implementar caminho dedicado sem selecionar tentativa por maior PK, timestamp ou ausência de guarda.
- [ ] Exigir confirmação de documento, exercício, item de documento, identidade/fencing e cobertura 1/1 para resultado positivo.
- [ ] Testar resultado incompleto/divergente e COMMIT incerto sem fallback ao writer compartilhado.
- [ ] Registrar como pendência GAIA a comprovação física de locks e atualização cabeçalho → item; não declarar resolvida por ordem de eventos em mock.

## Task 4: fronteira, compensação e recovery

**Interfaces:** preparar sequência separada marcar ENVIO_INICIADO/COMMIT, selar fronteira/COMMIT, revalidar estado durável, consumir autorização uma única vez, HTTP fora de transação. Nenhuma composição operacional de transporte será instalada. Recovery usa identidade própria e não recebe callback de dispatch; mutações de recovery ausentes continuam bloqueadas.

- [ ] Testar que recibo de função, COMMIT indeterminado ou ausência de selo durável não autorizam callback HTTP simulado.
- [ ] Testar timeout, HTTP 500, crash simulado e falha ao persistir resultado sem segundo callback HTTP.
- [ ] Testar compensação com token, geração, correlação ou versão divergentes; envio iniciado/selo existente/estado desconhecido impedem compensação.
- [ ] Testar duas chamadas concorrentes simuladas e retorno tardio sem retry; não alegar concorrência multi-instância PostgreSQL comprovada.
- [ ] Implementar somente comportamento demonstrável com os contratos disponíveis; manter operação não fornecida pelo SQL explicitamente indisponível.
- [ ] Reutilizar TimeProvider consistente e validade informada pelo contrato existente, sem criar TTL empresarial.

## Task 5: regressão, evidências e entrega

- [ ] Mapear Q01–Q24 do R8E como coberto por teste C#, inspeção estática ou pendência PostgreSQL/ACL; não marcar todos como PASS runtime.
- [ ] Selecionar regressões existentes de HIBE, ManufacturingOrder, ZZ1, D2 e bloqueios de segundo POST, justificando cada filtro.
- [ ] Executar build Release e testes focais com TRX verdadeiro; registrar contagem, warnings e eventual falha sem segredo/stack trace no pacote.
- [ ] Revisar diff, ausência da chamada antiga construída e ausência de composição operacional nova.
- [ ] Conferir fontes SQL, flags e writers legados inalterados; conferir parent exato e arquivos alterados.
- [ ] Criar um único commit focal autorizado após verificação, sem push.
- [ ] Gerar ZIP QA em Downloads com diff, fontes do commit, metadados Git, relatório de build, TRX, mapa/matriz/interfaces, bloqueios e manifesto SHA-256.
- [ ] Validar integridade do ZIP/manifesto e informar hash final; READY_FOR_OPERATIONAL_ACTIVATION=NO.

## Roteiro manual seguro

1. Conferir branch, parent e diff na worktree R8F; não executar aplicação nem scripts SQL.
2. Inspecionar o bloqueio do builder histórico e a ausência de caminho alternativo de reserva.
3. Conferir validações de identidade, cobertura 1/1 e separação resultado/COMMIT nas novas classes.
4. Executar apenas os testes offline indicados no relatório de QA e conferir o TRX.
5. Conferir que flags reais continuam false e nenhuma conexão/HTTP é aberta pelo caminho bloqueado.
6. Validar SHA-256 e manifesto do pacote; testes físicos de PostgreSQL/ACL/SAP continuam sujeitos a autorização posterior.

## Pendências obrigatórias para GAIA/HERA

- ABI e implementação SQL privilegiada de reserva/resultado/compensação/fronteira/recovery integral ROH.
- Resolução ProductType confiável e durável, política temporal homologada e ligação exata à tentativa.
- Protocolo físico de locks, incluindo cache SAP, FKs, triggers e todos os demais escritores.
- ACLs, owners, memberships, permissões por coluna e isolamento de entradas diretas R8B.1.
- Backend corporativo autenticado e homologado; não existe nesta baseline.
- Testes concorrentes PostgreSQL e reconciliação de COMMIT indeterminado em ambiente autorizado.

## Ledger de execução autorizada

- Task 1: complete — teste red do builder demonstrou 1 falha; builder agora rejeita incondicionalmente, sem comando/fallback.
- Tasks 2–4: complete no escopo C# offline — núcleo red 46 falhas/1 aprovado; green R8F/ABI R8C 101; endurecimento red 8 falhas/4 aprovados; green 121. Recibos nulos/identidade/recovery/status incerto passaram a bloquear. Interfaces SQL e ordem física L→I continuam dependências, não simuladas como solução pronta.
- Task 5: build/testes/revisão complete — Release 0 erros/10 avisos preexistentes; conjunto focal 256 aprovados/0 falhas/0 ignorados; revisor independente somente leitura sem achado concreto. Commit único e pacote serão produzidos após fechar estes fontes; metadados finais e manifesto pertencem ao ZIP de evidências.
- Ruling: o writer C# dedicado exige resultado integral pela porta privilegiada. Não criar updates privados L/I para fingir implantação das funções ausentes. Ordem G→V→E→T→L→I e L→I continuam contrato verificável no futuro SQL, impedindo ativação agora.
- Ruling: NuGet.Config do perfil foi bloqueado no sandbox. Restore offline foi executado com configuração sem fontes remotas e cache local, em comando autorizado. Nenhum pacote novo, SQL ou SAP foi consultado na rede.
- Todos os requisitos de prova física das matrizes Q01–Q30 permanecem explicitamente pendentes quando não demonstráveis offline.
