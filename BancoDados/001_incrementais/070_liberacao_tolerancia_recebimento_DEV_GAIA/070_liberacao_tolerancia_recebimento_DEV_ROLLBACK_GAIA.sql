\set ON_ERROR_STOP on
-- ============================================================
-- 070_liberacao_tolerancia_recebimento_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 070 - ROLLBACK
--
-- ATENCAO: APAGA LOG DE AUDITORIA. Cada linha registra uma decisao gerencial de
-- liberar um estouro de tolerancia — ou uma tentativa recusada. E exatamente o
-- tipo de registro que existe para ser consultado DEPOIS, quando alguem pergunta
-- quem autorizou aquele recebimento fora do limite.
--
-- Por isso RECUSA enquanto houver linha. Para desfazer mesmo assim:
--   psql -v confirmo_perder_log=sim -f <este arquivo>
--
-- O PERFIL E A PERMISSAO SO SAO REMOVIDOS SE NINGUEM OS ESTIVER USANDO: um
-- perfil Gerente com usuarios vinculados nao e apagado aqui, porque removê-lo
-- tiraria acesso de pessoas sem que ninguem tivesse pedido isso.
--
-- EXIGE psql (a trava depende de variavel do cliente).
-- ============================================================

\if :{?confirmo_perder_log}
\else
\set confirmo_perder_log nao
\endif

BEGIN;

DO $$
DECLARE
    linhas   bigint := 0;
    usuarios int := 0;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '070 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.liberacao_tolerancia_recebimento') IS NOT NULL THEN
        SELECT count(*) INTO linhas FROM desenvolvimento.liberacao_tolerancia_recebimento;

        IF linhas > 0 AND :'confirmo_perder_log' <> 'sim' THEN
            RAISE EXCEPTION
                '070 ROLLBACK ABORTADO: % liberacao(oes) auditadas seriam perdidas. Rode com -v confirmo_perder_log=sim se e isso mesmo.',
                linhas;
        END IF;

        IF linhas > 0 THEN
            RAISE WARNING '070 ROLLBACK: apagando % registro(s) de auditoria, por confirmacao explicita.', linhas;
        END IF;
    END IF;

    SELECT count(*) INTO usuarios
      FROM desenvolvimento.perfil_acesso pa
      JOIN desenvolvimento.usuario_perfil up ON up.codigo_perfil_acesso = pa.codigo_perfil_acesso
     WHERE pa.nome_perfil_acesso='Gerente' AND up.situacao_usuario_perfil IS TRUE;

    IF usuarios > 0 THEN
        RAISE WARNING '070 ROLLBACK: perfil Gerente tem % usuario(s) vinculado(s) e NAO sera removido.', usuarios;
    END IF;
END
$$;

DROP TABLE IF EXISTS desenvolvimento.liberacao_tolerancia_recebimento;

-- Vinculo e perfil saem SO quando ninguem os usa.
DELETE FROM desenvolvimento.perfil_permissao pp
 USING desenvolvimento.perfil_acesso pa, desenvolvimento.permissao p
 WHERE pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
   AND pp.codigo_permissao     = p.codigo_permissao
   AND pa.nome_perfil_acesso   = 'Gerente'
   AND p.acao_permissao        = 'LIBERAR_TOLERANCIA'
   AND NOT EXISTS (SELECT 1 FROM desenvolvimento.usuario_perfil up
                    WHERE up.codigo_perfil_acesso = pa.codigo_perfil_acesso
                      AND up.situacao_usuario_perfil IS TRUE);

DELETE FROM desenvolvimento.perfil_acesso pa
 WHERE pa.nome_perfil_acesso='Gerente'
   AND NOT EXISTS (SELECT 1 FROM desenvolvimento.usuario_perfil up
                    WHERE up.codigo_perfil_acesso = pa.codigo_perfil_acesso)
   AND NOT EXISTS (SELECT 1 FROM desenvolvimento.perfil_permissao pp
                    WHERE pp.codigo_perfil_acesso = pa.codigo_perfil_acesso);

DELETE FROM desenvolvimento.permissao
 WHERE modulo_permissao='PROCESSO_PRODUCAO'
   AND rotina_permissao='ENTRADA_PRODUTO'
   AND acao_permissao='LIBERAR_TOLERANCIA'
   AND NOT EXISTS (SELECT 1 FROM desenvolvimento.perfil_permissao pp
                    WHERE pp.codigo_permissao = permissao.codigo_permissao);

COMMIT;

\echo '== 070 revertido. =='
