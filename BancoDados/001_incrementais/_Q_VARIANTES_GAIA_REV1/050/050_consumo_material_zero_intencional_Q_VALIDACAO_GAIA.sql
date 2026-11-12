\set ON_ERROR_STOP on
-- BEGIN GAIA_Q_OWNER_APP_GUARD
DO $gaia_q_050_guard$
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_DATABASE atual=% esperado=fuga_jales_local_homologacao_q_v1_2',
            current_database();
    END IF;

    IF current_user <> 'fugapet_q_owner' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_CURRENT_USER atual=% esperado=fugapet_q_owner',
            current_user;
    END IF;

    IF session_user = 'fugapet_q_app' THEN
        RAISE EXCEPTION
            'FUGAPET_Q_050_ABORT_APP_NAO_PODE_EXECUTAR_MIGRATION';
    END IF;
END
$gaia_q_050_guard$;
-- END GAIA_Q_OWNER_APP_GUARD
-- ============================================================================
-- 050_consumo_material_zero_intencional_Q_VALIDACAO_GAIA.sql
-- Projeto: FugaPET Q
-- Schema: homologacao
-- Validacao pos-instalacao da migration 050.
--
-- READ-ONLY ABSOLUTO.
-- Nenhum DDL/DML.
-- Termina obrigatoriamente em ROLLBACK.
-- ============================================================================

BEGIN;
SET TRANSACTION READ ONLY;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '2min';
SET LOCAL search_path = pg_catalog, homologacao;

SELECT '============================================================';
SELECT 'FUGAPET|VALIDACAO=050_CONSUMO_MATERIAL_ZERO_INTENCIONAL';
SELECT concat(
    'TRANSACTION_READ_ONLY=',
    current_setting('transaction_read_only')
);

DO $$
DECLARE
    v_total_colunas integer;
    v_seq text;
BEGIN
    IF to_regclass('homologacao.consumo_material_componente_decisao') IS NULL THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: tabela consumo_material_componente_decisao ausente.';
    END IF;

    SELECT count(*)
      INTO v_total_colunas
      FROM information_schema.columns
     WHERE table_schema = 'homologacao'
       AND table_name = 'consumo_material_componente_decisao';

    IF v_total_colunas <> 11 THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: esperado 11 colunas; encontrado %.',
            v_total_colunas;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='codigo_decisao'
          AND data_type='bigint'
          AND is_nullable='NO'
          AND is_identity='YES'
          AND identity_generation='BY DEFAULT'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: codigo_decisao divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='codigo_apontamento'
          AND data_type='bigint'
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: codigo_apontamento divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='numero_reserva'
          AND data_type='character varying'
          AND character_maximum_length=10
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: numero_reserva divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='item_reserva'
          AND data_type='character varying'
          AND character_maximum_length=8
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: item_reserva divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='codigo_material'
          AND data_type='character varying'
          AND character_maximum_length=40
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: codigo_material divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='decisao_operacional'
          AND data_type='character varying'
          AND character_maximum_length=30
          AND is_nullable='NO'
          AND column_default IS NULL
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: decisao_operacional deve ser varchar(30) NOT NULL sem default.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='quantidade'
          AND data_type='numeric'
          AND numeric_precision=18
          AND numeric_scale=3
          AND is_nullable='NO'
          AND column_default IS NULL
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: quantidade deve ser numeric(18,3) NOT NULL sem default.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='unidade'
          AND data_type='character varying'
          AND character_maximum_length=3
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: unidade divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='usuario'
          AND data_type='character varying'
          AND character_maximum_length=120
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: usuario divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='estacao'
          AND data_type='character varying'
          AND character_maximum_length IS NULL
          AND is_nullable='NO'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: estacao divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='homologacao'
          AND table_name='consumo_material_componente_decisao'
          AND column_name='decidido_em'
          AND data_type='timestamp with time zone'
          AND is_nullable='NO'
          AND column_default IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: decidido_em divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND conname='pk_consumo_material_comp_decisao'
          AND contype='p'
          AND pg_get_constraintdef(oid,true)='PRIMARY KEY (codigo_decisao)'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: PK divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND conname='fk_consumo_material_comp_decisao_apontamento'
          AND contype='f'
          AND confrelid='homologacao.operacao_producao_apontamento'::regclass
          AND confdeltype='r'
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: FK apontamento ON DELETE RESTRICT ausente/divergente.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND contype='f'
          AND confrelid='homologacao.consumo_material_item'::regclass
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: FK indevida para consumo_material_item.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND conname='uq_consumo_material_comp_decisao_ap_res_item'
          AND contype='u'
          AND pg_get_constraintdef(oid,true)
              ='UNIQUE (codigo_apontamento, numero_reserva, item_reserva)'
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: UNIQUE apontamento/reserva/item ausente/divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND conname='ck_consumo_material_comp_decisao_decisao'
          AND contype='c'
          AND position(
                'ZERO_INTENCIONAL'
                IN pg_get_constraintdef(oid,true)
              ) > 0
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: CHECK decisao ZERO_INTENCIONAL ausente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND conname='ck_consumo_material_comp_decisao_quantidade'
          AND contype='c'
          AND position('quantidade = 0' IN pg_get_constraintdef(oid,true)) > 0
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: CHECK ZERO_INTENCIONAL -> quantidade 0 ausente.';
    END IF;

    IF (
        SELECT count(*)
        FROM pg_indexes
        WHERE schemaname='homologacao'
          AND tablename='consumo_material_componente_decisao'
    ) <> 2 THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: quantidade de indices divergente; esperado somente PK + UNIQUE.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'SELECT'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: APP sem SELECT.';
    END IF;

    IF NOT has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'INSERT'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: APP sem INSERT.';
    END IF;

    IF has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'UPDATE'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: APP possui UPDATE.';
    END IF;

    IF has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'DELETE'
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: APP possui DELETE.';
    END IF;

    v_seq := pg_get_serial_sequence(
        'homologacao.consumo_material_componente_decisao',
        'codigo_decisao'
    );

    IF v_seq IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 050 FALHOU: sequence/identity ausente.';
    END IF;

    IF NOT has_sequence_privilege('fugapet_q_app', v_seq, 'USAGE')
       OR NOT has_sequence_privilege('fugapet_q_app', v_seq, 'SELECT') THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: APP sem USAGE/SELECT na sequence identity.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM homologacao.consumo_material_componente_decisao
    ) THEN
        RAISE EXCEPTION
            'VALIDACAO 050 FALHOU: tabela deveria iniciar vazia.';
    END IF;

    RAISE NOTICE
        'VALIDACAO 050 OK: estrutura, integridade, ACL append-only e zero rows confirmados.';
