-- FUGAPET / ENTRADA MP / P5 / 056 / VALIDACAO  (somente leitura)
-- PORT DEV: variante da linha DEV, derivada do pacote Q de mesmo numero. Mudancas:
--   database fuga_jales_local_homologacao_q_v1_2 -> fuga_jales_local_desenvolvimento
--   schema   homologacao                         -> desenvolvimento
--   fugapet_q_app   -> fugapet_dev_app  (existe no DEV, rolsuper=false rolcanlogin=true)
--   fugapet_q_owner -> postgres         (no DEV tudo pertence a postgres; nao ha owner dedicado)
-- Delta de estrutura, guardas e post-checks preservados. O nome do gate Q no cabecalho
-- e mantido de proposito: registra a procedencia do pacote.
-- Executar DEPOIS da proposta. Esperado: tudo true, zero linhas na tabela.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT to_regclass('desenvolvimento.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
           AS tabela_criada,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'p')                                        AS chaves_primarias,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'c')                                        AS checks,
       has_table_privilege('fugapet_dev_app',
           'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido', 'SELECT') AS app_le,
       has_table_privilege('fugapet_dev_app',
           'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido', 'INSERT') AS app_insere,
       -- A aplicacao NAO pode apagar nem alterar: liberar exclusividade e ato administrativo.
       NOT has_table_privilege('fugapet_dev_app',
           'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido', 'DELETE') AS app_sem_delete,
       NOT has_table_privilege('fugapet_dev_app',
           'desenvolvimento.entrada_mp_p5_exclusividade_item_pedido', 'UPDATE') AS app_sem_update,
       (SELECT count(*) FROM desenvolvimento.entrada_mp_p5_exclusividade_item_pedido)
                                                                     AS linhas;

ROLLBACK;
