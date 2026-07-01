-- ============================================================
-- 030_consumo_material_persistencia_local_ROLLBACK_GAIA.sql
-- Projeto FugaPET - Schema homologacao
-- Desfaz a PROPOSTA 030. Remove as 3 tabelas (CASCADE remove indices/triggers/FKs).
-- AVISO: APAGA os dados de consumo local. Executar somente com autorizacao Gaia Dados.
-- ============================================================

SET search_path TO homologacao;

BEGIN;

DROP TABLE IF EXISTS homologacao.consumo_material_pesagem CASCADE;
DROP TABLE IF EXISTS homologacao.consumo_material_item CASCADE;
DROP TABLE IF EXISTS homologacao.consumo_material_lancamento CASCADE;

COMMIT;

SELECT 'ROLLBACK 030 (homologacao) CONCLUIDO - TABELAS DE CONSUMO LOCAL REMOVIDAS' AS resultado_rollback;
