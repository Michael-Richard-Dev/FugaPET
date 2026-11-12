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
-- ============================================================
-- 037_semi_acabado_HML_VALIDACAO_GAIA.sql
-- Projeto FugaPET_HML | Schema: homologacao
--
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- Validacao estrutural e funcional. Testes transacionais terminam com ROLLBACK.
-- ============================================================

SET search_path TO homologacao;

-- ============================================================
-- 1. VALIDACAO ESTRUTURAL
-- ============================================================
DO $$
DECLARE
    v_problemas text;
    v_comentario text := 'FugaPET incremental 037 semi_acabado';
BEGIN
    IF to_regclass('homologacao.semi_acabado_lancamento') IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 037: tabela semi_acabado_lancamento ausente.';
    END IF;

    IF to_regclass('homologacao.semi_acabado_pesagem') IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO 037: tabela semi_acabado_pesagem ausente.';
    END IF;

    IF obj_description('homologacao.semi_acabado_lancamento'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
        RAISE EXCEPTION 'VALIDACAO 037: comentario de propriedade ausente/divergente em semi_acabado_lancamento.';
    END IF;

    IF obj_description('homologacao.semi_acabado_pesagem'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
        RAISE EXCEPTION 'VALIDACAO 037: comentario de propriedade ausente/divergente em semi_acabado_pesagem.';
    END IF;

    WITH esperado(tabela, coluna, tipo, anulavel) AS (
        VALUES
            ('semi_acabado_lancamento','codigo_semi_acabado_lancamento','bigint','NO'),
            ('semi_acabado_lancamento','numero_ordem','character varying','NO'),
            ('semi_acabado_lancamento','material_produzido','character varying','NO'),
            ('semi_acabado_lancamento','descricao_material','character varying','NO'),
            ('semi_acabado_lancamento','centro','character varying','NO'),
            ('semi_acabado_lancamento','deposito_destino','character varying','NO'),
            ('semi_acabado_lancamento','item_ordem','character varying','NO'),
            ('semi_acabado_lancamento','lote','character varying','NO'),
            ('semi_acabado_lancamento','unidade','character varying','NO'),
            ('semi_acabado_lancamento','quantidade_planejada_kg','numeric','NO'),
            ('semi_acabado_lancamento','quantidade_entregue_kg','numeric','NO'),
            ('semi_acabado_lancamento','quantidade_pendente_kg','numeric','NO'),
            ('semi_acabado_lancamento','peso_liquido_total_kg','numeric','NO'),
            ('semi_acabado_lancamento','status_lancamento','character varying','NO'),
            ('semi_acabado_lancamento','material_document','character varying','YES'),
            ('semi_acabado_lancamento','material_document_year','character varying','YES'),
            ('semi_acabado_lancamento','mensagem_erro_sap','text','YES'),
            ('semi_acabado_lancamento','payload_preview_json','text','NO'),
            ('semi_acabado_lancamento','usuario_operacao','character varying','NO'),
            ('semi_acabado_lancamento','enviado_sap_em','timestamp with time zone','YES'),
            ('semi_acabado_lancamento','criado_em','timestamp with time zone','NO'),
            ('semi_acabado_lancamento','atualizado_em','timestamp with time zone','NO'),
            ('semi_acabado_pesagem','codigo_semi_acabado_pesagem','bigint','NO'),
            ('semi_acabado_pesagem','codigo_semi_acabado_lancamento','bigint','NO'),
            ('semi_acabado_pesagem','sequencia','integer','NO'),
            ('semi_acabado_pesagem','codigo_etiqueta','character varying','NO'),
            ('semi_acabado_pesagem','status_pesagem','character varying','NO'),
            ('semi_acabado_pesagem','peso_bruto_kg','numeric','NO'),
            ('semi_acabado_pesagem','peso_tara_kg','numeric','NO'),
            ('semi_acabado_pesagem','peso_liquido_kg','numeric','NO'),
            ('semi_acabado_pesagem','saldo_apos_pesagem_kg','numeric','NO'),
            ('semi_acabado_pesagem','origem','character varying','NO'),
            ('semi_acabado_pesagem','leitura_original','character varying','NO'),
            ('semi_acabado_pesagem','codigo_tara','bigint','YES'),
            ('semi_acabado_pesagem','registrado_em','timestamp with time zone','NO'),
            ('semi_acabado_pesagem','cancelado_em','timestamp with time zone','YES')
    )
    SELECT string_agg(e.tabela || '.' || e.coluna || ' esperado ' || e.tipo || '/' || e.anulavel
                      || ' encontrado ' || coalesce(c.data_type,'AUSENTE') || '/' || coalesce(c.is_nullable,'-'),
                      '; ' ORDER BY e.tabela, e.coluna)
      INTO v_problemas
      FROM esperado e
      LEFT JOIN information_schema.columns c
             ON c.table_schema = 'homologacao'
            AND c.table_name = e.tabela
            AND c.column_name = e.coluna
     WHERE c.column_name IS NULL
        OR c.data_type <> e.tipo
        OR c.is_nullable <> e.anulavel;

    IF v_problemas IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO 037: estrutura divergente: %', v_problemas;
    END IF;

    RAISE NOTICE 'OK - estrutura das tabelas validada.';
END $$;

-- Constraints esperadas.
DO $$
DECLARE
    v_ausentes text;
BEGIN
    WITH esperado(tabela, constraint_name) AS (
        VALUES
            ('semi_acabado_lancamento','ck_semi_acabado_lancamento_status'),
            ('semi_acabado_lancamento','ck_semi_acabado_lancamento_documento_confirmado'),
            ('semi_acabado_lancamento','ck_semi_acabado_lancamento_pesos_nao_negativos'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_status'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_sequencia_positiva'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_origem'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_pesos_nao_negativos'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_liquido_valida_positivo'),
            ('semi_acabado_pesagem','ck_semi_acabado_pesagem_cancelado_em')
    )
    SELECT string_agg(e.tabela || '.' || e.constraint_name, '; ' ORDER BY e.tabela, e.constraint_name)
      INTO v_ausentes
      FROM esperado e
      LEFT JOIN pg_constraint con
             ON con.conname = e.constraint_name
            AND con.conrelid = ('homologacao.' || e.tabela)::regclass
     WHERE con.oid IS NULL;

    IF v_ausentes IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO 037: constraints ausentes: %', v_ausentes;
    END IF;

    RAISE NOTICE 'OK - constraints esperadas presentes.';
END $$;

-- Indices esperados.
DO $$
DECLARE
    v_ausentes text;
BEGIN
    WITH esperado(index_name) AS (
        VALUES
            ('ix_semi_acabado_lancamento_ordem_item'),
            ('ix_semi_acabado_lancamento_status'),
            ('uq_semi_acabado_lancamento_material_document'),
            ('uq_semi_acabado_lancamento_aberto_por_op_item'),
            ('uq_semi_acabado_pesagem_lancamento_sequencia'),
            ('uq_semi_acabado_pesagem_codigo_etiqueta'),
            ('ix_semi_acabado_pesagem_lancamento_status')
    )
    SELECT string_agg(e.index_name, '; ' ORDER BY e.index_name)
      INTO v_ausentes
      FROM esperado e
      LEFT JOIN pg_class c ON c.relname = e.index_name
      LEFT JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'homologacao'
     WHERE c.oid IS NULL OR n.oid IS NULL;

    IF v_ausentes IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO 037: indices ausentes: %', v_ausentes;
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_index i
          JOIN pg_class idx ON idx.oid = i.indexrelid
          JOIN pg_class tbl ON tbl.oid = i.indrelid
          JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
         WHERE ns.nspname = 'homologacao'
           AND tbl.relname = 'semi_acabado_pesagem'
           AND idx.relname = 'uq_semi_acabado_pesagem_codigo_etiqueta'
           AND i.indisunique
           AND i.indpred IS NULL
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 037: indice global unico de codigo_etiqueta ausente/divergente.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM pg_index i
          JOIN pg_class idx ON idx.oid = i.indexrelid
          JOIN pg_class tbl ON tbl.oid = i.indrelid
          JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
         WHERE ns.nspname = 'homologacao'
           AND tbl.relname = 'semi_acabado_lancamento'
           AND idx.relname = 'uq_semi_acabado_lancamento_aberto_por_op_item'
           AND i.indisunique
           AND i.indpred IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'VALIDACAO 037: indice unico de lancamento aberto ausente/divergente.';
    END IF;

    RAISE NOTICE 'OK - indices esperados presentes e semantica principal validada.';
END $$;

-- ============================================================
-- 2. TESTES FUNCIONAIS TRANSACIONAIS
-- ============================================================
BEGIN;

DO $$
DECLARE
    v_lancamento bigint;
    v_lancamento_confirmado bigint;
    v_bloqueado boolean;
    v_qtd bigint;
BEGIN
    INSERT INTO homologacao.semi_acabado_lancamento
        (numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
         item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
         quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
         payload_preview_json, usuario_operacao)
    VALUES
        ('OP_VALIDACAO_037_GAIA','MAT_TESTE','Material teste 037','3007','PP01',
         '0010','L001','KG',100.000,0.000,100.000,100.000,'FINALIZADO_LOCAL',
         '{}','validador_037')
    RETURNING codigo_semi_acabado_lancamento INTO v_lancamento;

    INSERT INTO homologacao.semi_acabado_pesagem
        (codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
         peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg,
         origem, leitura_original)
    VALUES
        (v_lancamento, 1, 'ETQ_037_GAIA_A', 'VALIDA', 51.000, 1.000, 50.000, 50.000, 'BALANCA', '51.000'),
        (v_lancamento, 2, 'ETQ_037_GAIA_B', 'VALIDA', 50.500, 0.500, 50.000, 0.000, 'MANUAL', '50.500');

    SELECT count(*) INTO v_qtd
      FROM homologacao.semi_acabado_pesagem
     WHERE codigo_semi_acabado_lancamento = v_lancamento
       AND status_pesagem = 'VALIDA'
       AND saldo_apos_pesagem_kg IN (50.000, 0.000);

    IF v_qtd <> 2 THEN
        RAISE EXCEPTION 'FALHA: saldo_apos_pesagem_kg nao preservou saldo esperado 50/0.';
    END IF;
    RAISE NOTICE 'OK - saldo por etiqueta preservado para reimpressao.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_pesagem
            (codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
             peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg,
             origem, leitura_original)
        VALUES
            (v_lancamento, 3, 'ETQ_037_GAIA_A', 'VALIDA', 10.000, 0.000, 10.000, 0.000, 'MANUAL', '10.000');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: codigo_etiqueta duplicado foi aceito.';
    END IF;
    RAISE NOTICE 'OK - codigo_etiqueta unico global bloqueia duplicidade.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_pesagem
            (codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
             peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg,
             origem, leitura_original)
        VALUES
            (v_lancamento, 1, 'ETQ_037_GAIA_C', 'VALIDA', 10.000, 0.000, 10.000, 0.000, 'MANUAL', '10.000');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: sequencia duplicada no mesmo lancamento foi aceita.';
    END IF;
    RAISE NOTICE 'OK - sequencia unica por lancamento bloqueia duplicidade.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_pesagem
            (codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
             peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg,
             origem, leitura_original)
        VALUES
            (v_lancamento, 3, 'ETQ_037_GAIA_D', 'VALIDA', 0.000, 0.000, 0.000, 0.000, 'MANUAL', '0.000');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: pesagem valida com liquido zero foi aceita.';
    END IF;
    RAISE NOTICE 'OK - pesagem valida exige peso liquido positivo.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_pesagem
            (codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
             peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg,
             origem, leitura_original)
        VALUES
            (v_lancamento, 3, 'ETQ_037_GAIA_E', 'VALIDA', 10.000, 0.000, 10.000, -1.000, 'MANUAL', '10.000');
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: saldo apos pesagem negativo foi aceito.';
    END IF;
    RAISE NOTICE 'OK - saldo apos pesagem negativo bloqueado.';

    v_bloqueado := false;
    BEGIN
        UPDATE homologacao.semi_acabado_lancamento
           SET status_lancamento = 'XPTO'
         WHERE codigo_semi_acabado_lancamento = v_lancamento;
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: status_lancamento invalido foi aceito.';
    END IF;
    RAISE NOTICE 'OK - status_lancamento invalido bloqueado.';

    v_bloqueado := false;
    BEGIN
        UPDATE homologacao.semi_acabado_lancamento
           SET status_lancamento = 'CONFIRMADO_SAP'
         WHERE codigo_semi_acabado_lancamento = v_lancamento;
    EXCEPTION WHEN check_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: CONFIRMADO_SAP sem documento foi aceito.';
    END IF;
    RAISE NOTICE 'OK - CONFIRMADO_SAP exige documento e ano.';

    UPDATE homologacao.semi_acabado_lancamento
       SET status_lancamento = 'DIVERGENCIA_SAP'
     WHERE codigo_semi_acabado_lancamento = v_lancamento;
    RAISE NOTICE 'OK - DIVERGENCIA_SAP aceita como status controlado.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_lancamento
            (numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
             item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
             quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
             payload_preview_json, usuario_operacao)
        VALUES
            ('OP_VALIDACAO_037_GAIA','MAT_TESTE','Material teste 037','3007','PP01',
             '0010','L001','KG',100.000,0.000,100.000,10.000,'FINALIZADO_LOCAL',
             '{}','validador_037');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: segundo lancamento aberto para a mesma OP/item foi aceito.';
    END IF;
    RAISE NOTICE 'OK - indice de lancamento aberto por OP/item bloqueia duplicidade.';

    INSERT INTO homologacao.semi_acabado_lancamento
        (numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
         item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
         quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
         material_document, material_document_year, payload_preview_json, usuario_operacao)
    VALUES
        ('OP_VALIDACAO_037_GAIA','MAT_TESTE','Material teste 037','3007','PP01',
         '0010','L001','KG',100.000,100.000,0.000,100.000,'CONFIRMADO_SAP',
         '4900000037','2026','{}','validador_037')
    RETURNING codigo_semi_acabado_lancamento INTO v_lancamento_confirmado;

    RAISE NOTICE 'OK - lancamento CONFIRMADO_SAP historico permitido para mesma OP/item.';

    v_bloqueado := false;
    BEGIN
        INSERT INTO homologacao.semi_acabado_lancamento
            (numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
             item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
             quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
             material_document, material_document_year, payload_preview_json, usuario_operacao)
        VALUES
            ('OP_VALIDACAO_037_GAIA_2','MAT_TESTE','Material teste 037','3007','PP01',
             '0010','L001','KG',100.000,100.000,0.000,100.000,'CONFIRMADO_SAP',
             '4900000037','2026','{}','validador_037');
    EXCEPTION WHEN unique_violation THEN
        v_bloqueado := true;
    END;
    IF NOT v_bloqueado THEN
        RAISE EXCEPTION 'FALHA: documento SAP duplicado foi aceito.';
    END IF;
    RAISE NOTICE 'OK - documento SAP unico por ano bloqueia duplicidade.';
END $$;

ROLLBACK;

-- Residuo zero depois do rollback.
DO $$
DECLARE
    v_lancamentos bigint;
    v_pesagens bigint;
BEGIN
    SELECT count(*)
      INTO v_lancamentos
      FROM homologacao.semi_acabado_lancamento
     WHERE numero_ordem LIKE 'OP_VALIDACAO_037_GAIA%';

    SELECT count(*)
      INTO v_pesagens
      FROM homologacao.semi_acabado_pesagem
     WHERE codigo_etiqueta LIKE 'ETQ_037_GAIA_%';

    IF v_lancamentos <> 0 OR v_pesagens <> 0 THEN
        RAISE EXCEPTION 'VALIDACAO 037: residuos apos rollback. lancamentos=%, pesagens=%.',
            v_lancamentos, v_pesagens;
    END IF;

    RAISE NOTICE 'OK - testes transacionais descartados com ROLLBACK e residuos zero.';
END $$;

-- ============================================================
-- 3. PERMISSOES DO APP HML
-- ============================================================
DO $$
BEGIN
    RAISE NOTICE 'VALIDACAO: papel de aplicacao HML nao identificado nos incrementais existentes. Checagem de privilegios especifica nao executada.';
END $$;

DO $$
BEGIN
    RAISE NOTICE 'OK - regras de banco do Produto Semiacabado validadas (HML)';
END $$;
