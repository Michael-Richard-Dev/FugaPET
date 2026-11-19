\set ON_ERROR_STOP on
-- ============================================================
-- 068_processo_pesagem_na_caixa_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 068 - VALIDACAO (SOMENTE LEITURA)
--
-- PROVA as regras seguindo a PROGRESSAO REAL da caixa, e nao inventando estados.
--
-- A primeira versao deste arquivo inseria caixas ja em AGUARDANDO_AUTORIZACAO_SAP e
-- falhou: o gatilho fn_hu_caixa_preparar_insert FORCA status_hu_caixa := 'EM_PESAGEM'
-- em todo INSERT. As tres "pendentes" viraram tres caixas abertas na balanca, que o
-- indice deve mesmo recusar. A caixa so chega a pendente por UPDATE, e e assim que
-- esta validacao faz agora — EM_PESAGEM -> FINALIZADA_LOCAL, transicao que o gatilho
-- de UPDATE permite.
--
-- Tudo roda dentro de uma transacao sempre desfeita. Nenhuma linha sobra.
-- ============================================================

\echo '== 068 VALIDACAO =='

\echo '-- a coluna existe, com CHECK (esperado: t e 1)'
SELECT EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_schema='desenvolvimento' AND table_name='hu_caixa' AND column_name='processo_pesagem'
) AS coluna_existe;
SELECT count(*) AS checks_do_processo FROM pg_constraint
 WHERE conrelid='desenvolvimento.hu_caixa'::regclass AND conname='ck_hu_caixa_processo_pesagem';

\echo '-- o indice novo conhece o processo (esperado: t)'
SELECT indexdef ILIKE '%processo_pesagem%' AS indice_conhece_o_processo
  FROM pg_indexes
 WHERE schemaname='desenvolvimento' AND indexname='uq_hu_caixa_terminal_ativo';

\echo '-- privilegio de COLUNA do papel na coluna nova (esperado: INSERT e SELECT)'
SELECT privilege_type
  FROM information_schema.role_column_grants
 WHERE table_schema='desenvolvimento' AND table_name='hu_caixa'
   AND column_name='processo_pesagem' AND grantee='fugapet_dev_app'
 ORDER BY 1;

\echo '-- nenhuma caixa anterior foi reescrita (esperado: 0 com processo preenchido)'
SELECT count(*) AS caixas_com_processo
  FROM desenvolvimento.hu_caixa WHERE processo_pesagem IS NOT NULL;

