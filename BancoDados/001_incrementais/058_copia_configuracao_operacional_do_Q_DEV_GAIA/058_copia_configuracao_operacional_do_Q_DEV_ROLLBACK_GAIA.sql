\set ON_ERROR_STOP on
-- ============================================================
-- 058_copia_configuracao_operacional_do_Q_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 058 - Rollback
--
-- ROLLBACK_EXECUTION = NAO_AUTORIZADO por padrao. Nao executar sem autorizacao
-- expressa.
--
-- Este e o unico rollback desta serie que faz DELETE, e por isso as guardas sao as
-- mais estreitas: ele so aceita reverter o estado que o 058 deixou, EXATAMENTE como
-- deixou. Qualquer linha a mais, qualquer apontamento que referencie um perfil, ou
-- qualquer sinal de uso posterior, e o rollback ABORTA -- apagar configuracao de
-- roteamento em uso nao e reversivel.
--
-- Ordem obrigatoria: definicao -> perfil -> configuracao, pelas FKs em RESTRICT.
-- As configs 5 e 6 NAO sao apagadas: elas sao anteriores ao 058 e voltam ao estado
-- do incremento 040, com centro e centro_trabalho vazios.
--
--   NAO SAP. Nenhuma sequence e movida para tras: ids ja usados nao sao reciclados.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

DO $$
DECLARE
    v_ids bigint[];
    v_n   bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '058 ROLLBACK: database incorreto: %', current_database();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '058 ROLLBACK: principal administrativo inesperado: %', current_user;
    END IF;

    -- -------- O estado tem de ser exatamente o que o 058 deixou --------
    SELECT array_agg(codigo_configuracao ORDER BY codigo_configuracao)
      INTO v_ids FROM desenvolvimento.operacao_producao_configuracao;
    IF v_ids IS DISTINCT FROM ARRAY[5,6,7,8,9,10,11,12,13,14]::bigint[] THEN
        RAISE EXCEPTION
            '058 ROLLBACK: configs atuais = %, esperado exatamente 5..14. Houve mudanca posterior; rollback bloqueado.',
            coalesce(array_to_string(v_ids, ','), '<nenhuma>');
    END IF;

    SELECT array_agg(codigo_perfil_resultado ORDER BY codigo_perfil_resultado)
      INTO v_ids FROM desenvolvimento.operacao_resultado_perfil;
    IF v_ids IS DISTINCT FROM ARRAY[1,2,3,4,5,6,7]::bigint[] THEN
        RAISE EXCEPTION
            '058 ROLLBACK: perfis atuais = %, esperado exatamente 1..7. Rollback bloqueado.',
            coalesce(array_to_string(v_ids, ','), '<nenhum>');
    END IF;

    SELECT array_agg(codigo_definicao ORDER BY codigo_definicao)
      INTO v_ids FROM desenvolvimento.operacao_resultado_definicao;
    IF v_ids IS DISTINCT FROM ARRAY[1,2,3,4,5,6]::bigint[] THEN
        RAISE EXCEPTION
            '058 ROLLBACK: definicoes atuais = %, esperado exatamente 1..6. Rollback bloqueado.',
            coalesce(array_to_string(v_ids, ','), '<nenhuma>');
    END IF;

    -- -------- Nada pode estar em uso --------
    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_producao_apontamento WHERE codigo_perfil_resultado IS NOT NULL'
      INTO v_n;
    IF v_n <> 0 THEN
        RAISE EXCEPTION
            '058 ROLLBACK: % apontamentos referenciam perfil de resultado; rollback bloqueado para nao apagar configuracao em uso.',
            v_n;
    END IF;

    -- -------- As configs 5 e 6 nao podem ter sido alteradas por terceiros --------
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao IN (5, 6)
       AND alterado_por = 'MIGRATION_058_COPIA_CONFIG_Q';
    IF v_n <> 2 THEN
        RAISE EXCEPTION
            '058 ROLLBACK: as configs 5 e 6 nao tem a marca de alteracao do 058 (encontradas %); alguem alterou depois. Rollback bloqueado.',
            v_n;
    END IF;

    RAISE NOTICE 'GUARDAS=OK  estado identico ao que o 058 deixou';
END
$$;

-- ============================================================
-- 1. Indice criado pelo 058
-- ============================================================
DROP INDEX desenvolvimento.uq_operacao_config_ativa_plant_wc;

-- ============================================================
-- 2. Dados, na ordem imposta pelas FKs em RESTRICT
-- ============================================================

DO $$
DECLARE
    v_afetadas bigint;
