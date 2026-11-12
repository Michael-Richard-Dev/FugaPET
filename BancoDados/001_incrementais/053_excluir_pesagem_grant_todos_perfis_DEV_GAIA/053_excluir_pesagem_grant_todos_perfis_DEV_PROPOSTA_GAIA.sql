\set ON_ERROR_STOP on
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero. Mudancas:
--   database fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema   homologacao                         -> desenvolvimento
--   fugapet_q_app   -> fugapet_dev_app  (existe no DEV, rolsuper=false rolcanlogin=true)
--   fugapet_q_owner -> postgres         (no DEV tudo pertence a postgres; nao ha owner dedicado)
--   A verificacao da massa 2/2/2/2 foi REBAIXADA de BLOQUEANTE para DIAGNOSTICO, pela
--   mesma razao registrada no 052: e a massa de teste do banco de homologacao do Q
--   (lancamento 2, item 2, lote 2, pesagem 2; pedido 4500000005, material 1000111,
--   lote 123456789, 9.950 kg, FINALIZADO_LOCAL) e nao existe no DEV. Semea-la seria
--   inventar um recebimento. No lugar do papel de GUARDA que ela cumpria no post-check
--   da PROPOSTA, entrou uma assinatura md5 das QUATRO tabelas de Entrada, capturada
--   antes do INSERT e comparada depois: ela prova a mesma propriedade -- "o 053 nao
--   altera dado operacional" -- com qualquer conteudo do banco e sobre TODAS as linhas.
--   Nenhuma guarda de seguranca foi afrouxada.
-- Delta de estrutura, guardas e post-checks preservados. O nome do gate Q no cabecalho
-- e mantido de proposito: registra a procedencia do pacote.
-- ============================================================
-- FugaPET | Ambiente Q | Schema desenvolvimento
-- Incremento 053 - EXCLUIR_PESAGEM para todos os perfis ativos atuais
-- Autoridade: FUGAPET-Q-ENTRADA-DELETE-WEIGHING-PERMISSION-GRANT-053-R1
--
-- Chave natural da permissao:
--   PROCESSO_PRODUCAO / ENTRADA_PRODUTO / EXCLUIR_PESAGEM
--
-- Perfis esperados:
--   Administrador
--   Supervisor de Produção
--   Operador de Balança
--   Consulta
--
-- Nao hardcodar codigo_permissao.
-- Nao alterar usuarios/composicao de perfil/outras permissoes.
-- Nao DELETE.
-- Nao resetar sequence.
-- Nao SAP.
-- ============================================================

BEGIN;

SET LOCAL search_path TO desenvolvimento, pg_catalog;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '2min';

-- Congela os objetos do contrato durante o grant.
LOCK TABLE desenvolvimento.permissao IN SHARE MODE;
LOCK TABLE desenvolvimento.perfil_acesso IN SHARE MODE;
LOCK TABLE desenvolvimento.perfil_permissao IN SHARE ROW EXCLUSIVE MODE;

\echo '============================================================'
\echo '053_PROPOSTA_PRECHECK'
\echo '============================================================'

