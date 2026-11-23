\set ON_ERROR_STOP on
-- ============================================================
-- 069_tolerancia_recebimento_material_DEV_ROLLBACK_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 069 - ROLLBACK
--
-- ATENCAO: APAGA CADASTRO. A tabela guarda decisoes do usuario (a tolerancia de
-- cada material), nao dados derivados — nada as reconstroi. Por isso este script
-- NAO roda sozinho: ele RECUSA enquanto houver linha, e so segue com o
-- reconhecimento explicito abaixo.
--
-- Para desfazer MESMO com cadastro gravado, rode com a variavel ligada:
--   psql -v confirmo_perder_cadastro=sim -f <este arquivo>
--
-- Antes disso, considere a alternativa que nao perde nada: deixar a tabela e
-- desligar o uso no aplicativo. Uma tabela sem leitor nao atrapalha ninguem.
--
-- ESTE ARQUIVO EXIGE psql, ao contrario dos outros tres do 069. A trava depende da
-- variavel :confirmo_perder_cadastro, que e substituida pelo CLIENTE psql antes de
-- o servidor ver o texto. Rodado por um cliente que so manda SQL (o sqlrunner do
-- scratchpad, por exemplo, que descarta as linhas iniciadas por "\"), a variavel
-- nao e substituida e o script falha — o que e o desfecho seguro, mas confuso se
-- vier de surpresa. Em maquina sem psql, rode na VM do banco.
-- ============================================================

\if :{?confirmo_perder_cadastro}
\else
\set confirmo_perder_cadastro nao
\endif

BEGIN;

DO $$
DECLARE
    linhas bigint := 0;
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '069 ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.tolerancia_recebimento_material') IS NULL THEN
        RAISE NOTICE '069 ROLLBACK: tabela ja nao existe. Nada a fazer.';
        RETURN;
    END IF;

    SELECT count(*) INTO linhas FROM desenvolvimento.tolerancia_recebimento_material;

    IF linhas > 0 AND :'confirmo_perder_cadastro' <> 'sim' THEN
        RAISE EXCEPTION
            '069 ROLLBACK ABORTADO: % linha(s) de cadastro seriam perdidas. Rode com -v confirmo_perder_cadastro=sim se e isso mesmo.',
            linhas;
    END IF;

    IF linhas > 0 THEN
        RAISE WARNING '069 ROLLBACK: apagando % linha(s) de cadastro, por confirmacao explicita.', linhas;
    END IF;
END
$$;

DROP TABLE IF EXISTS desenvolvimento.tolerancia_recebimento_material;

COMMIT;

\echo '== 069 revertido. =='