\echo '-- PROVA DAS REGRAS (tudo desfeito ao final)'
BEGIN;

    -- Auxiliar das provas: nasce e morre DENTRO da transacao desfeita, para o script continuar
    -- nao deixando nada para tras. Copia uma caixa real como molde porque hu_caixa tem muita
    -- coluna obrigatoria, e inventar valores daria erro por motivo errado.
    CREATE FUNCTION desenvolvimento.inserir_caixa_validacao_068(p_processo text)
    RETURNS bigint
    LANGUAGE plpgsql AS $aux$
    DECLARE
        m desenvolvimento.hu_caixa%ROWTYPE;
        v_codigo bigint;
    BEGIN
        SELECT * INTO m FROM desenvolvimento.hu_caixa ORDER BY codigo_hu_caixa LIMIT 1;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'SEM_CAIXA_MODELO: a validacao precisa de ao menos uma caixa existente.';
        END IF;

        INSERT INTO desenvolvimento.hu_caixa
               (numero_ordem_producao, item_ordem_producao, material, lote, centro, deposito,
                material_embalagem, origem_material_embalagem, peso_bruto, peso_liquido, peso_tara,
                unidade_peso, quantidade, unidade_quantidade, origem_pesagem, codigo_usuario, terminal,
                processo_pesagem)
        VALUES (m.numero_ordem_producao, m.item_ordem_producao, m.material, m.lote, m.centro, m.deposito,
                m.material_embalagem, m.origem_material_embalagem, 1, 1, 0, 'KG', 1, 'UN', 'MANUAL',
                m.codigo_usuario, 'VALIDACAO-068', p_processo)
        RETURNING codigo_hu_caixa INTO v_codigo;

        RETURN v_codigo;
    END
    $aux$;

    \echo '-- 1. CAIXA A CAIXA: com uma caixa FINALIZADA_LOCAL, a proxima e RECUSADA (como hoje)'
    SAVEPOINT s1;
    DO $prova1$
    DECLARE
        v_codigo bigint;
    BEGIN
        v_codigo := desenvolvimento.inserir_caixa_validacao_068('CAIXA_A_CAIXA');
        UPDATE desenvolvimento.hu_caixa SET status_hu_caixa='FINALIZADA_LOCAL' WHERE codigo_hu_caixa=v_codigo;

        PERFORM desenvolvimento.inserir_caixa_validacao_068('CAIXA_A_CAIXA');
        RAISE EXCEPTION 'FALHA: o caixa a caixa aceitou uma segunda caixa com a anterior pendente.';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE 'OK: caixa a caixa continua admitindo so uma caixa pendente por terminal.';
    END
    $prova1$;
    ROLLBACK TO SAVEPOINT s1;

    \echo '-- 2. ENVIO UNICO: tres caixas pendentes no mesmo terminal sao ACEITAS (o defeito corrigido)'
    SAVEPOINT s2;
    DO $prova2$
    DECLARE
        v_codigo bigint;
        i integer;
        v_pendentes integer;
    BEGIN
        FOR i IN 1..3 LOOP
            v_codigo := desenvolvimento.inserir_caixa_validacao_068('ENVIO_UNICO');
            UPDATE desenvolvimento.hu_caixa SET status_hu_caixa='FINALIZADA_LOCAL' WHERE codigo_hu_caixa=v_codigo;
        END LOOP;

        SELECT count(*) INTO v_pendentes FROM desenvolvimento.hu_caixa
         WHERE terminal='VALIDACAO-068' AND status_hu_caixa='FINALIZADA_LOCAL';

        IF v_pendentes <> 3 THEN
            RAISE EXCEPTION 'FALHA: esperava 3 pendentes acumuladas, achei %.', v_pendentes;
        END IF;

        RAISE NOTICE 'OK: envio unico acumulou 3 caixas pendentes no mesmo terminal.';
    END
    $prova2$;
    ROLLBACK TO SAVEPOINT s2;

    \echo '-- 3. ENVIO UNICO: duas caixas ABERTAS NA BALANCA continuam RECUSADAS'
    SAVEPOINT s3;
    DO $prova3$
    BEGIN
        PERFORM desenvolvimento.inserir_caixa_validacao_068('ENVIO_UNICO');
        PERFORM desenvolvimento.inserir_caixa_validacao_068('ENVIO_UNICO');
        RAISE EXCEPTION 'FALHA: duas caixas abertas na balanca no mesmo terminal foram aceitas.';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE 'OK: uma caixa por vez na balanca vale tambem no envio unico.';
    END
    $prova3$;
    ROLLBACK TO SAVEPOINT s3;

    \echo '-- 4. o processo sobrevive ao gatilho de INSERT (esperado: ENVIO_UNICO)'
    SAVEPOINT s4;
    DO $prova4$
    DECLARE
        v_codigo bigint;
        v_processo text;
    BEGIN
        v_codigo := desenvolvimento.inserir_caixa_validacao_068('ENVIO_UNICO');
        SELECT processo_pesagem INTO v_processo FROM desenvolvimento.hu_caixa WHERE codigo_hu_caixa=v_codigo;

        IF v_processo IS DISTINCT FROM 'ENVIO_UNICO' THEN
            RAISE EXCEPTION 'FALHA: o gatilho apagou o processo (achei %).', COALESCE(v_processo,'<nulo>');
        END IF;

        RAISE NOTICE 'OK: o processo gravado no INSERT sobrevive ao gatilho.';
    END
    $prova4$;
    ROLLBACK TO SAVEPOINT s4;

    \echo '-- 5. processo fora do dominio e RECUSADO pelo CHECK'
    SAVEPOINT s5;
    DO $prova5$
    BEGIN
        PERFORM desenvolvimento.inserir_caixa_validacao_068('QUALQUER_COISA');
        RAISE EXCEPTION 'FALHA: processo invalido foi aceito.';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK: processo fora do dominio recusado.';
    END
    $prova5$;
    ROLLBACK TO SAVEPOINT s5;

ROLLBACK;

\echo '-- nada sobrou (esperado: 0)'
SELECT count(*) AS resto FROM desenvolvimento.hu_caixa WHERE terminal = 'VALIDACAO-068';
