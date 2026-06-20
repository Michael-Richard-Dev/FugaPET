# Submissao ao Gaia Dados - H22 (Endurecer Banco de Desenvolvimento V1.1)

## Status

APROVADO pelo Gaia como plano de endurecimento (diretriz). Executar agora SOMENTE a Secao A
(somente-leitura). A Secao B sera aplicada item a item, em incrementais separados ou na proxima
baseline consolidada de desenvolvimento. Nenhum arquivo de baseline foi alterado nesta entrega.

## Parecer do Gaia (incorporado)

- Secao A aprovada para execucao no DEV (somente-leitura). EXCEPTION para integridade de
  dados/schema; WARNING para postura de implantacao. Confirmado.
- Transacao global no instalador 000 aprovada, desde que o script final nao tenha comandos
  incompativeis com transacao.
- Aprovado trocar `date_trunc` por `clock_timestamp()` no bootstrap 001 e continuar a remocao
  gradual nos cadastros remanescentes.
- Roles: aprovado criar role dedicada, com SEPARACAO entre role de APLICACAO e role de MIGRACAO.
  A aplicacao nao usa postgres/superusuario; em DEV pode ser ampla, mas SEM DELETE amplo e SEM DDL.
  (Refletido em B4.)
- Setor padrao unico: regra de negocio confirmada (maximo um ativo por usuario). A4 correta; a
  proxima consolidacao deve criar INDICE UNICO PARCIAL para proteger a regra no banco. (Novo B6.)
- FKs criticas sem CASCADE: confirmado. Ampliar a validacao para incluir `log_integracao_sap`,
  `integracao_sap_outbox`, `integracao_sap_tentativa` e tabelas operacionais da INT012/HU quando
  existirem. (A5 ampliada + A5b para INT012/HU como WARNING de revisao.)
- INTEGRACAO_SAP_ATIVA no DEV: DECISAO = padrao `false`; `true` so em janela de teste controlado;
  ajustar o validador 002 para nao exigir `true` incondicionalmente. (B5.)

## Artefato

`025_h22_endurecimento_banco_desenvolvimento_PROPOSTA_GAIA.sql`

- **Secao A**: validacoes somente-leitura (seguras pos-instalacao).
- **Secao B**: mudancas estruturais propostas para o baseline, mantidas COMENTADAS
  (transacao global, bootstrap com `clock_timestamp()`, remocao gradual de
  `date_trunc`, role de aplicacao + grants, alinhamento de `INTEGRACAO_SAP_ATIVA`).

## Objetivo

Transformar a instalacao do banco de desenvolvimento V1.1 em algo mais seguro e validavel,
sem alterar o baseline diretamente: a proposta separa o que e validacao (segura) do que e
mudanca estrutural (a ser aprovada e incorporada pelo Gaia).

## Resultado da execucao da Secao A (probe read-only, com ROLLBACK)

Executada contra `fuga_jales_local_desenvolvimento` em 2026-06-20 via probe Npgsql somente-leitura
(transacao + ROLLBACK; nenhum dado alterado).

- **Sem EXCEPTION** (integridade OK): admin com BCrypt valido; no maximo um setor padrao ativo por
  usuario; FKs criticas do fluxo NOVO (entrada_produto_item/_pesagem) e de auditoria/log SAP NAO
  usam ON DELETE CASCADE.
- **WARNINGS (postura, para Secao B):**
  - conexao usa superusuario `postgres`; nenhuma role de aplicacao dedicada com USAGE no schema;
  - `INTEGRACAO_SAP_ATIVA = true` (recomendacao: `false` no DEV);
  - cascades de agregado a revisar: `hu_caixa_integracao_sap`, `hu_palete_integracao_sap`,
    `integracao_sap_int012_mensagem`, `integracao_sap_int012_reprocessamento`,
    `pesagem_entrada_item_leitura` (fluxo legado, deprecado no H21).

Nota de classificacao: `pesagem_entrada_item(_leitura)` e cascade de agregado do fluxo LEGADO;
ficou em A5b (WARNING/revisao), nao em A5 (EXCEPTION), que cobre o fluxo novo + auditoria/log SAP.

## Achados no baseline atual

1. **Sem transacao global no instalador.** O `000_execucao_completa` roda statement-by-statement;
   uma falha no meio deixa schema parcial.
2. **`date_trunc('minute', now())` ainda presente** em defaults de cadastros e no bootstrap 001
   (`usuario_atualizado_em`, `usuario_perfil_atualizado_em`). A funcao generica e os eventos
   prioritarios ja foram tratados no incremental 024/H14.
3. **Bootstrap nao usa `clock_timestamp()`** nos UPDATEs do admin.
4. **Sem roles/grants explicitos.** Tudo depende do dono/superusuario; a aplicacao conecta como
   `postgres` (superusuario) — postura insegura.
