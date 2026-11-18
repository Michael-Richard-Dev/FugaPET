\set ON_ERROR_STOP on
-- ============================================================
-- 066_catalogo_produto_centro_DEV_PREFLIGHT_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 066 - PREFLIGHT (SOMENTE LEITURA) - nao altera nada
-- ============================================================

\echo '== 066 PREFLIGHT =='

SELECT current_database() AS banco, current_schema() AS schema_corrente, current_user AS usuario;

\echo '-- a tabela ja existe? (esperado: f)'
SELECT to_regclass('desenvolvimento.produto_catalogo_centro') IS NOT NULL AS tabela_ja_existe;

\echo '-- papel de aplicacao (esperado: t)'
SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') AS papel_existe;

\echo '-- o vinculo (065) existe? e independente, mas convem conferir que a frente esta aplicada'
SELECT to_regclass('desenvolvimento.produto_etiqueta_vinculo') IS NOT NULL AS vinculo_existe;

\echo '-- quantos vinculos ja decididos (eles NAO sao tocados por este incremento)'
SELECT count(*) AS vinculos FROM desenvolvimento.produto_etiqueta_vinculo;
