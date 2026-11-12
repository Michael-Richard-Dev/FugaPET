-- FUGAPET / ENTRADA MP / P5 / 056 / VALIDACAO  (somente leitura)
-- Executar DEPOIS da proposta. Esperado: tudo true, zero linhas na tabela.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT to_regclass('homologacao.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
           AS tabela_criada,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'homologacao.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'p')                                        AS chaves_primarias,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'homologacao.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'c')                                        AS checks,
       has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'SELECT') AS app_le,
       has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'INSERT') AS app_insere,
       -- A aplicacao NAO pode apagar nem alterar: liberar exclusividade e ato administrativo.
       NOT has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'DELETE') AS app_sem_delete,
       NOT has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'UPDATE') AS app_sem_update,
       (SELECT count(*) FROM homologacao.entrada_mp_p5_exclusividade_item_pedido)
                                                                     AS linhas;

ROLLBACK;
