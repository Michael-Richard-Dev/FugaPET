# P5 R7 — fechamento focal SOURCE ONLY

Base preservada: aac9726b4f77d37d407c168b00b8588caa0df6a6. Branch: codex/entrada-mp-p5-d2d3-r7. Worktree real: C:\Users\TI\Documents\FugaPET\.worktrees\entrada-mp-p5-r7; o caminho recebido FugaPET.worktrees não existe. Nenhum checkout foi movido.

## 1. Evidência original, antes do patch

ZIP canônico: C:\Users\TI\Downloads\FUGAPET_P5_R7_AAC9726_EVIDENCE_V3.zip

SHA256: B7FEE4598097F2DAADBA166ACC607013FCEDBCD70261B9FF5DD2F258D073B5AB

Contém diff R6→R7, 13 fontes/documentos commitados pelo R7 em committed-sources/, manifesto SHA256 de todo o conteúdo e manifesto individual dos fontes, evidence-r7.json original, TRX original r7_commit_aac9726.trx, relatório nominal de 146 resultados Passed, log de build/test e worktree limpa capturada antes deste fechamento. Os 13 fontes foram exportados por git cat-file blob e verificados contra os OIDs do commit (13/13), sem conversão de atributos/line endings do git archive. evidence-r7.json conserva os hashes históricos do worktree usado no build; os hashes dos blobs transportados estão em COMMITTED_SOURCES_SHA256.txt. V1/V2 foram preservados, mas V3 é a evidência canônica. Não é exportação do projeto completo nem novo teste dos 146: é transporte do original. Configs operacionais/segredos, bin/obj e histórico BancoDados não foram incluídos.

## 2. FK na projeção D2 — delta PROPOSTO, NÃO APLICADO

No SQL 01 R5B, local_item já seleciona i.codigo_sap_pedido_compra_item, mas o SELECT final só publica numero_item/material/status/documentos. MaterialECacheCoerentes do adaptador não pode distinguir FK NULL legítima de coluna ausente; bloqueia a ausência. Não corrigir esse caso presumindo NULL ou removendo validação.

Menor delta a submeter à Gaia/Hera, exclusivamente no SELECT final aprovado:

```diff
     li.numero_item AS numero_item_local, li.material AS material_local,
+    li.codigo_sap_pedido_compra_item,
     li.status_lancamento, li.status_item,
```

Isso projeta coluna já existente no CTE; não cria schema/índice. Ainda precisa revisão contratual e testes de FK divergente, item canônico duplicado, outro PO/material. Não foi aplicado aos recursos SQL, ZIPs, banco ou migration.

## 3. Bootstrap sem guarda — bloqueado por contrato

R6A fn_reservar cria a guarda por INSERT ON CONFLICT DO NOTHING e usa FOR UPDATE. Entretanto, D2 R6A.1 retorna NULL_BLOCK_NO_LOCAL_GUARD e o coordenador R7 exige D2 sem impedimento antes de reservar: primeiro claim nunca avança no adaptador real. Isso é conflito de fases, não motivo para transformar ausência local em prova SAP negativa.

Delta focal proposto para adjudicação: distinguir resultado diagnóstico SEM_GUARDA de autorização; somente o backend confiável, após prova SAP homologada e revalidação local, pode solicitar aquisição inicial na transação da fn_reservar. A validação completa sob lock e a tentativa durável têm de concluir antes de qualquer liberação/HTTP. Definir o retorno entre D2/D3 e a prova sob lock em revisão conjunta; não alterar a categoria SQL para liberar por ausência. Nenhuma implementação de bootstrap liberador neste fechamento.

## 4. Multi-item — não suportado e mantido bloqueado

O contrato R5B/R6A possui uma guarda/tentativa por PO/item. O POST legado pode conter N itens; o coordenador R7 recebe um EscopoP5, não um documento e coleção de claims. Não há contrato de reserva conjunta de N chaves, ordem global de locks, cancelamento parcial, vínculo de N tentativas a um documento SAP ou mapeamento autoritativo de itens da resposta.

Não dividir um POST legado em N POSTs. Não invocar o coordenador em loop para contornar essa limitação. Enquanto não homologado o protocolo documental, qualquer documento contendo ROH/ZZ1, inclusive misto ROH+HIBE, permanece bloqueado integralmente no writer e no cliente antes de HTTP. Os testes focais verificam dois ROH e documento misto nos dois pontos. Documento HIBE puro não recebe esse bloqueio.

## 5. Atomicidade com legado — proteção focal adicionada

