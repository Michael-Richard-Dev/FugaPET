\set ON_ERROR_STOP on
-- ============================================================
-- 072_correcao_resultado_apontamento_Q_ROLLBACK_GAIA.sql
-- FugaPET | Ambientes QAS110 e QAS130 | Schema homologacao
-- Incremento 072 (Q) - ROLLBACK
--
-- Devolve a restricao UNIQUE (codigo_apontamento) e remove o indice de busca.
--
-- ATENCAO — ESTE ROLLBACK PODE SER IMPOSSIVEL, E ISSO E CORRETO.
--
-- Se algum apontamento ja recebeu uma CORRECAO, existem dois ou mais resultados
-- para ele, e a restricao nao pode voltar sem que alguem decida QUAL linha
-- descartar. Esse descarte nao e decisao de script: a tabela e append-only e cada
-- linha registra o que alguem informou e quando.
--
-- Por isso este script NAO apaga nada. Ele aborta, listando os apontamentos em
-- conflito, para que a decisao seja tomada por gente olhando os dados.
--
-- Reverter o banco sem reverter a aplicacao tambem quebra a tela de correcao: ela
-- passaria a tentar um INSERT que a restricao recusa. Reverta os dois juntos.
-- ============================================================

BEGIN;

DO $guarda$
DECLARE
    conflitos bigint := 0;
    exemplo text := '';
BEGIN
    IF current_database() NOT IN ('fuga_jales_local_homologacao_q_v1_2',
                                  'fuga_jales_local_homologacao_q130_v1_3') THEN
        RAISE EXCEPTION '072 Q ROLLBACK ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('homologacao.operacao_resultado_registro') IS NULL THEN
        RAISE EXCEPTION '072 Q ROLLBACK ABORTADO: operacao_resultado_registro ausente.';
    END IF;

    SELECT count(*), COALESCE(string_agg(codigo_apontamento::text, ', '), '')
      INTO conflitos, exemplo
      FROM (SELECT codigo_apontamento
              FROM homologacao.operacao_resultado_registro
             GROUP BY codigo_apontamento HAVING count(*) > 1
             LIMIT 20) d;

    IF conflitos > 0 THEN
        RAISE EXCEPTION
            '072 Q ROLLBACK ABORTADO: % apontamento(s) ja tem correcao e perderiam registro. Apontamentos: %. Decida com gente qual linha fica antes de reverter.',
            conflitos, exemplo;
    END IF;
END
$guarda$;

DROP INDEX IF EXISTS homologacao.ix_resultado_registro_apontamento;

ALTER TABLE homologacao.operacao_resultado_registro
    ADD CONSTRAINT uq_operacao_resultado_registro_apontamento
    UNIQUE (codigo_apontamento);

COMMENT ON TABLE homologacao.operacao_resultado_registro IS
    'Cabecalho append-only do resultado registrado para um apontamento.';

COMMIT;

\echo '== 072 revertido: um resultado por apontamento de novo. =='
\echo '== REVERTA TAMBEM A APLICACAO: a tela de correcao tentaria um INSERT recusado. =='
