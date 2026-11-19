\set ON_ERROR_STOP on
-- ============================================================
-- 068_processo_pesagem_na_caixa_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 068 - PREFLIGHT (SOMENTE LEITURA) - nao altera nada
-- ============================================================

\echo '== 068 PREFLIGHT =='

SELECT current_database() AS banco, current_user AS usuario;

\echo '-- a coluna ja existe? (esperado: f)'
SELECT EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_schema='desenvolvimento' AND table_name='hu_caixa' AND column_name='processo_pesagem'
) AS coluna_ja_existe;

\echo '-- o indice de hoje, que e o que este incremento troca'
SELECT indexdef FROM pg_indexes
 WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_terminal_ativo';

\echo '-- quantas caixas estao ATIVAS pela regra de hoje (nao confirmadas nem canceladas)'
SELECT count(*) AS ativas_hoje
  FROM desenvolvimento.hu_caixa
 WHERE status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA');

\echo '-- ha terminal com MAIS DE UMA ativa? (esperado: 0 linhas; o indice de hoje impede)'
SELECT upper(btrim(terminal)) AS terminal, count(*) AS ativas
  FROM desenvolvimento.hu_caixa
 WHERE status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA')
 GROUP BY 1 HAVING count(*) > 1 ORDER BY 2 DESC;

\echo '-- privilegios de COLUNA do papel de aplicacao (esta base concede por coluna, nao por tabela)'
SELECT privilege_type, count(*) AS colunas
  FROM information_schema.role_column_grants
 WHERE table_schema='desenvolvimento' AND table_name='hu_caixa' AND grantee='fugapet_dev_app'
 GROUP BY 1 ORDER BY 1;
