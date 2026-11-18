\set ON_ERROR_STOP on
-- ============================================================
-- 065_vinculo_produto_etiqueta_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 065 - Vinculo de produto acabado (centro 3007) a etiqueta e processo
-- PREFLIGHT (SOMENTE LEITURA) - nao altera nada
--
-- Prova, ANTES de escrever, que: o alvo e o banco certo, a tabela ainda nao existe,
-- a tabela etiqueta (referenciada pela FK) existe, e o papel de aplicacao existe.
-- ============================================================

\echo '== 065 PREFLIGHT =='

SELECT current_database() AS banco,
       current_schema()   AS schema_corrente,
       current_user       AS usuario;

\echo '-- a tabela ja existe? (esperado: f)'
SELECT to_regclass('desenvolvimento.produto_etiqueta_vinculo') IS NOT NULL AS tabela_ja_existe;

\echo '-- dependencia: tabela etiqueta (esperado: t)'
SELECT to_regclass('desenvolvimento.etiqueta') IS NOT NULL AS etiqueta_existe;

\echo '-- coluna da chave de etiqueta (esperado: 1 linha, bigint)'
SELECT column_name, data_type
  FROM information_schema.columns
 WHERE table_schema='desenvolvimento' AND table_name='etiqueta' AND column_name='codigo_etiqueta';

\echo '-- papel de aplicacao (esperado: t)'
SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') AS papel_existe;

\echo '-- quantas etiquetas cadastradas hoje (contexto: o usuario informou 1)'
SELECT count(*) AS etiquetas FROM desenvolvimento.etiqueta;
