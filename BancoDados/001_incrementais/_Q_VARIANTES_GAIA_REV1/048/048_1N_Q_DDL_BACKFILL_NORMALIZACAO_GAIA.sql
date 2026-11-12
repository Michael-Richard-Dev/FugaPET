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
-- FugaPET HML - FAST-TRACK CONTROLE DE APONTAMENTOS
-- GATE 048-1N-DDL-PREFLIGHT
-- ARTEFATO PROPOSTO - NAO EXECUTADO
-- DATABASE esperado: fuga_jales_local_homologacao_q_v1_2
-- SCHEMA esperado: homologacao
-- PostgreSQL esperado: 18.x
--
-- BLOCO A: CREATE + BACKFILL deterministico (transacao propria)
-- BLOCO B: normalizacao do apontamento 8 (transacao separada),
--          somente quando explicitamente habilitada em psql com:
--          -v NORMALIZAR_APONTAMENTO_8=1 -v ARES_AGREGADO_0050=PENDENTE
--
-- Nenhum evento historico e reescrito/apagado. Nenhum dado SAP e alterado.
-- Nao existe funcao oficial de reabertura nem trigger na tabela auditada.

\set ON_ERROR_STOP on

-- ============================================================================
-- BLOCO A - DDL + BACKFILL 1:N
-- ============================================================================

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SET LOCAL search_path = pg_catalog, homologacao;

