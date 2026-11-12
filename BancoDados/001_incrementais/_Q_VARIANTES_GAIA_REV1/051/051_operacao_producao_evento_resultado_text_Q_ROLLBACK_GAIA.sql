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
-- Artefato: ROLLBACK DOCUMENTAL FAIL-CLOSED
-- Banco esperado: fuga_jales_local_homologacao_q_v1_2
-- Schema esperado: homologacao
-- Responsavel: Michael Richard
-- Agente: Gaia V4
-- Gate: 050-E2E-OP1002101-0070-EVENT-RESULT-TEXT-MIGRATION-DESIGN-01
-- DOCUMENTAL. NAO EXECUTAR AUTOMATICAMENTE.

BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='2min';
SET LOCAL search_path=pg_catalog,homologacao;

SELECT current_database()='fuga_jales_local_homologacao_q_v1_2' AS db_ok,
       to_regnamespace('homologacao') IS NOT NULL AS schema_ok,
       to_regclass('homologacao.operacao_producao_evento') IS NOT NULL AS table_ok
\gset
\if :db_ok
\else
    \echo '051_ROLLBACK_STOP=BANCO_INCORRETO'
    \quit 3
\endif
\if :schema_ok
\else
    \echo '051_ROLLBACK_STOP=SCHEMA_AUSENTE'
    \quit 3
\endif
\if :table_ok
\else
    \echo '051_ROLLBACK_STOP=TABELA_AUSENTE'
    \quit 3
\endif

SELECT EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado'
                 AND data_type='text' AND is_nullable='NO' AND regexp_replace(coalesce(column_default,''),'[[:space:]]','','g') = regexp_replace(quote_literal(''::text) || '::text','[[:space:]]','','g')) AS contrato_text_exato,
       EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado'
                 AND data_type='character varying' AND character_maximum_length=40 AND is_nullable='NO'
                 AND regexp_replace(coalesce(column_default,''),'[[:space:]]','','g') = regexp_replace(quote_literal(''::text) || '::character varying','[[:space:]]','','g')) AS ja_revertido,
       coalesce(max(length(resultado)),0) <= 40 AS max_length_ok
FROM homologacao.operacao_producao_evento
\gset

\if :ja_revertido
    \echo '051_ROLLBACK_CLASSIFICACAO=JA_REVERTIDO_NOOP'
\elif :contrato_text_exato
    \if :max_length_ok
        ALTER TABLE homologacao.operacao_producao_evento
            ALTER COLUMN resultado TYPE varchar(40);
    \else
        \echo '051_ROLLBACK_STOP=MAX_LENGTH_GT40'
        \quit 3
    \endif
\else
    \echo '051_ROLLBACK_STOP=CONTRATO_DIVERGENTE'
    \quit 3
\endif

SELECT EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='homologacao' AND table_name='operacao_producao_evento' AND column_name='resultado'
                 AND data_type='character varying' AND character_maximum_length=40 AND is_nullable='NO'
                 AND regexp_replace(coalesce(column_default,''),'[[:space:]]','','g') = regexp_replace(quote_literal(''::text) || '::character varying','[[:space:]]','','g')) AS rollback_contract_ok
\gset
\if :rollback_contract_ok
\else
    \echo '051_ROLLBACK_STOP=POSTCHECK_DIVERGENTE'
    \quit 3
\endif
COMMIT;
