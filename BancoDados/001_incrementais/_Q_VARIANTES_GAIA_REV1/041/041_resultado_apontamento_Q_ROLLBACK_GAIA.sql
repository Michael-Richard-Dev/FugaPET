\set ON_ERROR_STOP on
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
-- ============================================================================
-- 041_resultado_apontamento_DEV_ROLLBACK_GAIA.sql
-- ROLLBACK OFICIAL, SEPARADO E NUNCA AUTOMATICO.
--
-- Exige variavel psql:
--   CONFIRMAR_ROLLBACK_041=EU_CONFIRMO_ROLLBACK_041_DEV
--
-- Bloqueia quando houver qualquer definicao, resultado ou item real, quando
-- configuracoes utilizarem os novos tipos ou quando a estrutura estiver parcial.
-- ============================================================================

\if :{?CONFIRMAR_ROLLBACK_041}
\else
    \echo 'ROLLBACK 041 BLOQUEADO: informe -v CONFIRMAR_ROLLBACK_041=EU_CONFIRMO_ROLLBACK_041_DEV'
    \quit 41
\endif

SELECT :'CONFIRMAR_ROLLBACK_041' = 'EU_CONFIRMO_ROLLBACK_041_DEV' AS confirmacao_ok \gset
\if :confirmacao_ok
\else
    \echo 'ROLLBACK 041 BLOQUEADO: confirmacao explicita incorreta.'
    \quit 42
\endif

SET search_path TO homologacao, public;

BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

DO $$
DECLARE
    v_tabelas integer;
    v_registros bigint;
    v_novos_tipos bigint;
    v_constraint_def text;
    v_tipos text[];
    v_tipos_041 constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'CONSUMO_SEMI_ACABADO',
        'PRODUTO_ACABADO',
        'RESULTADO_APONTAMENTO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION 'ROLLBACK 041: banco incorreto: %. Esperado fuga_jales_local_homologacao_q_v1_2.', current_database();
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION 'ROLLBACK 041: schema homologacao inexistente.';
    END IF;

    SELECT count(*) INTO v_tabelas
    FROM information_schema.tables
    WHERE table_schema = 'homologacao'
      AND table_name IN (
          'operacao_resultado_definicao',
          'operacao_resultado_registro',
          'operacao_resultado_registro_item'
      );

    IF v_tabelas <> 3 THEN
        RAISE EXCEPTION 'ROLLBACK 041: estrutura parcial/incompativel. Existem % de 3 tabelas. Nenhum objeto sera removido.', v_tabelas;
    END IF;

    SELECT
        (SELECT count(*) FROM homologacao.operacao_resultado_definicao)
      + (SELECT count(*) FROM homologacao.operacao_resultado_registro)
      + (SELECT count(*) FROM homologacao.operacao_resultado_registro_item)
    INTO v_registros;

    IF v_registros <> 0 THEN
        RAISE EXCEPTION 'ROLLBACK 041 BLOQUEADO: existem % registros nas tabelas 041. Nenhum dado real sera removido.', v_registros;
    END IF;

    SELECT count(*) INTO v_novos_tipos
    FROM homologacao.operacao_producao_configuracao
    WHERE tipo_processo IN ('CONSUMO_SEMI_ACABADO', 'RESULTADO_APONTAMENTO');

    IF v_novos_tipos <> 0 THEN
        RAISE EXCEPTION 'ROLLBACK 041 BLOQUEADO: existem % configuracoes usando os novos tipos. Remova a dependencia por processo formal antes do rollback.', v_novos_tipos;
    END IF;

    SELECT pg_get_constraintdef(c.oid, true)
      INTO v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    SELECT array_agg(x.match[1] ORDER BY x.match[1]) INTO v_tipos
    FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF v_constraint_def IS NULL OR v_tipos IS DISTINCT FROM v_tipos_041 THEN
        RAISE EXCEPTION 'ROLLBACK 041: constraint atual nao corresponde ao estado 041. Definicao: %.', v_constraint_def;
    END IF;
END $$;

-- Ordem inversa das dependencias. Sem CASCADE para bloquear dependencias externas.
DROP TABLE homologacao.operacao_resultado_registro_item;
DROP TABLE homologacao.operacao_resultado_registro;
DROP TABLE homologacao.operacao_resultado_definicao;

ALTER TABLE homologacao.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_tipo_processo;

ALTER TABLE homologacao.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_tipo_processo
    CHECK (
        tipo_processo IN (
            'CONSUMO_MATERIA_PRIMA',
            'CONSUMO_QUIMICOS',
            'SEMI_ACABADO',
            'PRODUTO_ACABADO',
            'SEM_DESTINO_CONFIGURADO'
        )
    );

DO $$
DECLARE
    v_constraint_def text;
    v_tipos text[];
    v_tipos_antigos constant text[] := ARRAY[
        'CONSUMO_MATERIA_PRIMA',
        'CONSUMO_QUIMICOS',
        'PRODUTO_ACABADO',
        'SEMI_ACABADO',
        'SEM_DESTINO_CONFIGURADO'
    ]::text[];
BEGIN
    IF to_regclass('homologacao.operacao_resultado_definicao') IS NOT NULL
       OR to_regclass('homologacao.operacao_resultado_registro') IS NOT NULL
       OR to_regclass('homologacao.operacao_resultado_registro_item') IS NOT NULL THEN
        RAISE EXCEPTION 'ROLLBACK 041: uma ou mais tabelas 041 permaneceram.';
    END IF;

    SELECT pg_get_constraintdef(c.oid, true)
      INTO v_constraint_def
      FROM pg_constraint c
     WHERE c.conrelid = 'homologacao.operacao_producao_configuracao'::regclass
       AND c.conname = 'ck_operacao_config_tipo_processo'
       AND c.contype = 'c'
       AND c.convalidated = true;

    SELECT array_agg(x.match[1] ORDER BY x.match[1]) INTO v_tipos
    FROM regexp_matches(v_constraint_def, '''([^'']+)''', 'g') AS x(match);

    IF v_constraint_def IS NULL OR v_tipos IS DISTINCT FROM v_tipos_antigos THEN
        RAISE EXCEPTION 'ROLLBACK 041: constraint anterior nao foi restaurada corretamente: %.', v_constraint_def;
    END IF;

    IF to_regclass('homologacao.operacao_producao_configuracao') IS NULL
       OR to_regclass('homologacao.operacao_producao_apontamento') IS NULL
       OR to_regclass('homologacao.operacao_producao_evento') IS NULL THEN
        RAISE EXCEPTION 'ROLLBACK 041: objetos 039 foram afetados indevidamente.';
    END IF;

    RAISE NOTICE 'ROLLBACK 041 OK: somente objetos 041 removidos e constraint anterior restaurada; 039/040 preservados.';
END $$;

COMMIT;
\echo 'OK - ROLLBACK 041 DEV concluido mediante confirmacao explicita'
