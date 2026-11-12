\set ON_ERROR_STOP on
-- ============================================================
-- 037_semi_acabado_HML_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_Dev | Schema: homologacao
-- Objeto: Produto Semiacabado - persistencia local, SAP 101 e etiqueta por pesagem
--
-- EXECUCAO CONTROLADA. NAO EXECUTAR AUTOMATICAMENTE.
-- Somente leitura. Nao altera dados.
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_estado text;
    v_problemas text;
    v_tabela_lancamento boolean := to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL;
    v_tabela_pesagem boolean := to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL;
    v_comentario text := 'FugaPET incremental 037 semi_acabado';
BEGIN
    IF NOT v_tabela_lancamento AND NOT v_tabela_pesagem THEN
        v_estado := 'PACOTE_NAO_APLICADO';
    ELSIF v_tabela_lancamento AND v_tabela_pesagem THEN
        v_estado := 'PACOTE_PRESENTE_PARA_CONFERENCIA';
    ELSE
        v_estado := 'ESTADO_PARCIAL_OU_INCOMPATIVEL';
    END IF;

    RAISE NOTICE 'Estado 037 semi-acabado: %', v_estado;

    IF v_estado = 'ESTADO_PARCIAL_OU_INCOMPATIVEL' THEN
        RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: estado parcial. lancamento=%, pesagem=%.',
            v_tabela_lancamento, v_tabela_pesagem;
    END IF;

    IF v_estado = 'PACOTE_PRESENTE_PARA_CONFERENCIA' THEN
        IF obj_description('homologacao.semi_acabado_lancamento'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: tabela semi_acabado_lancamento existe sem comentario de propriedade do pacote 037.';
        END IF;

        IF obj_description('homologacao.semi_acabado_pesagem'::regclass, 'pg_class') IS DISTINCT FROM v_comentario THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: tabela semi_acabado_pesagem existe sem comentario de propriedade do pacote 037.';
        END IF;
    END IF;
END $$;

-- Estado dos objetos-alvo.
SELECT 'semi_acabado_lancamento' AS objeto,
       to_regclass('homologacao.semi_acabado_lancamento') AS existe,
       CASE WHEN to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL
            THEN obj_description('homologacao.semi_acabado_lancamento'::regclass, 'pg_class')
            ELSE NULL END AS comentario
UNION ALL
SELECT 'semi_acabado_pesagem',
       to_regclass('homologacao.semi_acabado_pesagem'),
       CASE WHEN to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL
            THEN obj_description('homologacao.semi_acabado_pesagem'::regclass, 'pg_class')
            ELSE NULL END;

-- Colunas atuais para conferencia visual.
SELECT table_schema, table_name, column_name, data_type, is_nullable, column_default
  FROM information_schema.columns
 WHERE table_schema = 'homologacao'
   AND table_name IN ('semi_acabado_lancamento','semi_acabado_pesagem')
 ORDER BY table_schema, table_name, ordinal_position;

-- Validacao estrutural completa quando as tabelas ja existem.
DO $$
DECLARE
    v_problemas text;
BEGIN
    IF to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL THEN
        WITH esperado(coluna, tipo, anulavel) AS (
            VALUES
                ('codigo_semi_acabado_lancamento','bigint','NO'),
                ('numero_ordem','character varying','NO'),
                ('material_produzido','character varying','NO'),
                ('descricao_material','character varying','NO'),
                ('centro','character varying','NO'),
                ('deposito_destino','character varying','NO'),
                ('item_ordem','character varying','NO'),
                ('lote','character varying','NO'),
                ('unidade','character varying','NO'),
                ('quantidade_planejada_kg','numeric','NO'),
                ('quantidade_entregue_kg','numeric','NO'),
                ('quantidade_pendente_kg','numeric','NO'),
                ('peso_liquido_total_kg','numeric','NO'),
                ('status_lancamento','character varying','NO'),
                ('material_document','character varying','YES'),
                ('material_document_year','character varying','YES'),
                ('mensagem_erro_sap','text','YES'),
                ('payload_preview_json','text','NO'),
                ('usuario_operacao','character varying','NO'),
                ('enviado_sap_em','timestamp with time zone','YES'),
                ('criado_em','timestamp with time zone','NO'),
                ('atualizado_em','timestamp with time zone','NO')
        )
        SELECT string_agg(
                   e.coluna || ' (esperado ' || e.tipo || '/' || e.anulavel
                   || ', encontrado ' || coalesce(c.data_type, 'AUSENTE') || '/' || coalesce(c.is_nullable, '-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          LEFT JOIN information_schema.columns c
                 ON c.table_schema = 'homologacao'
                AND c.table_name = 'semi_acabado_lancamento'
                AND c.column_name = e.coluna
         WHERE c.column_name IS NULL
            OR c.data_type <> e.tipo
            OR c.is_nullable <> e.anulavel;

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: semi_acabado_lancamento incompativel: %', v_problemas;
        END IF;
    ELSE
        RAISE NOTICE 'PREFLIGHT: semi_acabado_lancamento ausente. Sera criada pela proposta.';
    END IF;

    IF to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL THEN
        WITH esperado(coluna, tipo, anulavel) AS (
            VALUES
                ('codigo_semi_acabado_pesagem','bigint','NO'),
                ('codigo_semi_acabado_lancamento','bigint','NO'),
                ('sequencia','integer','NO'),
                ('codigo_etiqueta','character varying','NO'),
                ('status_pesagem','character varying','NO'),
                ('peso_bruto_kg','numeric','NO'),
                ('peso_tara_kg','numeric','NO'),
                ('peso_liquido_kg','numeric','NO'),
                ('saldo_apos_pesagem_kg','numeric','NO'),
                ('origem','character varying','NO'),
                ('leitura_original','character varying','NO'),
                ('codigo_tara','bigint','YES'),
                ('registrado_em','timestamp with time zone','NO'),
                ('cancelado_em','timestamp with time zone','YES')
        )
        SELECT string_agg(
                   e.coluna || ' (esperado ' || e.tipo || '/' || e.anulavel
                   || ', encontrado ' || coalesce(c.data_type, 'AUSENTE') || '/' || coalesce(c.is_nullable, '-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          LEFT JOIN information_schema.columns c
                 ON c.table_schema = 'homologacao'
                AND c.table_name = 'semi_acabado_pesagem'
                AND c.column_name = e.coluna
         WHERE c.column_name IS NULL
            OR c.data_type <> e.tipo
            OR c.is_nullable <> e.anulavel;

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: semi_acabado_pesagem incompativel: %', v_problemas;
        END IF;
    ELSE
        RAISE NOTICE 'PREFLIGHT: semi_acabado_pesagem ausente. Sera criada pela proposta.';
    END IF;


    IF to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL THEN
        WITH esperado(coluna, tamanho, precisao, escala, identidade) AS (
            VALUES
                ('codigo_semi_acabado_lancamento', NULL::integer, NULL::integer, NULL::integer, 'YES'),
                ('numero_ordem', 40, NULL, NULL, 'NO'),
                ('material_produzido', 40, NULL, NULL, 'NO'),
                ('descricao_material', 255, NULL, NULL, 'NO'),
                ('centro', 10, NULL, NULL, 'NO'),
                ('deposito_destino', 20, NULL, NULL, 'NO'),
                ('item_ordem', 10, NULL, NULL, 'NO'),
                ('lote', 80, NULL, NULL, 'NO'),
                ('unidade', 10, NULL, NULL, 'NO'),
                ('quantidade_planejada_kg', NULL, 14, 3, 'NO'),
                ('quantidade_entregue_kg', NULL, 14, 3, 'NO'),
                ('quantidade_pendente_kg', NULL, 14, 3, 'NO'),
                ('peso_liquido_total_kg', NULL, 14, 3, 'NO'),
                ('status_lancamento', 30, NULL, NULL, 'NO'),
                ('material_document', 20, NULL, NULL, 'NO'),
                ('material_document_year', 4, NULL, NULL, 'NO'),
                ('usuario_operacao', 120, NULL, NULL, 'NO')
        )
        SELECT string_agg(
                   e.coluna || ' (tam/prec/esc/id esperado '
                   || coalesce(e.tamanho::text, '-') || '/'
                   || coalesce(e.precisao::text, '-') || '/'
                   || coalesce(e.escala::text, '-') || '/'
                   || e.identidade
                   || ', encontrado '
                   || coalesce(c.character_maximum_length::text, '-') || '/'
                   || coalesce(c.numeric_precision::text, '-') || '/'
                   || coalesce(c.numeric_scale::text, '-') || '/'
                   || coalesce(c.is_identity, '-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          JOIN information_schema.columns c
            ON c.table_schema = 'homologacao'
           AND c.table_name = 'semi_acabado_lancamento'
           AND c.column_name = e.coluna
         WHERE (e.tamanho IS NOT NULL AND c.character_maximum_length IS DISTINCT FROM e.tamanho)
            OR (e.precisao IS NOT NULL AND c.numeric_precision IS DISTINCT FROM e.precisao)
            OR (e.escala IS NOT NULL AND c.numeric_scale IS DISTINCT FROM e.escala)
            OR (c.is_identity IS DISTINCT FROM e.identidade);

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: semi_acabado_lancamento tamanho/precisao/identity incompativel: %', v_problemas;
        END IF;
    END IF;

    IF to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL THEN
        WITH esperado(coluna, tamanho, precisao, escala, identidade) AS (
            VALUES
                ('codigo_semi_acabado_pesagem', NULL::integer, NULL::integer, NULL::integer, 'YES'),
                ('codigo_semi_acabado_lancamento', NULL, NULL, NULL, 'NO'),
                ('codigo_etiqueta', 60, NULL, NULL, 'NO'),
                ('status_pesagem', 30, NULL, NULL, 'NO'),
                ('peso_bruto_kg', NULL, 14, 3, 'NO'),
                ('peso_tara_kg', NULL, 14, 3, 'NO'),
                ('peso_liquido_kg', NULL, 14, 3, 'NO'),
                ('saldo_apos_pesagem_kg', NULL, 14, 3, 'NO'),
                ('origem', 20, NULL, NULL, 'NO'),
                ('leitura_original', 60, NULL, NULL, 'NO'),
                ('codigo_tara', NULL, NULL, NULL, 'NO')
        )
        SELECT string_agg(
                   e.coluna || ' (tam/prec/esc/id esperado '
                   || coalesce(e.tamanho::text, '-') || '/'
                   || coalesce(e.precisao::text, '-') || '/'
                   || coalesce(e.escala::text, '-') || '/'
                   || e.identidade
                   || ', encontrado '
                   || coalesce(c.character_maximum_length::text, '-') || '/'
                   || coalesce(c.numeric_precision::text, '-') || '/'
                   || coalesce(c.numeric_scale::text, '-') || '/'
                   || coalesce(c.is_identity, '-') || ')',
                   '; ' ORDER BY e.coluna)
          INTO v_problemas
          FROM esperado e
          JOIN information_schema.columns c
            ON c.table_schema = 'homologacao'
           AND c.table_name = 'semi_acabado_pesagem'
           AND c.column_name = e.coluna
         WHERE (e.tamanho IS NOT NULL AND c.character_maximum_length IS DISTINCT FROM e.tamanho)
            OR (e.precisao IS NOT NULL AND c.numeric_precision IS DISTINCT FROM e.precisao)
            OR (e.escala IS NOT NULL AND c.numeric_scale IS DISTINCT FROM e.escala)
            OR (c.is_identity IS DISTINCT FROM e.identidade);

        IF v_problemas IS NOT NULL THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: semi_acabado_pesagem tamanho/precisao/identity incompativel: %', v_problemas;
        END IF;
    END IF;
END $$;

-- Duplicidades e dados que quebrariam constraints/indices se as tabelas ja existirem.
DO $$
BEGIN
    IF to_regclass('homologacao.semi_acabado_pesagem') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_pesagem
             GROUP BY codigo_semi_acabado_lancamento, sequencia
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existem duplicidades em (codigo_semi_acabado_lancamento, sequencia).';
        END IF;

        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_pesagem
             GROUP BY codigo_etiqueta
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existem codigo_etiqueta duplicados.';
        END IF;

        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_pesagem
             WHERE sequencia <= 0
                OR status_pesagem NOT IN ('VALIDA','CANCELADA')
                OR origem NOT IN ('MANUAL','BALANCA')
                OR peso_bruto_kg < 0
                OR peso_tara_kg < 0
                OR peso_liquido_kg < 0
                OR saldo_apos_pesagem_kg < 0
                OR (status_pesagem = 'VALIDA' AND peso_liquido_kg <= 0)
                OR (status_pesagem = 'CANCELADA' AND cancelado_em IS NULL)
                OR (status_pesagem = 'VALIDA' AND cancelado_em IS NOT NULL)
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existem pesagens incompatíveis com as constraints propostas.';
        END IF;
    END IF;

    IF to_regclass('homologacao.semi_acabado_lancamento') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_lancamento
             WHERE status_lancamento IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','ERRO_SAP','DIVERGENCIA_SAP')
             GROUP BY numero_ordem, item_ordem
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existe mais de um lancamento aberto para a mesma OP/item.';
        END IF;

        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_lancamento
             WHERE material_document IS NOT NULL
               AND material_document_year IS NOT NULL
             GROUP BY material_document, material_document_year
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existem documentos SAP duplicados.';
        END IF;

        IF EXISTS (
            SELECT 1
              FROM homologacao.semi_acabado_lancamento
             WHERE status_lancamento NOT IN ('FINALIZADO_LOCAL','ENVIANDO_SAP','CONFIRMADO_SAP','ERRO_SAP','DIVERGENCIA_SAP')
                OR peso_liquido_total_kg < 0
                OR quantidade_planejada_kg < 0
                OR quantidade_entregue_kg < 0
                OR quantidade_pendente_kg < 0
                OR (status_lancamento = 'CONFIRMADO_SAP'
                    AND (material_document IS NULL OR material_document_year IS NULL))
        ) THEN
            RAISE EXCEPTION 'PREFLIGHT 037 ABORTADO: existem lancamentos incompatíveis com as constraints propostas.';
        END IF;
    END IF;

    RAISE NOTICE 'PREFLIGHT 037 HML OK: pacote apto para proposta ou pacote ja aplicado em estado coerente.';
END $$;
