\set ON_ERROR_STOP on
-- ============================================================
-- 065_vinculo_produto_etiqueta_DEV_VALIDACAO_GAIA.sql
-- FugaPET | Ambiente DEV | Schema desenvolvimento
-- Incremento 065 - VALIDACAO (SOMENTE LEITURA)
--
-- Confere o que foi criado e PROVA as tres regras de integridade, tentando viola-las
-- dentro de uma transacao que e sempre desfeita. Nenhuma linha sobra.
-- ============================================================

\echo '== 065 VALIDACAO =='

\echo '-- tabela existe (esperado: t)'
SELECT to_regclass('desenvolvimento.produto_etiqueta_vinculo') IS NOT NULL AS tabela_existe;

\echo '-- constraints (esperado: pk, uq, fk e 4 checks)'
SELECT conname, contype
  FROM pg_constraint
 WHERE conrelid = 'desenvolvimento.produto_etiqueta_vinculo'::regclass
 ORDER BY contype, conname;

\echo '-- indices (esperado: pk + 2)'
SELECT indexname FROM pg_indexes
 WHERE schemaname='desenvolvimento' AND tablename='produto_etiqueta_vinculo'
 ORDER BY indexname;

\echo '-- privilegios do papel de aplicacao (esperado: SELECT/INSERT/UPDATE/DELETE)'
SELECT privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema='desenvolvimento'
   AND table_name='produto_etiqueta_vinculo'
   AND grantee='fugapet_dev_app'
 ORDER BY privilege_type;

\echo '-- PROVA DAS REGRAS (tudo desfeito ao final)'
BEGIN;

    -- 1. processo invalido deve falhar
    SAVEPOINT s1;
    DO $prova1$
    BEGIN
        INSERT INTO desenvolvimento.produto_etiqueta_vinculo (centro, produto, processo_pesagem, sem_etiqueta)
        VALUES ('3007', 'VALIDACAO-065-A', 'PROCESSO_INEXISTENTE', true);
        RAISE EXCEPTION 'FALHA: processo invalido foi aceito.';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK: processo invalido recusado.';
    END
    $prova1$;
    ROLLBACK TO SAVEPOINT s1;

    -- 2. "sem etiqueta" com etiqueta preenchida deve falhar
    SAVEPOINT s2;
    DO $prova2$
    DECLARE alguma bigint;
    BEGIN
        SELECT codigo_etiqueta INTO alguma FROM desenvolvimento.etiqueta LIMIT 1;
        IF alguma IS NULL THEN
            RAISE NOTICE 'PULADO: nao ha etiqueta cadastrada para testar a combinacao.';
            RETURN;
        END IF;
        INSERT INTO desenvolvimento.produto_etiqueta_vinculo (centro, produto, processo_pesagem, sem_etiqueta, codigo_etiqueta)
        VALUES ('3007', 'VALIDACAO-065-B', 'CAIXA_A_CAIXA', true, alguma);
        RAISE EXCEPTION 'FALHA: sem_etiqueta com etiqueta preenchida foi aceito.';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK: combinacao incoerente recusada.';
    END
    $prova2$;
    ROLLBACK TO SAVEPOINT s2;

    -- 3. dois vinculos para o mesmo produto devem falhar (o "um pra um")
    SAVEPOINT s3;
    DO $prova3$
    BEGIN
        INSERT INTO desenvolvimento.produto_etiqueta_vinculo (centro, produto, processo_pesagem, sem_etiqueta)
        VALUES ('3007', 'VALIDACAO-065-C', 'CAIXA_A_CAIXA', true);
        INSERT INTO desenvolvimento.produto_etiqueta_vinculo (centro, produto, processo_pesagem, sem_etiqueta)
        VALUES ('3007', 'VALIDACAO-065-C', 'ENVIO_UNICO', true);
        RAISE EXCEPTION 'FALHA: o mesmo produto aceitou dois vinculos.';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE 'OK: segundo vinculo do mesmo produto recusado.';
    END
    $prova3$;
    ROLLBACK TO SAVEPOINT s3;

ROLLBACK;

\echo '-- nada sobrou (esperado: 0)'
SELECT count(*) AS linhas_de_validacao
  FROM desenvolvimento.produto_etiqueta_vinculo
 WHERE produto LIKE 'VALIDACAO-065-%';