BEGIN
    DELETE FROM desenvolvimento.operacao_resultado_definicao
     WHERE codigo_definicao BETWEEN 1 AND 6;
    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 6 THEN
        RAISE EXCEPTION '058 ROLLBACK: esperadas 6 definicoes removidas, obtidas %.', v_afetadas;
    END IF;

    DELETE FROM desenvolvimento.operacao_resultado_perfil
     WHERE codigo_perfil_resultado BETWEEN 1 AND 7;
    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 7 THEN
        RAISE EXCEPTION '058 ROLLBACK: esperados 7 perfis removidos, obtidos %.', v_afetadas;
    END IF;

    DELETE FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao BETWEEN 7 AND 14;
    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 8 THEN
        RAISE EXCEPTION '058 ROLLBACK: esperadas 8 configs removidas, obtidas %.', v_afetadas;
    END IF;

    -- As configs 5 e 6 voltam ao estado do incremento 040.
    UPDATE desenvolvimento.operacao_producao_configuracao
       SET centro          = '',
           centro_trabalho = '',
           alterado_em     = NULL,
           alterado_por    = NULL
     WHERE codigo_configuracao IN (5, 6);
    GET DIAGNOSTICS v_afetadas = ROW_COUNT;
    IF v_afetadas <> 2 THEN
        RAISE EXCEPTION '058 ROLLBACK: esperadas 2 configs revertidas, obtidas %.', v_afetadas;
    END IF;

    RAISE NOTICE 'REVERTIDO=6 definicoes, 7 perfis, 8 configs removidas; 5 e 6 de volta ao estado do 040';
END
$$;

-- ============================================================
-- 2B. Restaurar as CHECKs originais do DEV
--     Seguro somente porque o DML acima ja removeu as linhas que dependiam da lista
--     ampliada: sem elas, nenhuma linha restante usa RESULTADO_APONTAMENTO.
-- ============================================================

DO $$
DECLARE
    v_n bigint;
BEGIN
    SELECT count(*) INTO v_n
      FROM desenvolvimento.operacao_producao_configuracao
     WHERE tipo_processo IN ('RESULTADO_APONTAMENTO', 'CONSUMO_SEMI_ACABADO');
    IF v_n <> 0 THEN
        RAISE EXCEPTION
            '058 ROLLBACK: restam % linhas com tipo_processo que a CHECK original do DEV nao permite; restauracao bloqueada.',
            v_n;
    END IF;
END
$$;

ALTER TABLE desenvolvimento.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_plant_wc_consistente;

ALTER TABLE desenvolvimento.operacao_producao_configuracao
    DROP CONSTRAINT ck_operacao_config_tipo_processo;

-- Definicao original do DEV, como medida antes do 058.
ALTER TABLE desenvolvimento.operacao_producao_configuracao
    ADD CONSTRAINT ck_operacao_config_tipo_processo
    CHECK (
        (tipo_processo)::text = ANY (ARRAY[
            'CONSUMO_MATERIA_PRIMA'::character varying,
            'CONSUMO_QUIMICOS'::character varying,
            'SEMI_ACABADO'::character varying,
            'PRODUTO_ACABADO'::character varying,
            'SEM_DESTINO_CONFIGURADO'::character varying
        ]::text[])
    );

-- ============================================================
-- 3. POST-CHECKS
-- ============================================================

DO $$
DECLARE
    v_ids bigint[];
    v_n   bigint;
BEGIN
    SELECT array_agg(codigo_configuracao ORDER BY codigo_configuracao)
      INTO v_ids FROM desenvolvimento.operacao_producao_configuracao;
    IF v_ids IS DISTINCT FROM ARRAY[5, 6]::bigint[] THEN
        RAISE EXCEPTION '058 ROLLBACK POSTCHECK: configs resultantes = %, esperado 5 e 6.', array_to_string(v_ids, ',');
    END IF;

    SELECT count(*) INTO v_n FROM desenvolvimento.operacao_producao_configuracao
     WHERE codigo_configuracao IN (5, 6)
       AND btrim(centro) = '' AND btrim(centro_trabalho) = ''
       AND alterado_em IS NULL AND alterado_por IS NULL;
    IF v_n <> 2 THEN
        RAISE EXCEPTION '058 ROLLBACK POSTCHECK: configs 5 e 6 nao voltaram ao estado do 040 (% de 2).', v_n;
    END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_perfil' INTO v_n;
    IF v_n <> 0 THEN RAISE EXCEPTION '058 ROLLBACK POSTCHECK: perfil com % linhas.', v_n; END IF;

    EXECUTE 'SELECT count(*) FROM desenvolvimento.operacao_resultado_definicao' INTO v_n;
    IF v_n <> 0 THEN RAISE EXCEPTION '058 ROLLBACK POSTCHECK: definicao com % linhas.', v_n; END IF;

    IF EXISTS (
        SELECT 1 FROM pg_indexes
         WHERE schemaname = 'desenvolvimento' AND indexname = 'uq_operacao_config_ativa_plant_wc'
    ) THEN
        RAISE EXCEPTION '058 ROLLBACK POSTCHECK: indice ainda presente.';
    END IF;

    RAISE NOTICE 'ROLLBACK=CONCLUIDO  estado de partida do 058 restaurado';
    RAISE NOTICE 'NOTA=as sequences de identity NAO foram recuadas: ids ja usados nao sao reciclados';
END
$$;

COMMIT;

\echo '058_ROLLBACK_CONCLUIDO'
\echo 'CONFIGS_RESTANTES=5, 6 (estado do incremento 040)'
\echo 'PERFIS=0  DEFINICOES=0'
\echo 'INDICE_REMOVIDO=uq_operacao_config_ativa_plant_wc'
\echo 'SEQUENCES_RECUADAS=NAO'
\echo 'SAP_WRITE=0'