-- A1. PRECHECKS FAIL-CLOSED
DO $precheck$
DECLARE
    v_total_legacy integer;
    v_validos integer;
    v_invalidos integer;
    v_tipo_pk text;
    v_tipo_legado text;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '048-1N ABORT: database inesperado: %', current_database();
    END IF;

    IF current_setting('server_version_num')::integer < 180000
       OR current_setting('server_version_num')::integer >= 190000 THEN
        RAISE EXCEPTION '048-1N ABORT: PostgreSQL esperado 18.x; atual=%', current_setting('server_version');
    END IF;

    IF to_regnamespace('homologacao') IS NULL THEN
        RAISE EXCEPTION '048-1N ABORT: schema homologacao ausente';
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento') IS NULL THEN
        RAISE EXCEPTION '048-1N ABORT: tabela operacao_producao_apontamento ausente';
    END IF;

    IF to_regclass('homologacao.consumo_material_lancamento') IS NULL THEN
        RAISE EXCEPTION '048-1N ABORT: tabela consumo_material_lancamento ausente';
    END IF;

    IF to_regclass('homologacao.semi_acabado_lancamento') IS NULL THEN
        RAISE EXCEPTION '048-1N ABORT: tabela semi_acabado_lancamento ausente';
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento_processo') IS NOT NULL THEN
        RAISE EXCEPTION '048-1N ABORT: objeto operacao_producao_apontamento_processo ja existe';
    END IF;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_tipo_pk
      FROM pg_attribute a
     WHERE a.attrelid = 'homologacao.operacao_producao_apontamento'::regclass
       AND a.attname = 'codigo_apontamento'
       AND a.attnum > 0
       AND NOT a.attisdropped;

    SELECT format_type(a.atttypid, a.atttypmod)
      INTO v_tipo_legado
      FROM pg_attribute a
     WHERE a.attrelid = 'homologacao.operacao_producao_apontamento'::regclass
       AND a.attname = 'codigo_registro_processo'
       AND a.attnum > 0
       AND NOT a.attisdropped;

    IF v_tipo_pk IS DISTINCT FROM 'bigint' OR v_tipo_legado IS DISTINCT FROM 'bigint' THEN
        RAISE EXCEPTION '048-1N ABORT: tipos incompatíveis: PK=% legado=%', v_tipo_pk, v_tipo_legado;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM pg_constraint c
          JOIN pg_namespace n ON n.oid = c.connamespace
         WHERE n.nspname = 'homologacao'
           AND c.conname IN (
               'operacao_producao_apontamento_processo_pkey',
               'fk_op_apontamento_processo_apontamento',
               'uq_op_apontamento_processo_tripla',
               'ck_op_apontamento_processo_tipo_nao_vazio'
           )
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: colisao de nome de constraint';
    END IF;

    SELECT count(*)
      INTO v_total_legacy
      FROM homologacao.operacao_producao_apontamento
     WHERE codigo_registro_processo IS NOT NULL;

    IF v_total_legacy <> 3 THEN
        RAISE EXCEPTION '048-1N ABORT: universo legado mudou; esperado=3 atual=%', v_total_legacy;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento a
         WHERE a.codigo_registro_processo IS NOT NULL
           AND a.tipo_processo IS DISTINCT FROM 'CONSUMO_MATERIA_PRIMA'
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: apareceu tipo legado nao homologado para este backfill';
    END IF;

    -- Snapshot legado esperado pelo preflight: somente as tres triplas abaixo.
    IF NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_apontamento
         WHERE codigo_apontamento = 6
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 59
    ) OR NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_apontamento
         WHERE codigo_apontamento = 8
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 63
    ) OR NOT EXISTS (
        SELECT 1 FROM homologacao.operacao_producao_apontamento
         WHERE codigo_apontamento = 9
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 64
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: triplas legadas esperadas 6/59, 8/63, 9/64 nao conferem';
    END IF;

    WITH legado AS (
        SELECT a.codigo_apontamento,
               a.tipo_processo,
               a.codigo_registro_processo,
               CASE
                 WHEN a.tipo_processo = 'CONSUMO_MATERIA_PRIMA' THEN EXISTS (
                     SELECT 1
                       FROM homologacao.consumo_material_lancamento l
                      WHERE l.codigo_consumo_material_lancamento = a.codigo_registro_processo
                 )
                 WHEN a.tipo_processo = 'SEMI_ACABADO' THEN EXISTS (
                     SELECT 1
                       FROM homologacao.semi_acabado_lancamento s
                      WHERE s.codigo_semi_acabado_lancamento = a.codigo_registro_processo
                 )
                 ELSE false
               END AS valido
          FROM homologacao.operacao_producao_apontamento a
         WHERE a.codigo_registro_processo IS NOT NULL
    )
    SELECT count(*) FILTER (WHERE valido),
           count(*) FILTER (WHERE NOT valido)
      INTO v_validos, v_invalidos
      FROM legado;

    IF v_validos <> 3 OR v_invalidos <> 0 THEN
        RAISE EXCEPTION '048-1N ABORT: backfill nao deterministico; validos=% invalidos=%', v_validos, v_invalidos;
    END IF;
END
$precheck$;

-- A2. CREATE TABLE - DDL CONGELADO
CREATE TABLE homologacao.operacao_producao_apontamento_processo (
    codigo_apontamento_processo BIGINT GENERATED BY DEFAULT AS IDENTITY,
    codigo_apontamento BIGINT NOT NULL,
    tipo_processo TEXT NOT NULL,
    codigo_registro_processo BIGINT NOT NULL,
    criado_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    atualizado_em TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT operacao_producao_apontamento_processo_pkey
        PRIMARY KEY (codigo_apontamento_processo),

    CONSTRAINT fk_op_apontamento_processo_apontamento
        FOREIGN KEY (codigo_apontamento)
        REFERENCES homologacao.operacao_producao_apontamento (codigo_apontamento)
        ON DELETE RESTRICT,

    CONSTRAINT uq_op_apontamento_processo_tripla
        UNIQUE (codigo_apontamento, tipo_processo, codigo_registro_processo),

    CONSTRAINT ck_op_apontamento_processo_tipo_nao_vazio
        CHECK (btrim(tipo_processo) <> '')
);

-- Deliberadamente NAO existe FK polimorfica para codigo_registro_processo.

-- A3. BACKFILL DETERMINISTICO DO LEGADO VALIDADO
INSERT INTO homologacao.operacao_producao_apontamento_processo (
    codigo_apontamento,
    tipo_processo,
    codigo_registro_processo
)
SELECT
    a.codigo_apontamento,
    a.tipo_processo,
    a.codigo_registro_processo
FROM homologacao.operacao_producao_apontamento a
WHERE a.codigo_registro_processo IS NOT NULL
  AND (
      (
          a.tipo_processo = 'CONSUMO_MATERIA_PRIMA'
          AND EXISTS (
              SELECT 1
                FROM homologacao.consumo_material_lancamento l
               WHERE l.codigo_consumo_material_lancamento = a.codigo_registro_processo
          )
      )
      OR
      (
          a.tipo_processo = 'SEMI_ACABADO'
          AND EXISTS (
              SELECT 1
                FROM homologacao.semi_acabado_lancamento s
               WHERE s.codigo_semi_acabado_lancamento = a.codigo_registro_processo
          )
      )
  )
ORDER BY a.codigo_apontamento;

-- A4. POS-CHECKS FAIL-CLOSED
DO $postcheck$
DECLARE
    v_total integer;
BEGIN
    IF to_regclass('homologacao.operacao_producao_apontamento_processo') IS NULL THEN
        RAISE EXCEPTION '048-1N ABORT: tabela nova nao existe apos CREATE';
    END IF;

    SELECT count(*)
      INTO v_total
      FROM homologacao.operacao_producao_apontamento_processo;

    IF v_total <> 3 THEN
        RAISE EXCEPTION '048-1N ABORT: total do backfill esperado=3 atual=%', v_total;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo p
          LEFT JOIN homologacao.operacao_producao_apontamento a
            ON a.codigo_apontamento = p.codigo_apontamento
         WHERE a.codigo_apontamento IS NULL
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: vinculo orfao para apontamento';
    END IF;

    IF EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo p
         WHERE btrim(p.tipo_processo) = ''
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: tipo_processo vazio';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo
         WHERE codigo_apontamento = 6
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 59
    ) OR NOT EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo
         WHERE codigo_apontamento = 8
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 63
    ) OR NOT EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo
         WHERE codigo_apontamento = 9
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 64
    ) THEN
        RAISE EXCEPTION '048-1N ABORT: backfill final nao contem as tres triplas esperadas';
    END IF;
