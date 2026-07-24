-- ============================================================
-- 034_etiqueta_validacao_GAIA.sql
-- PROPOSTA - NAO EXECUTAR
-- Validacao pos-proposta 034 - Cadastro de Etiqueta
-- ============================================================

SET search_path TO desenvolvimento;

SELECT conname, pg_get_constraintdef(oid) AS definicao
  FROM pg_constraint
 WHERE conrelid = 'desenvolvimento.etiqueta'::regclass
   AND conname IN (
       'ck_etiqueta_codigo_tamanho',
       'ck_etiqueta_nome_tamanho',
       'ck_etiqueta_descricao_tamanho'
   )
 ORDER BY conname;

SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'desenvolvimento'
   AND tablename = 'etiqueta'
   AND indexname = 'uq_etiqueta_codigo_interno_global'
   AND indexdef ILIKE '%UNIQUE%'
   AND indexdef ILIKE '%upper(trim(codigo_interno))%'
   AND indexdef NOT ILIKE '%WHERE%';

SELECT tgname
  FROM pg_trigger
 WHERE tgrelid = 'desenvolvimento.etiqueta'::regclass
   AND tgname = 'trg_bloqueia_inativar_etiqueta_com_produto_ativo'
   AND NOT tgisinternal;

SELECT upper(trim(codigo_interno)) AS codigo_interno_normalizado, count(*) AS quantidade
  FROM desenvolvimento.etiqueta
 GROUP BY upper(trim(codigo_interno))
HAVING count(*) > 1;

SELECT codigo_etiqueta
  FROM desenvolvimento.etiqueta
 WHERE coalesce(trim(codigo_interno), '') = ''
    OR char_length(trim(codigo_interno)) > 80
    OR char_length(trim(nome_etiqueta)) < 2
    OR char_length(trim(nome_etiqueta)) > 80
    OR char_length(coalesce(trim(descricao_etiqueta), '')) > 255;
