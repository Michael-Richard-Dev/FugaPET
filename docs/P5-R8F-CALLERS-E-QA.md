# R8F — Callers, matriz focal e roteiro de QA

GATE=FUGAPET-ENTRADA-MP-P5-R8F-ROH-CSHARP-TRANSACTION-PATH

Base f7eadde9ccf0a2783121d63ed2c9de832ea9fb02; branch codex/entrada-mp-p5-r8f-roh.
Worktree efetiva C:\Users\TI\Documents\FugaPET\.worktrees\entrada-mp-p5-r8f-roh. O caminho informado posteriormente sem o ponto antes de worktrees não existe; foi preservada a worktree já preparada, com branch/base verificadas. Nenhuma baseline aprovada foi editada.

## Arquivos do incremento

Alterados somente ComandosEvidenciaProdutoSapR8C.cs (bloqueio do método histórico) e EntradaMpP5R8CAbiTests.cs (expectativas dessa chamada). Criados ContratosRohR8F.cs, TransacaoRohR8F.cs, PercursoRohR8F.cs, EntradaMpRohR8FTests.cs, este documento, interface GAIA e plano autorizado. Não alterados Forms/Designer, controllers, writers HIBE/MO, mappers de quantidade/ZZ1, SQL ou flags homologadas.

## Mapa de callers após a correção

| Origem | Destino/comportamento | Composição |
|---|---|---|
| AcessoDados/Repositorio/ComandosEvidenciaProdutoSapR8C.cs, VincularTentativa | Lança P5_R8F_ENTRADA_DIRETA_R8B1_PROIBIDA antes de construir NpgsqlCommand; não encaminha à assinatura nova | Histórico não executável |
| EntradaMpP5R8CAbiTests, VinculoHistoricoDeOitoParametrosNaoConstituiAlternativaExecutavelRoh e casos de identidade/ABI | Exigem rejeição mesmo com argumentos históricos válidos | Testes offline |
| ComandosRecibosProdutoSapR8D.VincularTentativa | Constrói comando R8C/R8C.1 de 10 parâmetros/14 campos | Builder desconectado, sem executor/composição operacional; não é fallback R8F |
| PercursoRohR8F.ExecutarAsync | IExecutorRohR8F → sessão → ReservarIntegralAsync/marcação/selo/consulta → writer dedicado | Código novo interno, sem Program/DI/backend/SAP real |
| EscritorResultadoRohR8F.EscreverAsync | Somente RegistrarResultadoIntegralAsync; valida identidade, CAS, documento e cobertura 1/1 | Porta SQL ausente; não chama writer legado |
| PercursoRohR8F.CompensarAsync | Consulta exata + compensação integral na mesma unidade | Sem seleção por última PK/timestamp |
| RecoveryRohR8F.ReconciliarAsync | Somente ReconciliarSemReenvioAsync, executor Recovery | Sem callback HTTP; nenhum desbloqueio automático |
| TransacaoRohR8F.Sessao | IContratoSqlRohR8F com a mesma conexão e tx | Disponivel=false; stub lança; não abre conexão |
| EntradaMpP5BackendR8/EntradaMpP5BackendRepositorio anteriores | Mutações core P5/R8 e IContratoLegadoP5R8 anteriores | Preservados, desativados pelos bloqueios existentes; não compõem percurso operacional ROH |
| EntradaProdutoRepositorio.TentarReservarLancamentoParaEnvioSapAsync/LiberarReservaEnvioSapAposAbortPrePostAsync/AtualizarStatusAposEnvioSapAsync | Reserva/compensação antigas e resultado itens→cabeçalho | Preservados para legados; novo percurso ROH não os chama. Atestação de ACL/writers externos permanece pendente |
| MaterialDocumentSapServico/MaterialDocumentSapApiClient | Bloqueios reais ROH/UNKNOWN/ZZ1/misto/multi-item existentes | Intocados; testes de regressão usam HttpMessageHandler simulado |