5. **`INTEGRACAO_SAP_ATIVA` divergente:** o seed do 000 grava `false`, mas o validador 002 exige
   `true`. Alem da divergencia, manter `true` por padrao no DEV e arriscado (escrita SAP em
   ambiente compartilhado — ver C12/C13).
6. **`usuario_setor.setor_padrao`** nao tem garantia de "um unico padrao ativo por usuario".
7. **FKs criticas** (rastreabilidade/auditoria) precisam de garantia explicita de que NAO usam
   `ON DELETE CASCADE` (preservacao de historico).

## Validacoes propostas (Secao A - seguras)

| Validacao | Severidade | Regra |
|---|---|---|
| A1 superusuario | WARNING | conexao nao deve ser superusuaria nem `postgres` |
| A2 role/grants | WARNING | deve existir role nao-superusuaria com USAGE no schema |
| A3 admin BCrypt | EXCEPTION | `admin.senha_hash` deve casar `^\$2[aby]\$NN\$...` (60 chars) |
| A4 setor padrao unico | EXCEPTION | nenhum usuario com >1 `setor_padrao` ativo |
| A5 FK critica sem cascade | EXCEPTION | sem `ON DELETE CASCADE` em entrada_produto_item/_pesagem, auditoria_acao_usuario, log_alteracao_cadastral, pesagem_entrada_item(_leitura) |
| A6 INTEGRACAO_SAP_ATIVA | WARNING | recomenda `false` no DEV e alinhamento seed/validador |

WARNING = postura de implantacao (nao aborta). EXCEPTION = integridade de schema/dados (aborta).

## Mudancas estruturais propostas (Secao B - aprovar antes de aplicar)

- **B1 Transacao global** no `000` (`BEGIN … COMMIT` envolvendo todo o instalador).
- **B2 Bootstrap** `001`: trocar `date_trunc('minute', now())` por `clock_timestamp()`.
- **B3 Remocao gradual de `date_trunc`** nos `*_criado_em` de cadastros remanescentes.
- **B4 Role de aplicacao** dedicada (`fugapet_dev_app`, nao-superusuaria) + grants explicitos
  (SELECT/INSERT/UPDATE; DELETE so onde a regra exigir) e troca da connection string.
- **B5 `INTEGRACAO_SAP_ATIVA`** padrao `false` no DEV; ativar `true` apenas em janela de teste,
  alinhando o validador 002.

## Avaliacao: INTEGRACAO_SAP_ATIVA deve permanecer true no DEV?

**Recomendacao: nao.** Padrao `false` no desenvolvimento. Justificativa:

- a config SAP hoje e compartilhada e aponta para producao (pendencia conhecida);
- a escrita SAP ja e controlada e separada (C12) e a config malformada e bloqueada (C13);
- ativar a integracao por padrao aumenta a chance de chamada indevida.

Deixar `false` por padrao e ligar `true` apenas em testes controlados de homologacao, ajustando
o validador 002 para nao exigir `true` incondicionalmente.

## Impacto

- **Secao A:** nenhum (somente leitura). Pode rodar pos-instalacao a qualquer momento.
- **B1/B2/B3:** afetam apenas defaults/estrutura de instalacao e novos registros; nao reescrevem
  timestamps historicos; nao recriam indices (colunas continuam `timestamptz`).
- **B4:** muda a identidade de conexao da aplicacao; exige criar a role, conceder grants e
  atualizar a connection string. Sem impacto em dados.
- **B5:** muda o comportamento padrao da integracao no DEV; alinhar com o validador 002.

## Ordem segura de aplicacao

1. Rodar a **Secao A** (validacao) no banco atual e revisar EXCEPTIONS/WARNINGS.
2. Aprovar a **Secao B** item a item.
3. Aplicar **B4** (role + grants) e validar a aplicacao com a nova role (sem superusuario).
4. Aplicar **B5** (`INTEGRACAO_SAP_ATIVA=false`) e ajustar o validador 002.
5. Incorporar **B1/B2/B3** ao baseline (transacao global, bootstrap e defaults) na proxima
   consolidacao do baseline.
6. Reexecutar a **Secao A** para confirmar a postura endurecida.

## Pontos para parecer do Gaia

1. Aprovar a transacao global no instalador 000.
2. Aprovar `clock_timestamp()` no bootstrap e a remocao gradual de `date_trunc`.
3. Aprovar a role de aplicacao + grants e a remocao do uso de `postgres`/superusuario.
4. Confirmar a regra de "um unico setor padrao ativo por usuario".
5. Confirmar que nenhuma FK critica deve usar `ON DELETE CASCADE`.
6. Decidir o padrao de `INTEGRACAO_SAP_ATIVA` no DEV e alinhar o validador 002.

## Execucao

Nao executar automaticamente. A aplicacao pertence ao usuario responsavel ou ao Gaia Dados.
A Secao A e segura (somente leitura). A Secao B so deve ser aplicada apos aprovacao, sob
transacao controlada, com probe/validacao posterior.
