# R8F — Interface C#/SQL e limites de implementação

GATE=FUGAPET-ENTRADA-MP-P5-R8F-ROH-CSHARP-TRANSACTION-PATH

## Referências conferidas antes de implementar

- R8E: FUGAPET_ENTRADA_MP_P5_R8E_GLOBAL_LOCK_CONTRATO_BLOQUEANTE_NAO_APLICAR.zip; SHA256 C71AFCF79487AE06E3C0054081C8E339132AC351D8A8553F5A5E4C37BF1E1E60.
- R8F GAIA: FUGAPET_ENTRADA_MP_P5_R8F_ROH_SQL_PRIVILEGED_CONTAINMENT_CONTRATO_NAO_APLICAR.zip; SHA256 75F1AE57FEF90247BB5082FF05300A12E03E330A947D3940A9EB0151E833963A.
- As cinco assinaturas do membro 02 são documentais. O pacote não implementa as mutações. Seu único SQL concreto é precheck de catálogo, não executado por este incremento.

## Fronteira de confiança

Não existe backend SAP confiável homologado nesta baseline. Nada foi composto no Program, controller, Form, serviço SAP ou ingestor. Todos os tipos R8F são internos. Nem DTO, flag, session_user, hash, referência textual, evidência em memória ou IDs fornecidos pelo desktop provam GET autenticado/persistência.

O percurso aceita somente recibos tipados de classificação e reserva que deverão vir da futura porta confiável. As validações C# são checagens de consistência, NÃO autenticação desses objetos. A proveniência real continua exigida no backend e no PostgreSQL. O executor Npgsql concreto declara Disponivel=false e verifica também MutacoesHomologadas=false antes de consultar a fábrica. Nenhum JSON/env/controle de tela habilita a operação.

## Composição física preparada

`TransacaoRohR8F.ExecutarAsync<T>` mantém uma NpgsqlConnection e uma NpgsqlTransaction por unidade. A sessão confere estado aberto e ReferenceEquals(tx.Connection, conexao); todas as operações recebem os mesmos objetos por parâmetro. Não abre uma segunda conexão dentro da sessão. Não existe NpgsqlCommand R8F com SQL ad hoc ou função documental presumida instalada.

O callback deve validar o recibo ANTES do COMMIT. Erro de operação solicita RollbackAsync com token independente; só a conclusão desse rollback permite classificá-lo como revertido. Falha de rollback ou CommitAsync gera CommitRohIndeterminadoR8FException. Não se repete função/COMMIT. O executor só devolve T após CommitAsync concluído e descarte da unidade. Essa implementação está operacionalmente bloqueada e não foi executada com PostgreSQL.

## Matriz das cinco operações GAIA

| Contrato documental GAIA | Entradas / saídas propostas | Porta C# | Lacuna antes de executar |
|---|---|---|---|
| fn_p5_r8f_roh_reservar_integral | 12 / 9 | IContratoSqlRohR8F.ReservarIntegralAsync | Recibo proposto não projeta toda a identidade local/SAP/material/item, geração, classificação, versão anterior e cobertura 1/1. Não construir TitularRohR8F/Classificacao a partir do request |
| fn_p5_r8f_roh_vincular_evidencia_interna | 10 / 7 | Sem porta operacional independente no percurso R8F | Deve ser parte interna da reserva integral sob G/V/E/T; reconciliar recibo R8C.1 de 14 campos. Não chamar builder R8B.1 antigo nem conceder entrada direta |
| fn_p5_r8f_roh_registrar_resultado | 16 / 7 | RegistrarResultadoIntegralAsync, consumida por EscritorResultadoRohR8F | Faltam identidade integral, fencing/geração/versão, evidência/vínculo retornados e contagens. Exigir atualização L→I dentro da operação integral; não ecoar request para completar ReciboResultadoRohR8F |
| fn_p5_r8f_roh_compensar_pre_http | 12 / 4 | CompensarPreHttpIntegralAsync | Faltam recibo de identidade integral/geração/versões/cobertura e prova transacional da ausência de envio/selo/HTTP/incerteza. Booleano compensacao_confirmavel não é prova de COMMIT |
| fn_p5_r8f_roh_reconciliar_sem_reenvio | 19 / 6 | ReconciliarSemReenvioAsync, somente executor Recovery | Projetar identidade local completa, titular de recovery exato e cobertura, sem completar dados pelo pedido. permite_novo_post deve ser false |

IContratoSqlRohR8F tem implementações default que lançam R8F_SQL_PRIVILEGIADO_NAO_IMPLEMENTADO. ContratoSqlRohIndisponivelR8F não emite SQL. Os pedidos/recibos C# são domínio interno, não uma declaração de compatibilidade binária já resolvida com as projeções documentais. Nenhuma assinatura GAIA foi alterada.

### Entradas que precisam de decisão de ABI

O titular C# inclui pedido/item/material, lançamento, PK de item local, FK SAP real, tentativa, claim, correlação, geração, versão da guarda, evidência, vínculo e versão do vínculo. A reserva obtém tentativa/claim/correlação do servidor, não do desktop. Operações posteriores validam a identidade inteira e versão anterior; a versão retornada não pode regredir.

