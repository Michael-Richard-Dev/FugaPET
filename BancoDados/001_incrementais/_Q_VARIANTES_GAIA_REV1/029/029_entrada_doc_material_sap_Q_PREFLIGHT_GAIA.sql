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
-- 029_entrada_doc_material_sap_HML_PREFLIGHT_GAIA.sql
-- Projeto FugaPET_HML
-- Ambiente HOMOLOGACAO (HML)
-- Banco PostgreSQL - Schema homologacao
-- Objeto: Entrada de Produto - rastreabilidade do Documento de Material SAP (movimento 101)
-- Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
-- Data: 2026-06-25
-- Pacote: incremental 029
--
-- OBJETIVO
--   Validar o ESTADO ATUAL antes da aplicacao do incremental 029.
--   Este script NAO altera dados nem cria objetos. Deve falhar de forma
--   explicita se qualquer pre-condicao nao for atendida.
--
-- AVISO: NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
-- Ordem de execucao: PREFLIGHT -> PROPOSTA -> VALIDACAO -> (se necessario) ROLLBACK
-- ============================================================

SET search_path TO homologacao;

DO $$
DECLARE
    v_qtd integer;
BEGIN
    -- 1. Ambiente / schema correto
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'homologacao') THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: schema homologacao nao existe.';
    END IF;

    -- 2. Tabela do cabecalho do lancamento
    IF to_regclass('homologacao.entrada_produto_lancamento') IS NULL THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: tabela homologacao.entrada_produto_lancamento nao existe.';
    END IF;

    -- 3. Tabela de item (necessaria para o campo opcional documento_material_item)
    IF to_regclass('homologacao.entrada_produto_item') IS NULL THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: tabela homologacao.entrada_produto_item nao existe.';
    END IF;

    -- 4. Colunas base esperadas (pre-condicao do escopo de envio SAP)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_lancamento'
                      AND column_name = 'status_lancamento') THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: coluna entrada_produto_lancamento.status_lancamento nao existe.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_item'
                      AND column_name = 'status_item') THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: coluna entrada_produto_item.status_item nao existe.';
    END IF;

    -- 5. Constraint de status compativel com o fluxo de envio (ENVIADO_SAP / CONFIRMADO_SAP)
    PERFORM 1 FROM pg_constraint
      WHERE conname = 'ck_entrada_lancamento_status'
        AND conrelid = 'homologacao.entrada_produto_lancamento'::regclass;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: constraint ck_entrada_lancamento_status nao encontrada.';
    END IF;

    PERFORM 1 FROM pg_constraint
      WHERE conname = 'ck_entrada_lancamento_status'
        AND conrelid = 'homologacao.entrada_produto_lancamento'::regclass
        AND pg_get_constraintdef(oid) LIKE '%ENVIADO_SAP%'
        AND pg_get_constraintdef(oid) LIKE '%CONFIRMADO_SAP%';
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: ck_entrada_lancamento_status incompativel (esperados ENVIADO_SAP e CONFIRMADO_SAP).';
    END IF;

    -- 6. Permissao suficiente para ALTER TABLE (dono da tabela ou superuser)
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_roles r ON r.oid = c.relowner
         WHERE c.oid = 'homologacao.entrada_produto_lancamento'::regclass
           AND pg_has_role(current_user, r.rolname, 'MEMBER')
    ) AND NOT COALESCE((SELECT rolsuper FROM pg_roles WHERE rolname = current_user), false) THEN
        RAISE EXCEPTION 'Falha PREFLIGHT: usuario % sem privilegio para ALTER TABLE (nao e dono nem superuser).', current_user;
    END IF;

    -- 7. Estado: colunas do 029 NAO devem existir ainda (detecta reaplicacao parcial)
    SELECT count(*) INTO v_qtd FROM information_schema.columns
     WHERE table_schema = 'homologacao' AND table_name = 'entrada_produto_lancamento'
       AND column_name IN ('documento_material_sap', 'exercicio_documento_material_sap', 'enviado_sap_em');
    IF v_qtd > 0 THEN
        RAISE NOTICE 'Aviso PREFLIGHT: % coluna(s) do 029 ja existem em entrada_produto_lancamento (reaplicacao parcial). A PROPOSTA e idempotente (ADD COLUMN IF NOT EXISTS).', v_qtd;
    END IF;

    RAISE NOTICE 'PREFLIGHT 029 (HML) OK: pre-condicoes atendidas.';
END $$;

-- Diagnostico de dados atuais (somente leitura, nao altera nada)
WITH estado AS (
    SELECT 'lancamentos_total'            AS metrica, count(*)::bigint AS quantidade FROM homologacao.entrada_produto_lancamento
    UNION ALL
    SELECT 'lancamentos_confirmado_sap',  count(*)::bigint FROM homologacao.entrada_produto_lancamento WHERE status_lancamento = 'CONFIRMADO_SAP'
    UNION ALL
    SELECT 'lancamentos_enviado_sap',     count(*)::bigint FROM homologacao.entrada_produto_lancamento WHERE status_lancamento = 'ENVIADO_SAP'
    UNION ALL
    SELECT 'itens_total',                 count(*)::bigint FROM homologacao.entrada_produto_item
)
SELECT metrica, quantidade FROM estado ORDER BY metrica;
