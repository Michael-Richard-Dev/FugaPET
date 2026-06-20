-- ============================================================================
-- FugaPET_Dev - H22 - PROPOSTA DE ENDURECIMENTO PARA O GAIA DADOS
-- VALIDACAO DE IMPLANTACAO SEGURA DO BANCO DE DESENVOLVIMENTO V1.1
-- NAO EXECUTAR SEM REVISAO E APROVACAO DO GAIA DADOS.
-- Alvo: fuga_jales_local_desenvolvimento / schema desenvolvimento.
--
-- NATUREZA DESTE SCRIPT
--   - SECAO A: validacoes (somente leitura) que NAO alteram dados nem estrutura.
--     Podem ser executadas com seguranca pos-instalacao para auditar a postura.
--   - SECAO B (no final, COMENTADA): mudancas estruturais propostas para o baseline
--     (transacao global, bootstrap com clock_timestamp, remocao gradual de
--     date_trunc, role de aplicacao + grants). NAO sao aplicadas por este script;
--     ficam como proposta para o Gaia aprovar e aplicar no baseline.
--
-- SEVERIDADE
--   - RAISE EXCEPTION: integridade de schema/dados (aborta a transacao).
--   - RAISE WARNING:   postura de infraestrutura (role/superusuario/integracao),
--                      que exige decisao de implantacao e nao aborta a validacao.
-- ============================================================================

BEGIN;
SET LOCAL search_path TO desenvolvimento, public;

-- ----------------------------------------------------------------------------
-- A1. Uso de superusuario (postgres) pela conexao da aplicacao
-- A aplicacao NAO deve operar como superusuario. Recomendado: role dedicada
-- com grants explicitos. Advisory (WARNING) pois a correcao e de implantacao.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = current_user AND rolsuper) THEN
        RAISE WARNING 'H22: a conexao atual usa role superusuaria (%). A aplicacao deve usar role dedicada nao-superusuaria com grants explicitos.', current_user;
    END IF;

    IF lower(current_user) = 'postgres' THEN
        RAISE WARNING 'H22: a conexao atual usa o usuario postgres. Crie e use uma role de aplicacao especifica (ver Secao B).';
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A2. Roles/grants explicitos para o schema desenvolvimento
-- Deve existir ao menos uma role NAO-superusuaria com USAGE no schema. A
-- ausencia indica que tudo depende do dono/superusuario (postura insegura).
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_roles_app integer;
BEGIN
    SELECT count(*)
      INTO v_roles_app
      FROM pg_roles r
     WHERE NOT r.rolsuper
       AND r.rolcanlogin
       AND has_schema_privilege(r.rolname, 'desenvolvimento', 'USAGE');

    IF v_roles_app = 0 THEN
        RAISE WARNING 'H22: nenhuma role de aplicacao nao-superusuaria com USAGE em desenvolvimento. Defina role + grants explicitos (ver Secao B).';
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A3. Administrador com hash BCrypt valido (sem expor o hash)
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_hash text;
BEGIN
    SELECT senha_hash
      INTO v_hash
      FROM desenvolvimento.usuario
     WHERE lower(trim(login_usuario)) = 'admin'
       AND situacao_usuario = true
     ORDER BY codigo_usuario
     LIMIT 1;

    IF v_hash IS NULL THEN
        RAISE EXCEPTION 'H22: usuario admin ativo nao encontrado. Execute o bootstrap 001.';
    END IF;

    IF length(v_hash) <> 60
       OR v_hash !~ '^\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}$' THEN
        RAISE EXCEPTION 'H22: senha_hash do admin nao e um BCrypt valido de 60 caracteres.';
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A4. Um unico setor padrao ativo por usuario
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_usuarios_com_multiplos integer;
BEGIN
    SELECT count(*)
      INTO v_usuarios_com_multiplos
      FROM (
          SELECT codigo_usuario
            FROM desenvolvimento.usuario_setor
           WHERE setor_padrao = true
             AND situacao_usuario_setor = true
           GROUP BY codigo_usuario
          HAVING count(*) > 1
      ) AS multiplos;

    IF v_usuarios_com_multiplos > 0 THEN
        RAISE EXCEPTION 'H22: % usuario(s) com mais de um setor padrao ativo em usuario_setor.', v_usuarios_com_multiplos;
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A5. Ausencia de ON DELETE CASCADE em FKs CRITICAS (rastreabilidade/auditoria)
-- Essas tabelas devem preservar historico: nunca cascatear delete. O filtro por
-- existencia (pg_constraint so tem linhas de tabelas existentes) cobre o "quando
-- existirem" pedido pelo Gaia. Integridade de schema => EXCEPTION.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_fk_criticas_cascade text;
    -- Fluxo NOVO de rastreabilidade + tabelas de auditoria/log de integracao SAP:
    -- nunca devem cascatear delete. (Legados de agregado vao em A5b como revisao.)
    v_criticas text[] := ARRAY[
        'entrada_produto_item',
        'entrada_produto_pesagem',
        'auditoria_acao_usuario',
        'log_alteracao_cadastral',
        'log_integracao_sap',
        'integracao_sap_outbox',
        'integracao_sap_tentativa'
    ];
