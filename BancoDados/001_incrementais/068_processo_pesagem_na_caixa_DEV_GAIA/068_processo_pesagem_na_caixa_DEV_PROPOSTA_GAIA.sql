\set ON_ERROR_STOP on
-- ============================================================
-- 068_processo_pesagem_na_caixa_DEV_PROPOSTA_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 068 - O indice de terminal ativo passa a saber o PROCESSO
-- PROPOSTA (ESCRITA) - transacao unica
--
-- POR QUE EXISTE: o envio unico (processo ENVIO_UNICO do vinculo 065) acumula caixas
-- FINALIZADAS E SEM INTEGRAR ate o operador enviar todas no fim. O indice de hoje
--
--     uq_hu_caixa_terminal_ativo ON hu_caixa (upper(btrim(terminal)))
--     WHERE status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA')
--
-- admite UMA caixa nao-confirmada por terminal. Sob ele o envio unico e
-- estruturalmente impossivel: a SEGUNDA caixa ja e recusada pelo banco. Encontrado em
-- 19/11/2026, com 25 caixas pesadas e a pesagem travada na caixa seguinte.
--
-- A ESCOLHA: o indice passa a aplicar DUAS regras, pelo processo gravado na caixa.
--   CAIXA_A_CAIXA (e caixa sem processo registrado): regra IDENTICA a de hoje. A
--     protecao do caixa a caixa nao e afrouxada em nada.
--   ENVIO_UNICO: ocupa o terminal so o que EXIGE DECISAO HUMANA — caixa aberta na
--     balanca e envio em curso. Acumular pendente de envio e o processo, nao um
--     defeito a impedir.
--
-- A alternativa descartada era estreitar o indice para todos os processos. Seria um
-- incremento menor, mas tiraria do BANCO a autoridade sobre "nao deixe caixa
-- finalizada sem integrar" no caixa a caixa, deixando so a guarda da tela. Decisao do
-- usuario em 19/11/2026: preservar a protecao existente.
--
-- NULL = processo nao registrado, que e o caso de toda caixa anterior a este
-- incremento. 'IS DISTINCT FROM' trata NULL como caixa a caixa, entao as linhas
-- antigas continuam exatamente sob a regra sob a qual nasceram. Nenhuma linha e
-- reescrita por este incremento.
--
-- RISCO DE VIOLACAO AO CRIAR O INDICE: nenhum. O conjunto indexado so DIMINUI, e so
-- para linhas ENVIO_UNICO, que ainda nao existem.
--
-- PRIVILEGIO POR COLUNA: esta base concede INSERT por COLUNA em hu_caixa (SELECT de
-- tabela + 22 colunas com INSERT). Coluna nova SEM grant explicito faz a pesagem
-- falhar no INSERT, em runtime, na mao do operador — e nenhuma verificacao de catalogo
-- pega isso. O GRANT abaixo nao e opcional.
--
-- Leia o PREFLIGHT antes.
-- ============================================================

BEGIN;

DO $guarda$
BEGIN
    IF current_database() <> 'fuga_jales_local_desenvolvimento' THEN
        RAISE EXCEPTION '068 ABORTADO: alvo errado. Conectado em %.', current_database();
    END IF;

    IF to_regclass('desenvolvimento.hu_caixa') IS NULL THEN
        RAISE EXCEPTION '068 ABORTADO: tabela hu_caixa nao existe.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') THEN
        RAISE EXCEPTION '068 ABORTADO: papel fugapet_dev_app nao existe.';
    END IF;
END
$guarda$;

ALTER TABLE desenvolvimento.hu_caixa
    ADD COLUMN IF NOT EXISTS processo_pesagem varchar(20) NULL;

COMMENT ON COLUMN desenvolvimento.hu_caixa.processo_pesagem IS
    'Processo sob o qual a caixa foi aberta (do vinculo 065). NULO = nao registrado, tratado como CAIXA_A_CAIXA. Governa o indice de terminal ativo.';

DO $check$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid='desenvolvimento.hu_caixa'::regclass
           AND conname='ck_hu_caixa_processo_pesagem')
    THEN
        ALTER TABLE desenvolvimento.hu_caixa
            ADD CONSTRAINT ck_hu_caixa_processo_pesagem
            CHECK (processo_pesagem IS NULL
                   OR processo_pesagem IN ('CAIXA_A_CAIXA','ENVIO_UNICO'));
    END IF;
END
$check$;

-- O indice novo. DROP + CREATE porque predicado de indice parcial nao se altera.
DROP INDEX IF EXISTS desenvolvimento.uq_hu_caixa_terminal_ativo;

CREATE UNIQUE INDEX uq_hu_caixa_terminal_ativo
    ON desenvolvimento.hu_caixa (upper(btrim(terminal)))
 WHERE (
         -- Caixa a caixa e caixa sem processo registrado: a regra de sempre.
         processo_pesagem IS DISTINCT FROM 'ENVIO_UNICO'
         AND status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA')
       )
    OR (
         -- Envio unico: so ocupa o terminal o que exige decisao humana.
         processo_pesagem = 'ENVIO_UNICO'
         AND status_hu_caixa IN ('EM_PESAGEM','ENVIANDO_SAP')
       );

-- Sem este GRANT a pesagem quebra no INSERT: a base concede INSERT por COLUNA.
GRANT INSERT (processo_pesagem) ON desenvolvimento.hu_caixa TO fugapet_dev_app;
GRANT SELECT (processo_pesagem) ON desenvolvimento.hu_caixa TO fugapet_dev_app;

COMMIT;

\echo '== 068 aplicado. Rode a VALIDACAO. =='
