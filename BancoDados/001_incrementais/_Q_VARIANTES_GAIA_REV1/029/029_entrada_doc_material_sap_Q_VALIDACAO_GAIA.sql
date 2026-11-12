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
-- 029_entrada_doc_material_sap_HML_VALIDACAO_GAIA.sql
-- Projeto FugaPET_HML
-- Ambiente HOMOLOGACAO (HML)
-- Banco PostgreSQL - Schema homologacao
-- Objeto: Entrada de Produto - rastreabilidade do Documento de Material SAP (movimento 101)
-- Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
-- Data: 2026-06-25
-- Pacote: incremental 029
--
-- OBJETIVO
--   Comprovar, APOS a PROPOSTA, que: colunas foram criadas com os tipos corretos,
--   comentarios e constraints aplicados, rollback e possivel e registros existentes
--   continuam integros (nenhum impacto fora do escopo). Somente leitura.
--
-- AVISO: NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
-- Ordem de execucao: PREFLIGHT -> PROPOSTA -> VALIDACAO -> (se necessario) ROLLBACK
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_tipo   text;
    v_tam    integer;
    v_qtd    integer;
BEGIN
    -- 1. Colunas criadas + tipos corretos (entrada_produto_lancamento)
    SELECT data_type, character_maximum_length INTO v_tipo, v_tam
      FROM information_schema.columns
     WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_lancamento'
       AND column_name = 'documento_material_sap';
    IF v_tipo IS NULL THEN RAISE EXCEPTION 'Falha VALIDACAO: coluna documento_material_sap nao criada.'; END IF;
    IF v_tipo <> 'character varying' OR v_tam <> 20 THEN
        RAISE EXCEPTION 'Falha VALIDACAO: documento_material_sap com tipo/tamanho inesperado (% / %).', v_tipo, v_tam;
    END IF;

    SELECT data_type, character_maximum_length INTO v_tipo, v_tam
      FROM information_schema.columns
     WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_lancamento'
       AND column_name = 'exercicio_documento_material_sap';
    IF v_tipo IS NULL THEN RAISE EXCEPTION 'Falha VALIDACAO: coluna exercicio_documento_material_sap nao criada.'; END IF;
    IF v_tipo <> 'character varying' OR v_tam <> 4 THEN
        RAISE EXCEPTION 'Falha VALIDACAO: exercicio_documento_material_sap com tipo/tamanho inesperado (% / %).', v_tipo, v_tam;
    END IF;

    SELECT data_type INTO v_tipo
      FROM information_schema.columns
     WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_lancamento'
       AND column_name = 'enviado_sap_em';
    IF v_tipo IS NULL THEN RAISE EXCEPTION 'Falha VALIDACAO: coluna enviado_sap_em nao criada.'; END IF;
    IF v_tipo <> 'timestamp with time zone' THEN
        RAISE EXCEPTION 'Falha VALIDACAO: enviado_sap_em com tipo inesperado (%).', v_tipo;
    END IF;

    -- 2. Coluna opcional do item
    SELECT data_type, character_maximum_length INTO v_tipo, v_tam
      FROM information_schema.columns
     WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_item'
       AND column_name = 'documento_material_item';
    IF v_tipo IS NULL THEN RAISE EXCEPTION 'Falha VALIDACAO: coluna documento_material_item nao criada.'; END IF;
    IF v_tipo <> 'character varying' OR v_tam <> 20 THEN
        RAISE EXCEPTION 'Falha VALIDACAO: documento_material_item com tipo/tamanho inesperado (% / %).', v_tipo, v_tam;
    END IF;

    -- 3. Nenhuma das colunas novas deve ser NOT NULL
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'homologacao'
           AND ((table_name = 'entrada_produto_lancamento'
                 AND column_name IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em'))
             OR (table_name = 'entrada_produto_item' AND column_name = 'documento_material_item'))
           AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION 'Falha VALIDACAO: alguma coluna do 029 ficou NOT NULL (esperado nullable).';
    END IF;

    -- 4. Constraints aplicadas
    PERFORM 1 FROM pg_constraint
      WHERE conname = 'ck_entrada_lancamento_documento_material_par'
        AND conrelid = 'homologacao.entrada_produto_lancamento'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha VALIDACAO: constraint ck_entrada_lancamento_documento_material_par ausente.'; END IF;

    PERFORM 1 FROM pg_constraint
      WHERE conname = 'ck_entrada_lancamento_exercicio_material_formato'
        AND conrelid = 'homologacao.entrada_produto_lancamento'::regclass;
    IF NOT FOUND THEN RAISE EXCEPTION 'Falha VALIDACAO: constraint ck_entrada_lancamento_exercicio_material_formato ausente.'; END IF;

    -- 5. Comentarios aplicados
    IF col_description('homologacao.entrada_produto_lancamento'::regclass,
            (SELECT attnum FROM pg_attribute WHERE attrelid = 'homologacao.entrada_produto_lancamento'::regclass
              AND attname = 'documento_material_sap')) IS NULL THEN
        RAISE EXCEPTION 'Falha VALIDACAO: comentario ausente em documento_material_sap.';
    END IF;

    -- 6. Integridade dos registros existentes: nenhum viola a constraint de par; logo apos a
    --    PROPOSTA as colunas estao NULL (sem impacto em dados pre-existentes).
    SELECT count(*) INTO v_qtd
      FROM homologacao.entrada_produto_lancamento
     WHERE (documento_material_sap IS NULL) <> (exercicio_documento_material_sap IS NULL);
    IF v_qtd > 0 THEN
        RAISE EXCEPTION 'Falha VALIDACAO: % lancamento(s) violam o par documento/exercicio.', v_qtd;
    END IF;

    SELECT count(*) INTO v_qtd
      FROM homologacao.entrada_produto_lancamento
     WHERE documento_material_sap IS NOT NULL OR exercicio_documento_material_sap IS NOT NULL OR enviado_sap_em IS NOT NULL;
    RAISE NOTICE 'VALIDACAO 029 (HML): lancamentos com rastreabilidade preenchida = % (esperado 0 logo apos a PROPOSTA).', v_qtd;

    RAISE NOTICE 'VALIDACAO 029 (HML) OK: colunas, tipos, comentarios e constraints conferidos. Rollback disponivel.';
END $$;

-- Evidencias consultaveis (information_schema / pg_constraint / col_description)
SELECT table_name, column_name, data_type, character_maximum_length, is_nullable
  FROM information_schema.columns
 WHERE table_schema = 'homologacao'
   AND ((table_name = 'entrada_produto_lancamento'
         AND column_name IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em'))
     OR (table_name = 'entrada_produto_item' AND column_name = 'documento_material_item'))
 ORDER BY table_name, column_name;

SELECT conname, pg_get_constraintdef(oid) AS definicao
  FROM pg_constraint
 WHERE conrelid = 'homologacao.entrada_produto_lancamento'::regclass
   AND conname IN ('ck_entrada_lancamento_documento_material_par', 'ck_entrada_lancamento_exercicio_material_formato')
 ORDER BY conname;

SELECT a.attname AS coluna, col_description(a.attrelid, a.attnum) AS comentario
  FROM pg_attribute a
 WHERE a.attrelid = 'homologacao.entrada_produto_lancamento'::regclass
   AND a.attname IN ('documento_material_sap','exercicio_documento_material_sap','enviado_sap_em')
 ORDER BY a.attname;