END
$postcheck$;

COMMIT;

-- ============================================================================
-- BLOCO B - NORMALIZACAO DE STATUS DO APONTAMENTO 8
-- ============================================================================
--
-- Evidencia Ares/Hera congelada para habilitacao futura:
-- OP 1001961 / operacao 0050 possui 2 requisitos SAP atuais:
-- 13097/4 material 1000037 = CONCLUIDO
-- 13097/5 material 3500024 = PENDENTE
-- agregado 0050 = PENDENTE
-- Hera decidiu estado-alvo = EM_ANDAMENTO.
--
-- Este bloco fica DESABILITADO por padrao.
-- Para habilitar em psql, exigir simultaneamente:
--   -v NORMALIZAR_APONTAMENTO_8=1
--   -v ARES_AGREGADO_0050=PENDENTE
--
-- O modelo auditado nao possui trigger nem funcao oficial de reabertura.
-- As CHECKs permitem AGUARDANDO_FINALIZACAO -> EM_ANDAMENTO sem apagar
-- resultado_operacional/concluido_operacional_em e com campos de termino NULL.
-- Nao existe exigencia fisica de inserir evento para o UPDATE.

\if :{?NORMALIZAR_APONTAMENTO_8}
  \if :NORMALIZAR_APONTAMENTO_8
    \if :{?ARES_AGREGADO_0050}
      SELECT :'ARES_AGREGADO_0050' = 'PENDENTE' AS ares_agregado_ok \gset
      \if :ares_agregado_ok

BEGIN;

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = pg_catalog, homologacao;