A geração é exigida pelo C# para resultado e compensação, mas não aparece como argumento explícito nas propostas dessas funções. GAIA deverá definir o fencing transacional correspondente e o recibo/projeção privilegiada, em vez de descartar esse campo no adaptador. A versão da guarda não é a versão do vínculo. O contexto de auditoria/usuário e os logins backend também precisam de contrato confiável; não são inventados nem configurados aqui.

### Fronteira e leitura durável ainda pendentes

MarcarEnvioAsync, SelarFronteiraAsync e ConsultarTentativaAsync são portas C# bloqueantes adicionais, não novas assinaturas SQL definitivas. As funções core existentes de marcação/selo retornam texto, insuficiente para os recibos integrais exigidos. Necessitam composição privilegiada com ordem global, projeção de identidade/versões, envio_iniciado_em, fronteira_confirmada_em e indicação inequívoca de HTTP/incerteza. Texto genérico não cria capability HTTP.

## Contrato global, writer e estado real

Todas as mutações ROH devem obter G→V→E→T→L→I e manter locks até o fim da mesma transação; itens por PK crescente, ROH 1/1. Resultado ROH usa exclusivamente RegistrarResultadoIntegralAsync, nunca AtualizarStatusAposEnvioSapAsync. A operação SQL futura deve atualizar cabeçalho L ANTES do item I e P5 no mesmo COMMIT. O C# NÃO executa updates separados nem finge que a ordem interna do SQL está comprovada.

Não há implementação SQL executável do writer L→I nesta entrega. Há writer C# dedicado, validação de identidade/documento/cobertura, executor transacional preparado e stub bloqueante. Essa dependência é impeditiva operacional, não um PASS de lock order.

Estados legados usados: FINALIZADO_LOCAL/ERRO_SAP como snapshot anterior à reserva, ENVIADO_SAP como cabeçalho reservado/bloqueante, CONFIRMADO_SAP após confirmação integral. Não se cria ENVIANDO_SAP. Para resultado remoto desconhecido, o recibo não pode restaurar cabeçalho a FINALIZADO_LOCAL/ERRO_SAP nem confirmá-lo; P5 permanece indeterminado. ERRO_SAP nunca é licença de novo POST.

## Protocolo C# preparado

1. Validar consistência do recibo ROH e prova negativa sob HomologacaoP5 existente; não criar política temporal nova.
2. TX reserva integral: recibo completo, cobertura 1/1, COMMIT confirmado.
3. TX ENVIO_INICIADO: validar titular/versão/estado/timestamp, COMMIT confirmado.
4. TX selo: validar titular/versão/estado/timestamps, COMMIT confirmado.
5. TX consulta durável: revalidar titular/selo/ausência de HTTP ou incerteza; concluir unidade antes de HTTP.
6. Consumir tentativa e executar no máximo um callback HTTP. Este callback não está composto com SAP real.
7. TX resultado integral dedicado; desconhecido/500/timeout/crash preserva bloqueio. Falha no resultado/COMMIT nunca repete HTTP.

TimeProvider é compartilhado pelo percurso; usa validade/prova e janelas já homologadas recebidas por HomologacaoP5. Não instala TTL nem política de reclassificação. Expiração/cancelamento imediatamente após reserva permite solicitar compensação exata antes da fronteira. Depois de solicitar marcação de envio, falha/cancelamento não provoca compensação automática.

Compensação consulta e valida o estado exato e efetua compensação integral dentro da mesma unidade, sem escolher tentativa por PK/timestamp. Qualquer envio/selo/HTTP/incerteza rejeita. Recovery usa executor de papel diferente, token/geração próprios e prova posterior à fronteira. Não recebe dispatcher nem fornece autorização de POST.

A barreira local por ChaveP5 não é idempotência distribuída, não sobrevive a processo novo e não substitui fencing SQL. Não há mecanismo de limpeza, desbloqueio automático ou retry. Reconciliação não remove essa barreira nem gera POST. A segurança entre instâncias continua sendo dependência da porta SQL e backend confiável, hoje indisponíveis.

## Condições ainda impeditivas

- Implementação e convergência dos recibos/funções SQL, incluindo ordem L/I e classificação vinculada.
- Backend autenticado, owner/roles reais, auditoria, ACLs e isolamento de todos os writers/ingestores/recovery.
- Protocolo físico para helper R8B.1, cache SAP, FKs e triggers; nenhuma incorporação do 04_R6B.
- Política temporal e prova SAP empresarialmente homologadas.
- PostgreSQL runtime, concorrência multi-instância e reconciliação de COMMIT indeterminado não demonstrados.

SQL_ABI_RUNTIME_COMPATIBLE=NO; GLOBAL_LOCK_ORDER=CONTRACT_ONLY; ATOMICITY_RUNTIME_PROVEN=NO; READY_FOR_OPERATIONAL_ACTIVATION=NO.
