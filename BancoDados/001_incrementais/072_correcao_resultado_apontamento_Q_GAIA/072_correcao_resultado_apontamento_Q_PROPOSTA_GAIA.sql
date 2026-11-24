\set ON_ERROR_STOP on
-- ============================================================
-- 072_correcao_resultado_apontamento_Q_PROPOSTA_GAIA.sql
-- FugaPET | Ambientes QAS110 e QAS130 | Schema homologacao
-- Incremento 072 (Q) - Correcao do resultado do apontamento
-- PROPOSTA (ESCRITA) - transacao unica
--
-- O QUE MUDA: sai a restricao UNIQUE (codigo_apontamento) de
-- operacao_resultado_registro, e entra um indice de busca por apontamento.
--
-- POR QUE (relato do usuario em 24/11/2026): "na tela de Tempo no consumo so
-- esta cadastrando 5, mesmo o usuario colocando outro valor". A causa nao era a
-- tela. Com um resultado por apontamento garantido pelo banco, a tela reencontrava
-- o resultado gravado, mostrava-o e TRAVAVA a grade. O primeiro valor registrado
-- virava definitivo — o operador digitava outro tempo e nada mudava.
--
-- O QUE NAO MUDA, E E O PONTO: a tabela continua APPEND-ONLY. Corrigir nao apaga
-- nem reescreve o registro anterior; ele permanece, e deixa apenas de ser o valor
-- corrente. Quem auditar ve a correcao E o que ela substituiu. Um UPDATE no lugar
-- disso seria mais simples e perderia exatamente essa informacao.
--
-- QUEM E O VALOR CORRENTE: o de MAIOR codigo_resultado do apontamento. O codigo e
-- identity, entao cresce com o tempo de gravacao; a aplicacao le por ele, e nao
-- por registrado_em, para nao empatar entre duas gravacoes no mesmo instante.
--
-- ============================================================
-- ESTE INCREMENTO AFROUXA UMA GARANTIA DE INTEGRIDADE.
-- Depois dele, nada no banco impede dois resultados para o mesmo apontamento —
-- e e isso que se quer. Quem ler a tabela SEM pegar o mais recente passara a ver
-- linhas antigas como se fossem correntes. Os pontos de leitura conhecidos estao
-- em ResultadoApontamentoRepositorio e ResultadoApontamentoServico, e ambos foram
-- ajustados junto com este incremento.
-- ============================================================
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $guarda$
DECLARE
    papel text;
BEGIN
    papel := CASE current_database()
        WHEN 'fuga_jales_local_homologacao_q_v1_2'    THEN 'fugapet_q_app'
        WHEN 'fuga_jales_local_homologacao_q130_v1_3' THEN 'fugapet_q130_app'
    END;

    IF papel IS NULL THEN
        RAISE EXCEPTION '072 Q ABORTADO: alvo nao reconhecido. Conectado em %.', current_database();
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = papel) THEN
        RAISE EXCEPTION '072 Q ABORTADO: papel % nao existe neste banco.', papel;
    END IF;

    -- Sem as tabelas do 041 nao ha o que ajustar, e criar aqui seria reconstruir
    -- schema por conta propria em vez de aplicar o incremento que o define.
    IF to_regclass('homologacao.operacao_resultado_registro') IS NULL THEN
        RAISE EXCEPTION '072 Q ABORTADO: operacao_resultado_registro ausente. Aplique o 041 antes.';
    END IF;
END
$guarda$;

-- ------------------------------------------------------------
-- 1. Sai a unicidade por apontamento
-- ------------------------------------------------------------
-- IF EXISTS para o incremento ser reaplicavel sem erro.
ALTER TABLE homologacao.operacao_resultado_registro
    DROP CONSTRAINT IF EXISTS uq_operacao_resultado_registro_apontamento;

-- ------------------------------------------------------------
-- 2. Entra o indice de busca
-- ------------------------------------------------------------
-- A restricao UNIQUE tambem servia de indice para a busca por apontamento, que a
-- tela faz toda vez que abre. Tirando-a sem por outro, a consulta passaria a
-- varrer a tabela inteira — o ajuste ficaria correto e lento.
CREATE INDEX IF NOT EXISTS ix_resultado_registro_apontamento
    ON homologacao.operacao_resultado_registro (codigo_apontamento, codigo_resultado DESC);

COMMENT ON INDEX homologacao.ix_resultado_registro_apontamento IS
    'Busca dos resultados de um apontamento, do mais recente para o mais antigo. O corrente e o de maior codigo_resultado (incremento 072).';

COMMENT ON TABLE homologacao.operacao_resultado_registro IS
    'Resultados registrados por apontamento, APPEND-ONLY. Desde o incremento 072 um apontamento pode ter varios: o corrente e o de MAIOR codigo_resultado, e os anteriores ficam como historico da correcao.';

COMMIT;

\echo '== 072 aplicado. Rode a VALIDACAO. =='
\echo '== LEMBRE: a partir daqui o banco ACEITA varios resultados por apontamento. =='
\echo '== Quem ler a tabela precisa pegar o de MAIOR codigo_resultado.            =='