No SQL candidato R8C.1 ainda existe wrapper → fn_p5_r8b_vincular_tentativa → helper. Ingestão/vínculo/resolução R8B.1 e recibos R8C também alcançam o helper. Esses fontes SQL não foram alterados e seus locks/ACL não são corrigidos pelo bloqueio do builder C#. Nenhum caller externo, job, role ou banco instalado foi presumido inventariado.

## Matriz R8E Q01–Q24

Os testes desta entrega provam somente comportamento C# offline; coluna pendência não significa cenário físico executado.

| ID | Evidência C# focal | Pendência física/contratual |
|---|---|---|
| Q01 | DuasChamadasConcorrentesDaMesmaEntradaNaoFazemDoisDispatches | Dois processos/conexões e exclusão SQL |
| Q02 | Builder antigo sempre rejeitado | EXECUTE/memberships reais |
| Q03 | ABI R8D 10 entradas/14 saídas preservada | Relocks internos R8C.1/R8B.1 |
| Q04 | Nenhum ingestor/composição novo | Serialização INGESTOR/PROVIDER |
| Q05 | Porta SQL única bloqueada, sem DML de contorno | Helper, locks L/I/cache/FK |
| Q06 | Writer dedicado não usa método itens→cabeçalho | Implementar/observar SQL L→I |
| Q07 | HibeRealMapperPreservaWriterEClienteComHttpFalso | Fluxo HIBE real sob identidade aprovada |
| Q08 | ClassificacaoIncompletaOuNaoRohNaoIniciaTransacao | Procedência SAP/backend autenticado |
| Q09 | ManufacturingOrderPreservaRotaDosOutrosProcessos | Runtime autorizado MO |
| Q10 | FalhaNaOperacaoNaoConfirmaEstadoParcialNemRepeteHttp | Rollback PostgreSQL físico |
| Q11 | CommitIncertoNuncaCompensaNemRepetePost, commits 1–5 | ACK/WAL/reconexão reais |
| Q12 | FronteiraExigeCommitsSeparadosEHttpOcorreForaDaTransacao | Crash de processo e estado durável |
| Q13 | ResultadoRemotoDesconhecidoBloqueiaSegundoPost | Quarentena SQL após timeout/500 |
| Q14 | CompensacaoExigeAusenciaDeHttpEIdentidadeExata | CAS SQL/ACL, mesma tx real |
| Q15 | Compensação rejeita envio/selo/HTTP/incerteza | Escritores concorrentes reais |
| Q16 | WriterRohExigeConfirmacaoIntegralDoDocumento | Confirmação atômica P5+L/I |
| Q17 | Documento/item divergente rejeitado sem segundo POST | Retorno tardio/auditoria privilegiada |
| Q18 | RecoveryDivergenteNaoConfirmaNemEnvia | Dois recoveries, token/geração SQL |
| Q19 | ReciboDeReservaDivergenteNaoProduzPost MULTI/COBERTURA | Cardinalidade SQL sob lock |
| Q20 | Nenhum UPDATE R8F ad hoc/flag habilitada | Privilégios de coluna/tabela APP |
| Q21 | Nenhum GRANT/role criado | SET ROLE/memberships/owner reais |
| Q22 | Nenhum helper/cache alterado | Atualização P/S concorrente e FKs |
| Q23 | Prova/classificação expirada ou vínculo divergente rejeitados | Reclassificação concorrente SQL |
| Q24 | Nenhuma migration/DDL executada | Rollback de implantação futura |

R8F GAIA Q25–Q28: bloqueio C# direto é testado, mas permissões, ingestor, cache e separação física ROH/HIBE permanecem dependências SQL/ACL. Q29: falha no resultado/commit simulados não confirma parcial nem repete HTTP; atomicidade física pendente. Q30: barreira local retém incerteza; reinício/múltiplas instâncias exige backend/SQL, sem alegação de prova por mock.

## Testes focais e regressões justificadas