R6A exige que o resultado em guarda/tentativa e, quando aplicável, legado Entrada MP sejam persistidos na mesma transação. O template fn_resultado_http atual só atualiza as duas tabelas P5. EntradaProdutoRepositorio.AtualizarStatusAposEnvioSapAsync (linha 1344 do R7) atualiza itens/cabeçalho em ExecutarEmTransacaoAuditavelAsync própria; chamar ambos sequencialmente não compartilha conexão/tx e não oferece atomicidade conjunta. A lista de documentos dos itens legados usa ordem best-effort: não promover isso como identidade autoritativa de item SAP.

Correção segura deste fechamento: IBackendP5.ResultadoLegadoAtomicoDisponivel tem default false. Coordenador e recovery bloqueiam antes de prova, reserva, dispatch ou mutação se essa capacidade não estiver disponível. O adaptador real herda false; somente a fixture sintética de backend declara true. Dois testes reproduziram o avanço indevido no contrato anterior e verificam o novo bloqueio. MutacoesHomologadas continua false.

Delta futuro, não executado: um ponto de persistência backend/DB revisado deve validar escopo, token, geração quando recovery, identidade documental por item, cardinalidades e atualizar P5+legado na mesma conexão/transação, revertendo tudo se qualquer item/cabeçalho falhar. Deve preservar auditoria, estados indeterminados e ausência de retry. Exige revisão de owner/EXECUTE e dos statements legados por Gaia/Janus; nenhum corpo SQL alternativo foi inventado aqui.

## 6. Caminhos de POST e limite de cobertura

| Caminho C# existente | Fronteira e comportamento |
| --- | --- |
| Form → EntradaProdutoController → IntegracaoEntradaSapServico → MaterialDocumentSapGovernadoServico → MaterialDocumentSapServico | P5 no controller e bloqueio ZZ1 no writer antes da capability/dispatch |
| Chamada direta a MaterialDocumentSapServico | Mesmo bloqueio antes do dispatch, inclusive seam sintético |
| Chamada direta a MaterialDocumentSapApiClient | Bloqueio ZZ1 antes de CSRF/HTTP; POST físico em _httpClient.SendAsync(post) |
| ProdutoAcabadoMovimentoAdapters / gateway de 101 PA | Usa o mesmo cliente; request por ManufacturingOrder sem ZZ1 não é reclassificado como Entrada MP; sem alteração funcional PA |
| MaterialDocumentSapConfiguracaoInvalidaServico / mock | Não são dispatchers SAP reais |
| Outbox | Não foi encontrado dispatcher executável de Entrada MP em AcessoDados/Servicos/Controle. ExclusaoPesagemRepositorio apenas consulta fn_entrada_produto_sap_guard_counts por correlação. Não presumir ausência de job externo ou fila no banco |

Essa cobertura identifica todos os call sites C# do cliente/writer no snapshot auditado; não é prova de inventário de jobs externos. O DTO não contém ProductType: payload sem ZZ1 não pode ser reconhecido como ROH só por seu formato. A classificação do fluxo normal ocorre antes, em ClassificadorItemEntradaMaterial/PreparadorPayloadMaterialDocumentEntrada. Não ampliar o bloqueio a todo PO para cobrir request forjado, pois isso bloquearia HIBE. Fonte autenticada de classificação e envelope de autorização no dispatcher futuro continuam pendentes. Nada habilitado e nenhum P5 bypass criado.

## 7. HIBE e testes focais

Os testes usam classificação real ProductType=HIBE, EntradaProdutoAlocacao101.Decidir e EntradaProdutoController.MontarItemMaterialDocument. A quantidade legada é 5.5 (formato 0.###), não 5.500; par ZZ1 permanece null. Writer e cliente completam CSRF+POST exclusivamente em HttpMessageHandler falso, sem rede, preservando o comportamento HIBE. Documento misto com ROH é bloqueado pelo item ROH, não por classificação indevida de HIBE.

Executar somente EntradaMpP5R7*: testes de atomicidade, D2/FK/bootstrap, multi-item, HIBE, concorrência/fencing e falhas impactados. Não repetir regressão completa nem os 146 originais. TRX/LOG novos ficam em C:\Users\TI\Documents\FugaPET\EVIDENCE\P5_R7_CLOSURE.

Resultado focal: 62/62 PASS em Release, sem skipped. As duas falhas de proteção de atomicidade foram reproduzidas antes do patch; os dois testes HIBE tiveram expectativa inicial corrigida de 5.500 para o formato legado comprovado 5.5, sem alterar produção. Revisão independente estática sem achados materiais no diff focal. Isso não comprova locks/ACL/atomicidade real PostgreSQL nem rede SAP.

## 8. Status final

Fonte/contratos originais R5B/R6A/R6A.1 preservados; nenhuma DDL/DML/SAP real/deploy/push. P5 continua bloqueado. Candidato apto à revisão independente de código/evidência, não à ativação. Permanecem pendentes R6 QA, segurança/roles, política temporal, prova SAP autoritativa, deltas D2/bootstrap, protocolo multi-item e persistência atômica real.