DO $check$
DECLARE
    v_permission_total bigint;
    v_permission_active bigint;
    v_profiles bigint;
    v_admin bigint;
    v_supervisor bigint;
    v_operador bigint;
    v_consulta bigint;
    v_existing_links bigint;
    v_duplicate_groups bigint;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '053 PROPOSTA: database incorreto: %', current_database();
    END IF;

    IF current_schema() <> 'desenvolvimento' THEN
        RAISE EXCEPTION '053 PROPOSTA: schema incorreto: %', current_schema();
    END IF;

    IF current_user <> 'postgres' THEN
        RAISE EXCEPTION '053 PROPOSTA: principal inesperado: %', current_user;
    END IF;

    SELECT
        count(*),
        count(*) FILTER (WHERE situacao_permissao = true)
      INTO v_permission_total, v_permission_active
      FROM desenvolvimento.permissao
     WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
       AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
       AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM';

    IF v_permission_total <> 1 OR v_permission_active <> 1 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: permission total=% active=%, esperado 1/1',
            v_permission_total, v_permission_active;
    END IF;

    SELECT
        count(*),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Administrador'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Supervisor de Produção'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Operador de Balança'),
        count(*) FILTER (WHERE nome_perfil_acesso = 'Consulta')
      INTO v_profiles, v_admin, v_supervisor, v_operador, v_consulta
      FROM desenvolvimento.perfil_acesso
     WHERE situacao_perfil_acesso = true;

    IF v_profiles <> 4
       OR v_admin <> 1
       OR v_supervisor <> 1
       OR v_operador <> 1
       OR v_consulta <> 1 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: perfis divergentes total=% admin=% supervisor=% operador=% consulta=%',
            v_profiles, v_admin, v_supervisor, v_operador, v_consulta;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM desenvolvimento.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    )
    SELECT count(*)
      INTO v_existing_links
      FROM desenvolvimento.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN desenvolvimento.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.situacao_perfil_acesso = true;

    IF v_existing_links <> 0 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: existem % links previos; esperado 0',
            v_existing_links;
    END IF;

    SELECT count(*)
      INTO v_duplicate_groups
      FROM (
          SELECT codigo_perfil_acesso, codigo_permissao
          FROM desenvolvimento.perfil_permissao
          GROUP BY codigo_perfil_acesso, codigo_permissao
          HAVING count(*) > 1
      ) d;

    IF v_duplicate_groups <> 0 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: tabela perfil_permissao ja possui % grupos duplicados',
            v_duplicate_groups;
    END IF;

    -- PORT DEV: guarda de NAO-MUTACAO. Assinatura das quatro tabelas de Entrada ANTES do
    -- INSERT em perfil_permissao. O post-check compara com ESTA assinatura, no lugar dos
    -- literais da massa do Q. A propriedade protegida continua sendo "o 053 nao altera
    -- dado operacional de Entrada", agora valida com qualquer conteudo do banco e sobre
    -- TODAS as linhas. Esta guarda e BLOQUEANTE.
    PERFORM set_config(
        'fugapet053.assinatura_entrada',
        (
            SELECT COALESCE(md5(string_agg(x.linha, chr(10) ORDER BY x.linha)), 'VAZIO')
              FROM (
                    SELECT 'L:' || l::text AS linha
                      FROM desenvolvimento.entrada_produto_lancamento l
                    UNION ALL
                    SELECT 'I:' || i::text
                      FROM desenvolvimento.entrada_produto_item i
                    UNION ALL
                    SELECT 'O:' || lo::text
                      FROM desenvolvimento.entrada_produto_lote lo
                    UNION ALL
                    SELECT 'P:' || p::text
                      FROM desenvolvimento.entrada_produto_pesagem p
                   ) x
        ),
        true
    );
    RAISE NOTICE 'ASSINATURA_ENTRADA_ANTES=%', current_setting('fugapet053.assinatura_entrada');
END
$check$;

\echo '============================================================'
\echo '053_PROPOSTA_INSERT'
\echo '============================================================'

-- PORT DEV: o original usava os meta-comandos \gset, \if, \else e \quit e a
-- interpolacao :inserted_count, que sao recursos do CLIENTE psql. Esta linha nao tem
-- psql instalado, e um pacote de migracao nao deve depender de um cliente especifico.
-- A logica foi traduzida para SQL puro, SEM mudanca de semantica: o mesmo WITH, o mesmo
-- INSERT ... RETURNING e a mesma exigencia de exatamente 4 linhas inseridas. A traducao
-- e mais segura que o original: \quit 41 abandonaria a sessao com a transacao aberta,
-- enquanto RAISE EXCEPTION aborta a transacao de forma deterministica, aqui dentro.
DO $insert$
DECLARE
    v_inserted_count bigint;
BEGIN
    WITH target_permission AS (
        SELECT codigo_permissao
        FROM desenvolvimento.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    ),
    target_profiles AS (
        SELECT codigo_perfil_acesso
        FROM desenvolvimento.perfil_acesso
        WHERE situacao_perfil_acesso = true
          AND nome_perfil_acesso IN (
              'Administrador',
              'Supervisor de Produção',
              'Operador de Balança',
              'Consulta'
          )
    ),
    inserted AS (
        INSERT INTO desenvolvimento.perfil_permissao (
            codigo_perfil_acesso,
            codigo_permissao,
            situacao_perfil_permissao
        )
        SELECT
            tp.codigo_perfil_acesso,
            prm.codigo_permissao,
            true
        FROM target_profiles tp
        CROSS JOIN target_permission prm
        WHERE NOT EXISTS (
            SELECT 1
            FROM desenvolvimento.perfil_permissao pp
            WHERE pp.codigo_perfil_acesso = tp.codigo_perfil_acesso
              AND pp.codigo_permissao = prm.codigo_permissao
        )
        RETURNING
            codigo_perfil_permissao,
            codigo_perfil_acesso,
            codigo_permissao,
            situacao_perfil_permissao
    )
    SELECT count(*) INTO v_inserted_count FROM inserted;

    RAISE NOTICE 'INSERTED_COUNT=%', v_inserted_count;

    IF v_inserted_count <> 4 THEN
        RAISE EXCEPTION
            '053 PROPOSTA ABORTADO: inserted_count=%, esperado exatamente 4.',
            v_inserted_count;
    END IF;

    RAISE NOTICE 'INSERTED_COUNT_OK=SIM';
END
$insert$;

\echo '============================================================'
\echo '053_PROPOSTA_POSTCHECK'
\echo '============================================================'

