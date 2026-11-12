-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_owner_app_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_DATABASE_INCORRETO atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_CURRENT_USER_INCORRETO atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_RUNTIME_APP_NAO_PODE_EXECUTAR_DDL';
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION
            'FUGAPET_Q_ABORT_SCHEMA_HOMOLOGACAO_AUSENTE';
    END IF;
END
$gaia_q_owner_app_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================
-- 027_h27_cadastro_cargo_HML_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_HML
-- Banco PostgreSQL - Banco alvo: fuga_jales_local_homologacao_q_v1_2
-- Schema alvo: homologacao
-- Objeto: Cadastro de Cargo
--
-- OBJETIVO
--   Validar dados atuais antes da aplicacao do incremental 027 em HML.
--   Este script NAO altera estrutura nem dados.
--
-- DECISAO FUNCIONAL APROVADA
--   - nome_cargo: minimo 2 e maximo 80 caracteres uteis apos trim.
--   - descricao_cargo: maximo 255 caracteres uteis apos trim.
--   - cargo em uso por usuario ativo nao pode ser inativado.
--   - aplicacao deve validar antes, mas o banco deve bloquear bypass por SQL direto.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.schemata WHERE schema_name = 'homologacao'
    ) THEN
        RAISE EXCEPTION 'Schema homologacao nao existe.';
    END IF;

    IF to_regclass('homologacao.cargo') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.cargo nao existe.';
    END IF;

    IF to_regclass('homologacao.usuario') IS NULL THEN
        RAISE EXCEPTION 'Tabela homologacao.usuario nao existe.';
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.cargo
     WHERE nome_cargo IS NULL
        OR char_length(trim(nome_cargo)) < 2
        OR char_length(trim(nome_cargo)) > 80;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight 027 HML reprovado: % cargos com nome_cargo fora de 2..80.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.cargo
     WHERE descricao_cargo IS NOT NULL
       AND char_length(trim(descricao_cargo)) > 255;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight 027 HML reprovado: % cargos com descricao_cargo maior que 255.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM (
            SELECT upper(trim(nome_cargo))
              FROM homologacao.cargo
             WHERE situacao_cargo = true
             GROUP BY upper(trim(nome_cargo))
            HAVING count(*) > 1
      ) x;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight 027 HML reprovado: % grupos de cargos ativos duplicados por upper(trim(nome_cargo)).', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.usuario u
      JOIN homologacao.cargo c ON c.codigo_cargo = u.codigo_cargo
     WHERE u.situacao_usuario = true
       AND c.situacao_cargo = false;
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Preflight 027 HML reprovado: % usuarios ativos vinculados a cargos inativos.', v_qtd;
    END IF;
END $$;

SELECT 'nome_cargo_fora_limite_2_80' AS validacao, count(*) AS quantidade
FROM homologacao.cargo
WHERE nome_cargo IS NULL
   OR char_length(trim(nome_cargo)) < 2
   OR char_length(trim(nome_cargo)) > 80
UNION ALL
SELECT 'descricao_cargo_maior_255', count(*)
FROM homologacao.cargo
WHERE descricao_cargo IS NOT NULL
  AND char_length(trim(descricao_cargo)) > 255
UNION ALL
SELECT 'cargos_ativos_duplicados_upper_trim', count(*)
FROM (
    SELECT upper(trim(nome_cargo)) AS nome_normalizado
      FROM homologacao.cargo
     WHERE situacao_cargo = true
     GROUP BY upper(trim(nome_cargo))
    HAVING count(*) > 1
) d
UNION ALL
SELECT 'usuarios_ativos_cargo_inativo', count(*)
FROM homologacao.usuario u
JOIN homologacao.cargo c ON c.codigo_cargo = u.codigo_cargo
WHERE u.situacao_usuario = true
  AND c.situacao_cargo = false;

SELECT 'PREFLIGHT 027 HML APROVADO - SEM INCONSISTENCIAS BLOQUEANTES' AS resultado_preflight;