BEGIN
    SELECT string_agg(conrelid::regclass::text, ', ')
      INTO v_fk_criticas_cascade
      FROM pg_constraint c
      JOIN pg_namespace n ON n.oid = c.connamespace
     WHERE c.contype = 'f'
       AND c.confdeltype = 'c'              -- 'c' = ON DELETE CASCADE
       AND n.nspname = 'desenvolvimento'
       AND (conrelid::regclass::text) = ANY (v_criticas);

    IF v_fk_criticas_cascade IS NOT NULL THEN
        RAISE EXCEPTION 'H22: FK critica com ON DELETE CASCADE encontrada em: %. Use ON DELETE RESTRICT/SET NULL.', v_fk_criticas_cascade;
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A5b. Agregados que HOJE cascateiam delete para a tabela-pai (filhos de
-- agregado): INT012/HU e o fluxo LEGADO de pesagem (pesagem_entrada_item*,
-- deprecado no H21). Gaia pediu para incluir na revisao. Converter de CASCADE
-- para RESTRICT e decisao de baseline (Secao B); aqui apenas SINALIZA (WARNING)
-- para nao reprovar a Secao A aprovada.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_fk_int012_hu_cascade text;
    v_revisar text[] := ARRAY[
        'integracao_sap_int012_log',
        'integracao_sap_int012_mensagem',
        'integracao_sap_int012_reprocessamento',
        'hu_caixa_integracao_sap',
        'hu_palete_integracao_sap',
        'pesagem_entrada_item',
        'pesagem_entrada_item_leitura'
    ];
BEGIN
    SELECT string_agg(conrelid::regclass::text, ', ')
      INTO v_fk_int012_hu_cascade
      FROM pg_constraint c
      JOIN pg_namespace n ON n.oid = c.connamespace
     WHERE c.contype = 'f'
       AND c.confdeltype = 'c'
       AND n.nspname = 'desenvolvimento'
       AND (conrelid::regclass::text) = ANY (v_revisar);

    IF v_fk_int012_hu_cascade IS NOT NULL THEN
        RAISE WARNING 'H22: revisar ON DELETE CASCADE em tabelas de integracao/log INT012/HU: %. Avaliar converter para RESTRICT na Secao B.', v_fk_int012_hu_cascade;
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- A6. Avaliacao de INTEGRACAO_SAP_ATIVA como padrao em desenvolvimento
-- Recomendacao H22: NAO manter true por padrao no DEV (risco de escrita SAP em
-- ambiente compartilhado). Advisory (WARNING). Tambem sinaliza a divergencia
-- entre o seed do baseline (false) e o validador 002 (que exige true).
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_integracao text;
BEGIN
    SELECT valor
      INTO v_integracao
      FROM desenvolvimento.configuracao_geral
     WHERE upper(trim(chave)) = 'INTEGRACAO_SAP_ATIVA'
       AND situacao_configuracao_geral = true
     ORDER BY codigo_configuracao_geral DESC
     LIMIT 1;

    RAISE WARNING 'H22: INTEGRACAO_SAP_ATIVA atual = %. Recomendacao: padrao false no desenvolvimento; ativar apenas para testes controlados (ver C12/C13). Alinhar seed (000) e validador (002).', coalesce(v_integracao, '<ausente>');
END $$;

COMMIT;

SELECT 'H22: validacao de endurecimento concluida (verifique WARNINGS).' AS resultado_validacao;

