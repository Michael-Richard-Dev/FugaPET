# FugaPET - Incremental 038 - Cancelamento local controlado de Semiacabado

Objetivo: permitir cancelar localmente, de forma controlada, lançamentos antigos de Produto Semiacabado em `ERRO_SAP` e sem documento SAP, preservando histórico/pesagens e liberando novo lançamento operacional.

Regras:
- Não executar automaticamente.
- Não altera dados reais fora da ação operacional futura da tela.
- Não remove pesagens nem etiquetas históricas.
- Não toca na integração SAP 101 já validada.
- Rollback não faz parte do fluxo normal.

Ordem sugerida:
1. `psql -v ON_ERROR_STOP=1 -f 038_cancelamento_lancamento_semi_acabado_DEV_PREFLIGHT_GAIA.sql`
2. `psql -v ON_ERROR_STOP=1 -f 038_cancelamento_lancamento_semi_acabado_DEV_PROPOSTA_GAIA.sql`
3. `psql -v ON_ERROR_STOP=1 -f 038_cancelamento_lancamento_semi_acabado_DEV_VALIDACAO_GAIA.sql`
4. Repetir em HML somente após aprovação DEV e backup HML.

Objetos criados/alterados:
- Colunas: `cancelado_em`, `cancelado_por`, `motivo_cancelamento`.
- Status permitido: adiciona `CANCELADO` a `ck_semi_acabado_lancamento_status`.
- Check: `ck_semi_acabado_lancamento_cancelamento_local`.
- Índice aberto continua excluindo `CANCELADO`.