END
$$;

SELECT concat(
    'TABLE=homologacao.consumo_material_componente_decisao',
    '|ROWS=',count(*),
    '|ZERO_DECISOES_APONTAMENTO_11=',
       count(*) FILTER (WHERE codigo_apontamento=11)
)
FROM homologacao.consumo_material_componente_decisao;

SELECT concat(
    'COLUNA',
    '|ORDEM=',ordinal_position,
    '|NOME=',column_name,
    '|TIPO=',data_type,
    '|MAXLEN=',COALESCE(character_maximum_length::text,'<NULL>'),
    '|PRECISAO=',COALESCE(numeric_precision::text,'<NULL>'),
    '|ESCALA=',COALESCE(numeric_scale::text,'<NULL>'),
    '|NULLABLE=',is_nullable,
    '|DEFAULT=',COALESCE(column_default,'<NULL>'),
    '|IDENTITY=',is_identity
)
FROM information_schema.columns
WHERE table_schema='homologacao'
  AND table_name='consumo_material_componente_decisao'
ORDER BY ordinal_position;

SELECT concat(
    'CONSTRAINT',
    '|NOME=',conname,
    '|TIPO=',contype,
    '|VALIDADA=',convalidated,
    '|DEFINICAO=',pg_get_constraintdef(oid,true)
)
FROM pg_constraint
WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
ORDER BY contype,conname;

SELECT concat(
    'INDEX',
    '|NOME=',indexname,
    '|DEFINICAO=',indexdef
)
FROM pg_indexes
WHERE schemaname='homologacao'
  AND tablename='consumo_material_componente_decisao'
ORDER BY indexname;

SELECT concat(
    'ACL_APP',
    '|SELECT=',has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'SELECT'
    ),
    '|INSERT=',has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'INSERT'
    ),
    '|UPDATE=',has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'UPDATE'
    ),
    '|DELETE=',has_table_privilege(
        'fugapet_q_app',
        'homologacao.consumo_material_componente_decisao',
        'DELETE'
    )
);

SELECT concat(
    'CONSUMO_MATERIAL_ITEM_FK=',
    CASE WHEN EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid='homologacao.consumo_material_componente_decisao'::regclass
          AND contype='f'
          AND confrelid='homologacao.consumo_material_item'::regclass
    )
    THEN 'SIM'
    ELSE 'NAO'
    END
);

SELECT concat(
    'TRANSACTION_READ_ONLY_FINAL=',
    current_setting('transaction_read_only')
);

ROLLBACK;

\echo VALIDACAO 050 ENCERRADA COM ROLLBACK