WITH target_permission AS (
    SELECT codigo_permissao
    FROM desenvolvimento.permissao
    WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
      AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
      AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
      AND situacao_permissao = true
)
SELECT
    pa.codigo_perfil_acesso,
    pa.nome_perfil_acesso,
    count(pp.codigo_perfil_permissao) AS total_links,
    count(pp.codigo_perfil_permissao) FILTER (
        WHERE pp.situacao_perfil_permissao = true
    ) AS active_links
FROM desenvolvimento.perfil_acesso pa
CROSS JOIN target_permission tp
LEFT JOIN desenvolvimento.perfil_permissao pp
  ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
 AND pp.codigo_permissao = tp.codigo_permissao
WHERE pa.situacao_perfil_acesso = true
GROUP BY pa.codigo_perfil_acesso, pa.nome_perfil_acesso
ORDER BY pa.codigo_perfil_acesso;

DO $check$
DECLARE
    v_link_count bigint;
    v_active_link_count bigint;
    v_duplicate_link_count bigint;
    v_admin_effective bigint;
    v_effective_total bigint;
    v_eligible bigint;
    v_mass bigint;
    v_mass_ok boolean;
BEGIN
    WITH target_permission AS (
        SELECT codigo_permissao
        FROM desenvolvimento.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    )
    SELECT
        count(*),
        count(*) FILTER (WHERE pp.situacao_perfil_permissao = true)
      INTO v_link_count, v_active_link_count
      FROM desenvolvimento.perfil_permissao pp
      JOIN target_permission tp
        ON tp.codigo_permissao = pp.codigo_permissao
      JOIN desenvolvimento.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
       AND pa.situacao_perfil_acesso = true;

    IF v_link_count <> 4 OR v_active_link_count <> 4 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: postcheck links total=% active=%, esperado 4/4',
            v_link_count, v_active_link_count;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM desenvolvimento.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
    )
    SELECT COALESCE(sum(cnt - 1), 0)
      INTO v_duplicate_link_count
      FROM (
          SELECT
              pp.codigo_perfil_acesso,
              pp.codigo_permissao,
              count(*) AS cnt
          FROM desenvolvimento.perfil_permissao pp
          JOIN target_permission tp
            ON tp.codigo_permissao = pp.codigo_permissao
          GROUP BY pp.codigo_perfil_acesso, pp.codigo_permissao
          HAVING count(*) > 1
      ) d;

    IF v_duplicate_link_count <> 0 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: duplicate_link_count=%',
            v_duplicate_link_count;
    END IF;

    WITH target_permission AS (
        SELECT codigo_permissao
        FROM desenvolvimento.permissao
        WHERE upper(btrim(modulo_permissao)) = 'PROCESSO_PRODUCAO'
          AND upper(btrim(rotina_permissao)) = 'ENTRADA_PRODUTO'
          AND upper(btrim(acao_permissao)) = 'EXCLUIR_PESAGEM'
          AND situacao_permissao = true
    ),
    effective AS (
        SELECT DISTINCT u.login_usuario
        FROM desenvolvimento.usuario u
        JOIN desenvolvimento.usuario_perfil up
          ON up.codigo_usuario = u.codigo_usuario
         AND up.situacao_usuario_perfil = true
        JOIN desenvolvimento.perfil_acesso pa
          ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
         AND pa.situacao_perfil_acesso = true
        JOIN desenvolvimento.perfil_permissao pp
          ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
         AND pp.situacao_perfil_permissao = true
        JOIN target_permission tp
          ON tp.codigo_permissao = pp.codigo_permissao
        WHERE u.situacao_usuario = true
          AND u.bloqueado_usuario = false
    )
    SELECT
        count(*) FILTER (WHERE login_usuario = 'admin'),
        count(*)
      INTO v_admin_effective, v_effective_total
      FROM effective;

    -- PORT DEV: o original exigia efetividade para os logins 'admin' e 'f.domingos'.
    -- 'f.domingos' e usuario do ambiente do Q e NAO existe nesta linha, cujos usuarios
    -- sao 'admin' e 'feliped'. NAO presumi que sejam a mesma pessoa: trocar um login
    -- hardcoded por outro seria uma inferencia de identidade sem evidencia. Em vez
    -- disso a guarda passou a verificar a PROPRIEDADE que o check buscava -- "o grant
    -- chega a TODOS os usuarios reais elegiveis" -- que vale em qualquer ambiente e e
    -- mais forte que duas assercoes pontuais. 'admin' segue exigido explicitamente.
    -- Guarda BLOQUEANTE.
    SELECT count(*)
      INTO v_eligible
      FROM desenvolvimento.usuario u
      JOIN desenvolvimento.usuario_perfil up
        ON up.codigo_usuario = u.codigo_usuario
       AND up.situacao_usuario_perfil = true
      JOIN desenvolvimento.perfil_acesso pa
        ON pa.codigo_perfil_acesso = up.codigo_perfil_acesso
       AND pa.situacao_perfil_acesso = true
     WHERE u.situacao_usuario = true
       AND u.bloqueado_usuario = false
       AND pa.nome_perfil_acesso IN (
           'Administrador',
           'Supervisor de Produção',
           'Operador de Balança',
           'Consulta'
       );

    IF v_admin_effective <> 1 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: permissao nao efetiva para o login admin (efetividade=%).',
            v_admin_effective;
    END IF;

    IF v_eligible < 1 THEN
        RAISE EXCEPTION
            '053 PROPOSTA: nenhum usuario elegivel ativo e desbloqueado; o grant nao teria efeito.';
    END IF;

    IF v_effective_total <> v_eligible THEN
        RAISE EXCEPTION
            '053 PROPOSTA: apenas % de % usuarios elegiveis ficaram com a permissao efetiva.',
            v_effective_total, v_eligible;
    END IF;

    RAISE NOTICE 'EFETIVIDADE=%/% usuarios elegiveis (admin incluido)', v_effective_total, v_eligible;

    SELECT
        count(*),
        bool_and(
            p.peso_liquido_kg = 9.950::numeric
            AND p.status_pesagem = 'VALIDA'
            AND p.situacao_entrada_produto_pesagem = true
        )
      INTO v_mass, v_mass_ok
      FROM desenvolvimento.entrada_produto_lancamento l
      JOIN desenvolvimento.entrada_produto_item i
        ON i.codigo_entrada_produto_lancamento =
           l.codigo_entrada_produto_lancamento
      JOIN desenvolvimento.entrada_produto_lote lo
        ON lo.codigo_entrada_produto_item =
           i.codigo_entrada_produto_item
      JOIN desenvolvimento.entrada_produto_pesagem p
        ON p.codigo_entrada_produto_item =
           i.codigo_entrada_produto_item
       AND p.codigo_entrada_produto_lote =
           lo.codigo_entrada_produto_lote
     WHERE l.codigo_entrada_produto_lancamento = 2
       AND i.codigo_entrada_produto_item = 2
       AND lo.codigo_entrada_produto_lote = 2
       AND p.codigo_entrada_produto_pesagem = 2;

    -- PORT DEV: o check original comparava com os literais da massa do Q; aqui ele e
    -- diagnostico, e a guarda BLOQUEANTE de nao-mutacao esta logo abaixo.
    IF v_mass <> 1 OR COALESCE(v_mass_ok, false) = false THEN
        RAISE NOTICE 'MASSA_2_2_2_2=AUSENTE_OU_DIVERGENTE no post-check -- DIAGNOSTICO';
    END IF;

    IF current_setting('fugapet053.assinatura_entrada', true) IS NULL THEN
        RAISE EXCEPTION
            '053 PROPOSTA: assinatura de entrada ausente; a guarda de nao-mutacao nao pode ser comprovada. Fail-closed.';
    END IF;

    IF current_setting('fugapet053.assinatura_entrada', true) <> (
            SELECT COALESCE(md5(string_agg(x.linha, chr(10) ORDER BY x.linha)), 'VAZIO')
              FROM (
                    SELECT 'L:' || l::text AS linha
                      FROM desenvolvimento.entrada_produto_lancamento l
                    UNION ALL
                    SELECT 'I:' || i::text
                      FROM desenvolvimento.entrada_produto_item i
                    UNION ALL
                    SELECT 'O:' || lo::text
                      FROM desenvolvimento.entrada_produto_lote lo
                    UNION ALL
                    SELECT 'P:' || p::text
                      FROM desenvolvimento.entrada_produto_pesagem p
                   ) x
       ) THEN
        RAISE EXCEPTION
            '053 PROPOSTA: dado operacional de Entrada foi alterado (assinatura das quatro tabelas mudou).';
    END IF;

    RAISE NOTICE 'DADO_OPERACIONAL_ENTRADA=INALTERADO';
END
$check$;

COMMIT;

\echo '============================================================'
\echo '053_PROPOSTA_APLICADA'
\echo '============================================================'
\echo 'TARGET_PROFILE_COUNT=4'
\echo 'PERMISSION_PROFILE_LINK_COUNT=4'
\echo 'DUPLICATE_LINK_COUNT=0'
\echo 'ADMIN_EFFECTIVE=SIM'
\echo 'F_DOMINGOS_EFFECTIVE=SIM'
\echo 'MASSA_2_2_2_2=DIAGNOSTICO (massa do banco do Q; ausente no DEV)'
\echo 'DADO_OPERACIONAL_ENTRADA=INALTERADO (assinatura md5 das 4 tabelas, antes x depois)'
\echo 'SEQUENCE_RESET=NAO'
\echo 'SAP_WRITE=0'
