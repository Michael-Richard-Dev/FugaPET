\set ON_ERROR_STOP on
-- ============================================================
-- 059_permissao_habilitar_escrita_sap_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 059 - Permissao HABILITAR_ESCRITA_SAP
-- ROLLBACK (ESCRITA) - transacao unica
--
-- Desfaz exatamente o que o PROPOSTA fez: remove as 2 concessoes ao Administrador
-- e as 2 permissoes HABILITAR_ESCRITA_SAP. Nada mais.
--
-- Efeito: o DEV volta a NAO conseguir armar a escrita SAP 101. A tela volta a
-- responder "Usuario sem permissao para habilitar a escrita SAP". Isto e a volta ao
-- estado fail-closed, nao uma avaria.
--
-- GUARDA IMPORTANTE: se a permissao tiver sido concedida a algum perfil ALEM do
-- Administrador depois do 059, isto NAO e mais o estado que este pacote criou.
-- O rollback aborta e pede decisao humana, em vez de apagar concessao de terceiro.
-- ============================================================

BEGIN;

DO $$
DECLARE
    v_outros text;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION
            '059 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    SELECT string_agg(DISTINCT pa.nome_perfil_acesso, ', ') INTO v_outros
      FROM desenvolvimento.perfil_permissao pp
      JOIN desenvolvimento.perfil_acesso pa ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
      JOIN desenvolvimento.permissao p      ON p.codigo_permissao = pp.codigo_permissao
     WHERE p.acao_permissao = 'HABILITAR_ESCRITA_SAP'
       AND pa.nome_perfil_acesso <> 'Administrador';

    IF v_outros IS NOT NULL THEN
        RAISE EXCEPTION
            '059 ROLLBACK ABORTADO: a permissao tambem esta com %. Isso nao veio do 059; '
            'decida manualmente antes de remover.', v_outros;
    END IF;

    RAISE NOTICE 'GUARDAS=OK  so ha concessao ao Administrador';
END
$$;

-- 1. As concessoes primeiro (FK RESTRICT impede a ordem inversa).
DELETE FROM desenvolvimento.perfil_permissao pp
 USING desenvolvimento.permissao p
 WHERE p.codigo_permissao = pp.codigo_permissao
   AND p.modulo_permissao = 'PROCESSO_PRODUCAO'
   AND p.acao_permissao   = 'HABILITAR_ESCRITA_SAP';

-- 2. As permissoes.
DELETE FROM desenvolvimento.permissao
 WHERE modulo_permissao = 'PROCESSO_PRODUCAO'
   AND acao_permissao   = 'HABILITAR_ESCRITA_SAP'
   AND rotina_permissao IN ('ENTRADA_PRODUTO', 'CONTROLE_APONTAMENTOS');

-- GUARDA DE SAIDA
DO $$
DECLARE
    v_restou integer;
    v_enviar integer;
BEGIN
    SELECT count(*) INTO v_restou
      FROM desenvolvimento.permissao
     WHERE acao_permissao = 'HABILITAR_ESCRITA_SAP';

    IF v_restou <> 0 THEN
        RAISE EXCEPTION '059 ROLLBACK ABORTADO: sobraram % permissoes.', v_restou;
    END IF;

    -- O que nao era do 059 tem de continuar de pe.
    SELECT count(*) INTO v_enviar
      FROM desenvolvimento.permissao
     WHERE acao_permissao = 'ENVIAR_SAP';

    IF v_enviar <> 1 THEN
        RAISE EXCEPTION
            '059 ROLLBACK ABORTADO: ENVIAR_SAP deveria continuar existindo (1 linha); ha %.',
            v_enviar;
    END IF;

    RAISE NOTICE 'SAIDA=OK  059 revertido; ENVIAR_SAP intacta';
END
$$;

COMMIT;

\echo ''
\echo '=== 059 revertido. O DEV voltou a NAO poder armar a escrita SAP 101. ==='
\echo ''
