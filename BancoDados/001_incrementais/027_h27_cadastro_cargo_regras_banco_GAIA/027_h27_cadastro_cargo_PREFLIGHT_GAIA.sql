-- ============================================================
-- 027_h27_cadastro_cargo_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_Dev
-- Banco PostgreSQL - Schema desenvolvimento
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Validar dados atuais antes da aplicacao do incremental 027.
--   Este script NAO altera estrutura nem dados.
-- ============================================================

SET search_path TO desenvolvimento;

SELECT 'nome_cargo_fora_limite_2_80' AS validacao, count(*) AS quantidade
FROM desenvolvimento.cargo
WHERE nome_cargo IS NULL
   OR char_length(trim(nome_cargo)) < 2
   OR char_length(trim(nome_cargo)) > 80
UNION ALL
SELECT 'descricao_cargo_maior_255', count(*)
FROM desenvolvimento.cargo
WHERE descricao_cargo IS NOT NULL
  AND char_length(trim(descricao_cargo)) > 255
UNION ALL
SELECT 'cargos_ativos_duplicados_upper_trim', count(*)
FROM (
    SELECT upper(trim(nome_cargo)) AS nome_normalizado
      FROM desenvolvimento.cargo
     WHERE situacao_cargo = true
     GROUP BY upper(trim(nome_cargo))
    HAVING count(*) > 1
) d
UNION ALL
SELECT 'usuarios_ativos_cargo_inativo', count(*)
FROM desenvolvimento.usuario u
JOIN desenvolvimento.cargo c ON c.codigo_cargo = u.codigo_cargo
WHERE u.situacao_usuario = true
  AND c.situacao_cargo = false;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento'
    ) THEN
        RAISE EXCEPTION 'Schema desenvolvimento nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.cargo') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.cargo nao existe.';
    END IF;

    IF to_regclass('desenvolvimento.usuario') IS NULL THEN
        RAISE EXCEPTION 'Tabela desenvolvimento.usuario nao existe.';
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.cargo
     WHERE nome_cargo IS NULL
        OR char_length(trim(nome_cargo)) < 2
        OR char_length(trim(nome_cargo)) > 80;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight reprovado: % cargos com nome_cargo fora de 2..80.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.cargo
     WHERE descricao_cargo IS NOT NULL
       AND char_length(trim(descricao_cargo)) > 255;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight reprovado: % cargos com descricao_cargo maior que 255.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM (
            SELECT upper(trim(nome_cargo))
              FROM desenvolvimento.cargo
             WHERE situacao_cargo = true
             GROUP BY upper(trim(nome_cargo))
            HAVING count(*) > 1
      ) x;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight reprovado: % grupos de cargos ativos duplicados por upper(trim(nome_cargo)).', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM desenvolvimento.usuario u
      JOIN desenvolvimento.cargo c ON c.codigo_cargo = u.codigo_cargo
     WHERE u.situacao_usuario = true
       AND c.situacao_cargo = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight reprovado: % usuarios ativos vinculados a cargos inativos.', v_qtd;
    END IF;
END $$;

SELECT 'PREFLIGHT 027 DEV APROVADO - SEM INCONSISTENCIAS BLOQUEANTES' AS resultado_preflight;
