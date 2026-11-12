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
-- FugaPET - Incremental 051 - Q
-- operacao_producao_evento.resultado: varchar(40) -> text
-- Artefato: PREFLIGHT READ-ONLY FAIL-CLOSED
-- Banco esperado: fuga_jales_local_homologacao_q_v1_2
-- Schema esperado: homologacao
-- Responsavel: Michael Richard
-- Agente: Gaia V4
-- Gate: 050-E2E-OP1002101-0070-EVENT-RESULT-TEXT-MIGRATION-DESIGN-01
-- DOCUMENTAL. NAO EXECUTAR AUTOMATICAMENTE.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='2min';
SET LOCAL search_path=pg_catalog,homologacao;

DO $gaia$
DECLARE v_type text; v_nullable text; v_default text;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN RAISE EXCEPTION '051_PREFLIGHT_BANCO_INCORRETO'; END IF;
    IF to_regnamespace('homologacao') IS NULL THEN RAISE EXCEPTION '051_PREFLIGHT_SCHEMA_AUSENTE'; END IF;
    IF to_regclass('homologacao.operacao_producao_evento') IS NULL THEN RAISE EXCEPTION '051_PREFLIGHT_TABELA_AUSENTE'; END IF;

    SELECT data_type || CASE WHEN data_type='character varying' THEN '(' || character_maximum_length::text || ')' ELSE '' END,
           is_nullable, column_default
      INTO v_type,v_nullable,v_default
      FROM information_schema.columns
     WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado';

    IF v_type IS NULL THEN RAISE EXCEPTION '051_PREFLIGHT_COLUNA_AUSENTE'; END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado'
                 AND data_type='text' AND is_nullable='NO' AND regexp_replace(coalesce(column_default,''),'[[:space:]]','','g') = regexp_replace(quote_literal(''::text) || '::text','[[:space:]]','','g')) THEN
        RAISE NOTICE '051_PREFLIGHT_CLASSIFICACAO=JA_APLICADO';
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado'
                     AND data_type='character varying' AND character_maximum_length=40 AND is_nullable='NO'
                     AND regexp_replace(coalesce(column_default,''),'[[:space:]]','','g') = regexp_replace(quote_literal(''::text) || '::character varying','[[:space:]]','','g')) THEN
        RAISE EXCEPTION '051_PREFLIGHT_CONTRATO_DIVERGENTE type=% nullable=% default=%',v_type,v_nullable,coalesce(v_default,'<NULL>');
    END IF;

    RAISE NOTICE '051_PREFLIGHT_CONTRATO_ATUAL=EXATO';
END
$gaia$;

SELECT concat('051_PREFLIGHT_STATS|ROWS=',count(*)::text,
              '|MAX_LENGTH=',coalesce(max(length(resultado))::text,'<NULL>'),
              '|NULLS=',count(*) FILTER (WHERE resultado IS NULL)::text,
              '|HASH_RESULTADOS=',md5(coalesce(string_agg(codigo_evento_apontamento::text || chr(31) || resultado,
                  chr(30) ORDER BY codigo_evento_apontamento),'')))
FROM homologacao.operacao_producao_evento;
SELECT concat('TRANSACTION_READ_ONLY=',current_setting('transaction_read_only'));
ROLLBACK;