DO $normalizacao$
DECLARE
    v_status_antes text;
    v_resultado_antes text;
    v_concluido_antes timestamptz;
    v_codigo_processo_antes bigint;
    v_terminado_antes timestamptz;
    v_codigo_termino_antes text;
    v_usuario_termino_antes text;
    v_estacao_termino_antes text;
    v_idem_termino_antes text;
    v_eventos_antes integer;
    v_processo_status_antes text;
    v_documento_antes text;
    v_exercicio_antes text;
    v_rows integer;
BEGIN
    IF current_database() <> 'fuga_jales_local_homologacao_q_v1_2' THEN
        RAISE EXCEPTION '048-NORM ABORT: database inesperado: %', current_database();
    END IF;

    IF to_regclass('homologacao.operacao_producao_apontamento_processo') IS NULL THEN
        RAISE EXCEPTION '048-NORM ABORT: relacao 1:N ainda nao existe';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento_processo
         WHERE codigo_apontamento = 8
           AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
           AND codigo_registro_processo = 63
    ) THEN
        RAISE EXCEPTION '048-NORM ABORT: tripla (8, CONSUMO_MATERIA_PRIMA, 63) ausente';
    END IF;

    SELECT a.status,
           a.resultado_operacional,
           a.concluido_operacional_em,
           a.codigo_registro_processo,
           a.terminado_em,
           a.codigo_barras_termino,
           a.usuario_termino,
           a.estacao_termino,
           a.idempotency_key_termino
      INTO v_status_antes,
           v_resultado_antes,
           v_concluido_antes,
           v_codigo_processo_antes,
           v_terminado_antes,
           v_codigo_termino_antes,
           v_usuario_termino_antes,
           v_estacao_termino_antes,
           v_idem_termino_antes
      FROM homologacao.operacao_producao_apontamento a
     WHERE a.codigo_apontamento = 8
       AND a.numero_ordem = '1001961'
       AND a.operacao = '0050'
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION '048-NORM ABORT: apontamento 8 nao encontrado com OP/operacao esperadas';
    END IF;

    IF v_status_antes <> 'AGUARDANDO_FINALIZACAO' THEN
        RAISE EXCEPTION '048-NORM ABORT: status esperado AGUARDANDO_FINALIZACAO; atual=%', v_status_antes;
    END IF;

    IF v_resultado_antes IS DISTINCT FROM 'ConfirmadoSap' OR v_concluido_antes IS NULL THEN
        RAISE EXCEPTION '048-NORM ABORT: evidencia de conclusao operacional local divergente';
    END IF;

    IF v_codigo_processo_antes IS DISTINCT FROM 63 THEN
        RAISE EXCEPTION '048-NORM ABORT: codigo_registro_processo esperado=63 atual=%', v_codigo_processo_antes;
    END IF;

    IF v_terminado_antes IS NOT NULL
       OR v_codigo_termino_antes IS NOT NULL
       OR v_usuario_termino_antes IS NOT NULL
       OR v_estacao_termino_antes IS NOT NULL
       OR v_idem_termino_antes IS NOT NULL THEN
        RAISE EXCEPTION '048-NORM ABORT: campos de termino deixaram de estar NULL';
    END IF;

    SELECT count(*)
      INTO v_eventos_antes
      FROM homologacao.operacao_producao_evento
     WHERE codigo_apontamento = 8;

    IF v_eventos_antes <> 4 THEN
        RAISE EXCEPTION '048-NORM ABORT: historico do apontamento 8 mudou; eventos esperado=4 atual=%', v_eventos_antes;
    END IF;

    SELECT l.status_lancamento,
           l.documento_material_sap,
           l.exercicio_documento_material_sap
      INTO v_processo_status_antes,
           v_documento_antes,
           v_exercicio_antes
      FROM homologacao.consumo_material_lancamento l
     WHERE l.codigo_consumo_material_lancamento = 63;

    IF NOT FOUND THEN
        RAISE EXCEPTION '048-NORM ABORT: processo 63 ausente';
    END IF;

    IF v_processo_status_antes IS DISTINCT FROM 'CONFIRMADO_SAP'
       OR v_documento_antes IS DISTINCT FROM '4900005594'
       OR v_exercicio_antes IS DISTINCT FROM '2026' THEN
        RAISE EXCEPTION '048-NORM ABORT: processo 63/documento SAP divergente do preflight';
    END IF;

    -- UPDATE MINIMO E CONTROLADO.
    -- Preserva deliberadamente:
    -- concluido_operacional_em, resultado_operacional, codigo_registro_processo,
    -- todos os campos de termino e todo historico de eventos.
    UPDATE homologacao.operacao_producao_apontamento
       SET status = 'EM_ANDAMENTO',
           atualizado_em = clock_timestamp()
     WHERE codigo_apontamento = 8
       AND numero_ordem = '1001961'
       AND operacao = '0050'
       AND status = 'AGUARDANDO_FINALIZACAO';

    GET DIAGNOSTICS v_rows = ROW_COUNT;
    IF v_rows <> 1 THEN
        RAISE EXCEPTION '048-NORM ABORT: UPDATE esperado=1 linha atual=%', v_rows;
    END IF;

    -- Pos-checks de preservacao.
    IF NOT EXISTS (
        SELECT 1
          FROM homologacao.operacao_producao_apontamento a
         WHERE a.codigo_apontamento = 8
           AND a.status = 'EM_ANDAMENTO'
           AND a.resultado_operacional IS NOT DISTINCT FROM v_resultado_antes
           AND a.concluido_operacional_em IS NOT DISTINCT FROM v_concluido_antes
           AND a.codigo_registro_processo IS NOT DISTINCT FROM v_codigo_processo_antes
           AND a.terminado_em IS NOT DISTINCT FROM v_terminado_antes
           AND a.codigo_barras_termino IS NOT DISTINCT FROM v_codigo_termino_antes
           AND a.usuario_termino IS NOT DISTINCT FROM v_usuario_termino_antes
           AND a.estacao_termino IS NOT DISTINCT FROM v_estacao_termino_antes
           AND a.idempotency_key_termino IS NOT DISTINCT FROM v_idem_termino_antes
    ) THEN
        RAISE EXCEPTION '048-NORM ABORT: pos-check de preservacao do apontamento falhou';
    END IF;

    IF (SELECT count(*)
          FROM homologacao.operacao_producao_evento
         WHERE codigo_apontamento = 8) <> v_eventos_antes THEN
        RAISE EXCEPTION '048-NORM ABORT: historico de eventos foi alterado';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM homologacao.consumo_material_lancamento l
         WHERE l.codigo_consumo_material_lancamento = 63
           AND l.status_lancamento IS NOT DISTINCT FROM v_processo_status_antes
           AND l.documento_material_sap IS NOT DISTINCT FROM v_documento_antes
           AND l.exercicio_documento_material_sap IS NOT DISTINCT FROM v_exercicio_antes
    ) THEN
        RAISE EXCEPTION '048-NORM ABORT: processo 63/documento SAP foi alterado';
    END IF;
END
$normalizacao$;

COMMIT;

      \else
        \echo 'BLOCO B NAO EXECUTADO: ARES_AGREGADO_0050 deve ser PENDENTE.'
      \endif
    \else
      \echo 'BLOCO B NAO EXECUTADO: variavel ARES_AGREGADO_0050 ausente.'
    \endif
  \else
    \echo 'BLOCO B NAO EXECUTADO: NORMALIZAR_APONTAMENTO_8 nao habilitado.'
  \endif
\else
  \echo 'BLOCO B NAO EXECUTADO: NORMALIZAR_APONTAMENTO_8 ausente.'
\endif

-- FIM DO ARTEFATO.
-- Nao ha INSERT de evento de normalizacao: o modelo fisico auditado nao o exige.
-- Caso Hera congele um novo contrato de auditoria que exija evento corretivo,
-- deve ser preparado um NOVO evento verdadeiro de normalizacao no momento real
-- da execucao, nunca retroativo e nunca reescrevendo os quatro eventos existentes.