- R8F: classificação, FK/PK, vínculo/evidência, cobertura, fronteira, falha de resultado, COMMIT incerto, cancelamento/expiração, compensação exata, recovery separado, ausência de backend/stub e concorrência local simulada.
- ABI R8C: testes históricos ajustados apenas para a chamada que passou a ser proibida; demais métodos não foram alterados.
- ABI R8D: garantir que a assinatura tipada 10/14 continua intacta, não substituir por fallback 8/texto.
- R7/R7B/Closure: HIBE, ManufacturingOrder, UNKNOWN, ZZ1 incompleto e multi-item/misto; transporte simulado.
- R8A D2: FK local ausente/nula/inválida/divergente nunca reconstruída pelo cache.
- QuantidadePedidoZz1R2 e PesoRealNaoSubstituiQuantidadeDocumentalRoh: quantidade integral do item SAP continua separada do peso real ZZ1; novo percurso não constrói nem altera payload.

Não repetir suíte completa. O relatório build/TRX final do pacote registra os resultados reais e o filtro exato. Testes red locais contêm falhas esperadas e não integram o pacote para evitar stack traces; o TRX de entrega deve conter somente execução final aprovada, sem alteração do arquivo original.

Execução verificada: build Release do projeto de testes com referências reconstruídas, 0 erros e 10 avisos em arquivos preexistentes; 256 testes focais aprovados, 0 falhas, 0 ignorados. TRX real: TestResults/R8F/r8f-verified.trx. A classe R8F e a ABI R8C somaram 121 testes na verificação anterior. Red inicial do builder: 1 falha; red do núcleo com stubs: 46 falhas/1 aprovado; endurecimento adicional: 8 falhas/4 aprovados, seguido de green. Nenhuma suíte completa foi executada.

Revisão independente de fonte, somente leitura, não encontrou defeito concreto nos quatro arquivos novos de código/testes e no delta existente. Não executou testes nem atestou backend/ACL, locks, fencing entre processos ou PostgreSQL runtime. Os bloqueios operacionais permanecem explícitos.

## Verificação manual offline

1. Conferir parent, branch e diff; confirmar que não há arquivos SQL, Forms, mappers de quantidade/ZZ1 ou flags modificados.
2. Inspecionar VincularTentativa histórico: deve lançar antes de criar comando e não redirecionar para outro builder.
3. Conferir o único caminho novo de resultado: EscritorResultadoRohR8F → RegistrarResultadoIntegralAsync. A ordem SQL L→I continua exigida/pendente, nunca inferida por uma lista de eventos simulados.
4. Em PowerShell, definir FUGAPET_R8C_SQL_PACKAGE e FUGAPET_R8D_SQL_PACKAGE para os ZIPs históricos com hashes pinados; ler filtro/command do pacote. Executar somente dotnet test do projeto de testes, Release, --no-restore, filtro focal e logger TRX. Não iniciar WinForms nem configuração real de SAP/banco.
5. Conferir TRX: projeto FugaPET_Dev.Tests real, zero falhas; testes HTTP usam somente handlers/delegates simulados.
6. Conferir TransacaoRohR8F.Disponivel=false, TransacaoEntradaMpP5R8.FencingAtomicoDisponivel=false e BloqueioP5R7.MutacoesHomologadas=false. Não habilitar nenhuma delas para testar manualmente.
7. Conferir manifesto SHA-256 e hash externo do ZIP. Não executar precheck SQL, migration ou POST.

## Bloqueios e critérios honestos de entrega

READY_FOR_OPERATIONAL_ACTIVATION=NO. As cinco funções R8F não estão implementadas, não foram chamadas e não são consideradas instaladas. Não há backend confiável, ACLs atestadas, composição de dispatcher ROH ou política temporal nova. A convergência ABI requer os campos adicionais descritos na interface GAIA, sem eco de request. COMMIT/rollback/concorrência/lock order PostgreSQL reais NÃO comprovados.

Código novo não contém credenciais, host SAP/DB real, payload bruto ou logging de tokens. ToString dos pedidos/titulares/recibos é sanitizado. Dados dos testes R8F são sintéticos. Nenhum commit intermediário, push, deploy, SQL ou acesso SAP/banco foi realizado durante implementação.
