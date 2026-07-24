-- ============================================================
-- 034_etiqueta_PREFLIGHT_GAIA.sql
-- PROPOSTA - NAO EXECUTAR
-- Projeto FugaPET_Dev
-- Cadastro de Etiqueta - diagnostico previo
-- ============================================================

SET search_path TO desenvolvimento;

-- Diagnostico de codigo interno duplicado GLOBAL, incluindo ativos e inativos.
SELECT upper(trim(codigo_interno)) AS codigo_interno_normalizado,
       count(*) AS quantidade,
       string_agg(codigo_etiqueta::text, ', ' ORDER BY codigo_etiqueta) AS codigos_etiqueta,
       bool_or(situacao_etiqueta) AS possui_ativo,
       bool_or(NOT situacao_etiqueta) AS possui_inativo
  FROM desenvolvimento.etiqueta
 GROUP BY upper(trim(codigo_interno))
HAVING count(*) > 1
 ORDER BY codigo_interno_normalizado;

-- Diagnostico de registros fora das regras propostas.
SELECT codigo_etiqueta,
       codigo_interno,
       nome_etiqueta,
       tipo_etiqueta,
       descricao_etiqueta,
       situacao_etiqueta
  FROM desenvolvimento.etiqueta
 WHERE coalesce(trim(codigo_interno), '') = ''
    OR char_length(trim(codigo_interno)) > 80
    OR char_length(trim(nome_etiqueta)) < 2
    OR char_length(trim(nome_etiqueta)) > 80
    OR char_length(coalesce(trim(descricao_etiqueta), '')) > 255
 ORDER BY codigo_etiqueta;
