\set ON_ERROR_STOP on
-- ============================================================
-- 059_permissao_habilitar_escrita_sap_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 059 - Permissao HABILITAR_ESCRITA_SAP
-- PROPOSTA (ESCRITA) - transacao unica, tudo ou nada
--
-- O QUE FAZ, exatamente:
--   1. Insere 2 permissoes, se ausentes:
--        PROCESSO_PRODUCAO | ENTRADA_PRODUTO       | HABILITAR_ESCRITA_SAP
--        PROCESSO_PRODUCAO | CONTROLE_APONTAMENTOS | HABILITAR_ESCRITA_SAP
--   2. Concede as duas ao perfil "Administrador", e a mais ninguem.
--
-- O QUE NAO FAZ:
--   - Nao altera, desativa ou remove nenhuma permissao existente.
--   - Nao concede nada ao perfil "Operador de Balanca" nem a qualquer outro.
--     O Q nao concede, e replicar o Q e o criterio deste pacote.
--   - Nao mexe em usuario, usuario_perfil, auditoria ou qualquer tabela de processo.
--
-- IDEMPOTENTE: rodar duas vezes nao duplica nada e nao e erro.
--
-- LEIA O PREFLIGHT ANTES. Ele prova o alvo, a ausencia da permissao, a unicidade do
-- perfil Administrador e que as sequences estao a frente do maior codigo em uso.
--
-- CONSEQUENCIA DE SEGURANCA, dita sem rodeio: depois deste pacote, um usuario do
-- perfil Administrador passa a conseguir ARMAR a escrita SAP 101 pela tela. O arme
-- continua sendo one-shot, com TTL, auditado de forma duravel e fail-closed, e so
-- acontece apos confirmacao explicita do operador na propria tela. Mas a barreira
-- "ninguem neste banco pode armar" deixa de existir.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- GUARDAS DE ENTRADA
-- ------------------------------------------------------------
DO $$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            '059 ABORTADO: alvo errado. Esperado fuga_jales_local_desenvolvimento, conectado em %.',
            current_database();
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM desenvolvimento.permissao
         WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
           AND rotina_permissao = 'ENTRADA_PRODUTO'
           AND acao_permissao   = 'ENVIAR_SAP'
    ) THEN
        RAISE EXCEPTION
            '059 ABORTADO: nao encontrei ENVIAR_SAP em ENTRADA_PRODUTO. Este banco nao '
            'tem o seed de permissoes esperado; investigue antes de inserir.';
    END IF;

    IF (SELECT count(*) FROM desenvolvimento.perfil_acesso
         WHERE nome_perfil_acesso = 'Administrador' AND situacao_perfil_acesso IS TRUE) <> 1 THEN
        RAISE EXCEPTION
            '059 ABORTADO: esperava exatamente 1 perfil ativo chamado Administrador.';
    END IF;

    RAISE NOTICE 'GUARDAS=OK';
END
$$;

-- ------------------------------------------------------------
-- 1. AS PERMISSOES
--    Sem ON CONFLICT: nao ha constraint unica sobre a tripla nesta
--    tabela, entao o controle e o WHERE NOT EXISTS.
-- ------------------------------------------------------------
INSERT INTO desenvolvimento.permissao (
    codigo_permissao, modulo_permissao, rotina_permissao, acao_permissao,
    descricao_permissao, situacao_permissao,
    permissao_criado_por, permissao_criado_em)
SELECT nextval('desenvolvimento.permissao_codigo_permissao_seq'),
       novas.modulo, novas.rotina, novas.acao, novas.descricao, true,
       NULL, now()
  FROM (VALUES
        ('PROCESSO_PRODUCAO', 'ENTRADA_PRODUTO', 'HABILITAR_ESCRITA_SAP',
         'Habilitar uma tentativa temporaria de escrita SAP 101 no ambiente DEV.'),
        ('PROCESSO_PRODUCAO', 'CONTROLE_APONTAMENTOS', 'HABILITAR_ESCRITA_SAP',
         NULL)
       ) AS novas(modulo, rotina, acao, descricao)
 WHERE NOT EXISTS (
        SELECT 1 FROM desenvolvimento.permissao p
         WHERE p.modulo_permissao = novas.modulo
           AND p.rotina_permissao = novas.rotina
           AND p.acao_permissao   = novas.acao);

-- ------------------------------------------------------------
-- 2. A CONCESSAO, so ao Administrador
-- ------------------------------------------------------------
INSERT INTO desenvolvimento.perfil_permissao (
    codigo_perfil_permissao, codigo_perfil_acesso, codigo_permissao,
    situacao_perfil_permissao,
    perfil_permissao_criado_por, perfil_permissao_criado_em)
SELECT nextval('desenvolvimento.perfil_permissao_codigo_perfil_permissao_seq'),
       pa.codigo_perfil_acesso, p.codigo_permissao, true,
       NULL, now()
  FROM desenvolvimento.permissao p
 CROSS JOIN desenvolvimento.perfil_acesso pa
 WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
   AND p.acao_permissao   = 'HABILITAR_ESCRITA_SAP'
   AND pa.nome_perfil_acesso = 'Administrador'
   AND NOT EXISTS (
        SELECT 1 FROM desenvolvimento.perfil_permissao pp
         WHERE pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
           AND pp.codigo_permissao     = p.codigo_permissao);

-- ------------------------------------------------------------
-- GUARDA DE SAIDA
-- Exige o estado final EXATO. Qualquer excesso derruba a transacao.
-- ------------------------------------------------------------
DO $$
DECLARE
    v_permissoes   integer;
    v_concessoes   integer;
    v_outros_perfis text;
BEGIN
    SELECT count(*) INTO v_permissoes
      FROM desenvolvimento.permissao
     WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
       AND acao_permissao   = 'HABILITAR_ESCRITA_SAP'
       AND rotina_permissao IN ('ENTRADA_PRODUTO', 'CONTROLE_APONTAMENTOS')
       AND situacao_permissao IS TRUE;

    IF v_permissoes <> 2 THEN
        RAISE EXCEPTION
            '059 ABORTADO: esperava 2 permissoes HABILITAR_ESCRITA_SAP ativas; encontrei %.',
            v_permissoes;
    END IF;

    SELECT count(*) INTO v_concessoes
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
      JOIN desenvolvimento.permissao p
        ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'HABILITAR_ESCRITA_SAP'
       AND pa.nome_perfil_acesso = 'Administrador'
       AND pp.situacao_perfil_permissao IS TRUE;

    IF v_concessoes <> 2 THEN
        RAISE EXCEPTION
            '059 ABORTADO: esperava 2 concessoes ao Administrador; encontrei %.',
            v_concessoes;
    END IF;

    -- Nenhum outro perfil pode ter saido daqui com escrita SAP.
    SELECT string_agg(DISTINCT pa.nome_perfil_acesso, ', ') INTO v_outros_perfis
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.perfil_acesso pa
        ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
      JOIN desenvolvimento.permissao p
        ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'HABILITAR_ESCRITA_SAP'
       AND pa.nome_perfil_acesso <> 'Administrador';

    IF v_outros_perfis IS NOT NULL THEN
        RAISE EXCEPTION
            '059 ABORTADO: HABILITAR_ESCRITA_SAP chegou a outros perfis: %.', v_outros_perfis;
    END IF;

    RAISE NOTICE 'SAIDA=OK  2 permissoes, 2 concessoes, somente Administrador';
END
$$;

COMMIT;

\echo ''
\echo '=== 059 PROPOSTA aplicado. Rode o VALIDACAO. ==='
\echo ''