-- ============================================================================
-- SECAO B - PROPOSTAS ESTRUTURAIS PARA O BASELINE (NAO APLICAR SEM APROVACAO)
-- Mantidas COMENTADAS de proposito. O Gaia decide se incorpora ao baseline.
-- ============================================================================
--
-- B1. Transacao global no instalador 000:
--     Envolver TODO o 000_execucao_completa em uma transacao unica para que uma
--     falha no meio nao deixe schema parcial:
--
--     BEGIN;
--     SET LOCAL search_path TO desenvolvimento, public;
--     ... (todo o conteudo do 000) ...
--     COMMIT;
--
-- B2. Bootstrap (001) com clock_timestamp() no lugar de date_trunc('minute', now()):
--
--     -- linha ~60:
--     -- usuario_atualizado_em = clock_timestamp()
--     -- linha ~91:
--     -- usuario_perfil_atualizado_em = clock_timestamp()
--
-- B3. Remocao gradual de date_trunc('minute', now()) nos cadastros remanescentes
--     (a fn_definir_atualizado_em e os eventos prioritarios ja foram tratados no
--     incremental 024/H14). Exemplo de ALTER por coluna (aplicar em lote aprovado):
--
--     -- ALTER TABLE desenvolvimento.setor   ALTER COLUMN setor_criado_em   SET DEFAULT clock_timestamp();
--     -- ALTER TABLE desenvolvimento.cargo   ALTER COLUMN cargo_criado_em   SET DEFAULT clock_timestamp();
--     -- ... demais *_criado_em de cadastros nao prioritarios ...
--
-- B4. Roles SEPARADAS (parecer Gaia): role de APLICACAO != role de MIGRACAO.
--     Nenhuma usa postgres/superusuario. Em DEV a role da aplicacao pode ser
--     ampla, mas SEM DELETE amplo e SEM DDL (DDL pertence a role de migracao).
--
--     -- Role de MIGRACAO (DDL/baseline/incrementais; dona dos objetos):
--     -- CREATE ROLE fugapet_dev_migracao LOGIN PASSWORD '<definir no ambiente>';
--     -- GRANT USAGE, CREATE ON SCHEMA desenvolvimento TO fugapet_dev_migracao;
--
--     -- Role de APLICACAO (runtime; sem DDL, sem DELETE amplo):
--     -- CREATE ROLE fugapet_dev_app LOGIN PASSWORD '<definir no ambiente>';
--     -- GRANT USAGE ON SCHEMA desenvolvimento TO fugapet_dev_app;
--     -- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA desenvolvimento TO fugapet_dev_app;
--     -- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA desenvolvimento TO fugapet_dev_app;
--     -- ALTER DEFAULT PRIVILEGES FOR ROLE fugapet_dev_migracao IN SCHEMA desenvolvimento
--     --     GRANT SELECT, INSERT, UPDATE ON TABLES TO fugapet_dev_app;
--     -- ALTER DEFAULT PRIVILEGES FOR ROLE fugapet_dev_migracao IN SCHEMA desenvolvimento
--     --     GRANT USAGE, SELECT ON SEQUENCES TO fugapet_dev_app;
--     -- (DELETE concedido pontualmente apenas onde a regra de negocio exigir)
--     -- Depois: a aplicacao usa fugapet_dev_app; migracoes usam fugapet_dev_migracao.
--
-- B5. INTEGRACAO_SAP_ATIVA (DECISAO GAIA): padrao false no DEV; ativar true
--     somente em janela de teste controlado. Ajustar tambem o validador 002
--     para nao exigir true incondicionalmente.
--
--     -- UPDATE desenvolvimento.configuracao_geral
--     --    SET valor = 'false'
--     --  WHERE upper(trim(chave)) = 'INTEGRACAO_SAP_ATIVA';
--
-- B6. Indice unico parcial protegendo "um unico setor padrao ativo por usuario"
--     (parecer Gaia: validacao A4 correta; banco deve proteger a regra):
--
--     -- CREATE UNIQUE INDEX uq_usuario_setor_padrao_ativo
--     --     ON desenvolvimento.usuario_setor (codigo_usuario)
--     --     WHERE setor_padrao = true AND situacao_usuario_setor = true;
-- ============================================================================
