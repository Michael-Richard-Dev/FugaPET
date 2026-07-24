\set ON_ERROR_STOP on
-- ============================================================
-- 039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql
-- Projeto FugaPET_Dev  |  Schema: desenvolvimento
--
-- EXECUCAO CONTROLADA - NAO EXECUTAR AUTOMATICAMENTE.
-- Rodar DEPOIS da PROPOSTA. Validacao ESTRUTURAL + FUNCIONAL. As insercoes de teste rodam dentro de
-- um bloco com rollback intencional: NAO deixam residuo.
-- ============================================================
SET search_path TO desenvolvimento;

-- Timeouts suficientes para os testes transacionais, sem deixar execucao indefinida.
SET lock_timeout = '5s';
SET statement_timeout = '120s';

-- ---------- 1. Estrutural ----------
SELECT 'TABELAS' AS grupo, table_name
  FROM information_schema.tables
 WHERE table_schema='desenvolvimento'
   AND table_name IN ('operacao_producao_configuracao','operacao_producao_apontamento','operacao_producao_evento')
 ORDER BY table_name;

SELECT 'INDICES' AS grupo, indexname
  FROM pg_indexes
 WHERE schemaname='desenvolvimento'
   AND tablename IN ('operacao_producao_configuracao','operacao_producao_apontamento','operacao_producao_evento')
 ORDER BY indexname;

SELECT 'CONSTRAINTS' AS grupo, conname
  FROM pg_constraint
 WHERE conrelid IN ('desenvolvimento.operacao_producao_configuracao'::regclass,
                    'desenvolvimento.operacao_producao_apontamento'::regclass,
                    'desenvolvimento.operacao_producao_evento'::regclass)
 ORDER BY conname;

-- ---------- 2. Funcional (descartado ao final) ----------
DO $$
DECLARE
    v_apont bigint;
    v_apont_termino_duplicado bigint;
    v_apont_cronologico bigint;
    v_ok boolean;
    v_constraint text;
BEGIN
    -- Configuracao valida.
    INSERT INTO desenvolvimento.operacao_producao_configuracao
        (centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, tipo_processo, tela_destino, criado_por)
    VALUES ('3007','ZP01','000000','0050','','CONSUMO_MATERIA_PRIMA','ProcessoConsumoMaterialForm','validador');

    -- 2a. Tipo de processo invalido deve falhar.
    v_ok := false;
    BEGIN
        INSERT INTO desenvolvimento.operacao_producao_configuracao
            (centro, operacao_sap, tipo_processo, criado_por)
        VALUES ('3007','0060','XPTO','validador');
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: tipo_processo invalido foi aceito.'; END IF;

    -- 2b. Segunda configuracao ATIVA para a mesma combinacao (incl. centro_trabalho) deve falhar.
    v_ok := false;
    BEGIN
        INSERT INTO desenvolvimento.operacao_producao_configuracao
            (centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho, tipo_processo, criado_por)
        VALUES ('3007','ZP01','000000','0050','','','CONSUMO_QUIMICOS','validador');
    EXCEPTION WHEN unique_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: configuracao ATIVA duplicada foi aceita.'; END IF;

    -- 2b2. CENTRO DE TRABALHO diferente e uma configuracao DIFERENTE: deve ser aceita.
    INSERT INTO desenvolvimento.operacao_producao_configuracao
        (centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho, tipo_processo, criado_por)
    VALUES ('3007','ZP01','000000','0050','','CT99','CONSUMO_QUIMICOS','validador');

    -- Apontamento de inicio.
    INSERT INTO desenvolvimento.operacao_producao_apontamento
        (numero_ordem, sequencia, operacao, suboperacao, usuario_inicio, estacao_inicio,
         codigo_barras_inicio, idempotency_key, status)
    VALUES ('OP_VALIDACAO_039','000000','0050','','validador','EST01',
            '000001001710005001','OP_OPERACAO_EVENTO_V1|000001001710005001','EM_ANDAMENTO')
    RETURNING codigo_apontamento INTO v_apont;

    -- 2c. Status invalido deve falhar.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='XPTO' WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: status invalido foi aceito.'; END IF;

    -- 2d. DOIS APONTAMENTOS ATIVOS da mesma operacao deve falhar.
    v_ok := false;
    BEGIN
        INSERT INTO desenvolvimento.operacao_producao_apontamento
            (numero_ordem, sequencia, operacao, suboperacao, usuario_inicio, estacao_inicio,
             codigo_barras_inicio, idempotency_key, status)
        VALUES ('OP_VALIDACAO_039','000000','0050','','outro','EST02',
                '000001001710005001','OP_OPERACAO_EVENTO_V1|OUTRA_CHAVE','EM_ANDAMENTO');
    EXCEPTION WHEN unique_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: dois apontamentos ATIVOS da mesma operacao foram aceitos.'; END IF;

    -- 2e. CODIGO DE INICIO usado duas vezes deve falhar (mesma idempotency_key, outra operacao).
    v_ok := false;
    BEGIN
        INSERT INTO desenvolvimento.operacao_producao_apontamento
            (numero_ordem, sequencia, operacao, suboperacao, usuario_inicio, estacao_inicio,
             codigo_barras_inicio, idempotency_key, status)
        VALUES ('OP_VALIDACAO_039','000000','0060','','validador','EST01',
                '000001001710005001','OP_OPERACAO_EVENTO_V1|000001001710005001','EM_ANDAMENTO');
    EXCEPTION WHEN unique_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: codigo de inicio reutilizado foi aceito.'; END IF;

    -- 2f. CONCLUIDA sem dados de termino deve falhar.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='CONCLUIDA' WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: CONCLUIDA sem dados de termino foi aceita.'; END IF;

    -- 2f2. AGUARDANDO_FINALIZACAO sem resultado operacional deve falhar (vinculo obrigatorio).
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='AGUARDANDO_FINALIZACAO' WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: AGUARDANDO_FINALIZACAO sem resultado operacional foi aceita.'; END IF;

    -- 2f3. Transicao operacional COMPLETA (com vinculo do lancamento) e aceita.
    UPDATE desenvolvimento.operacao_producao_apontamento
       SET status='AGUARDANDO_FINALIZACAO',
           resultado_operacional='ConfirmadoSap',
           codigo_registro_processo=12345,
           concluido_operacional_em=now(),
           mensagem_resultado_operacional='Consumo enviado.'
     WHERE codigo_apontamento=v_apont;

    -- 2f4. Estado NAO persistido (calculado de tela) deve ser rejeitado pelo CHECK.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='PENDENTE' WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: estado calculado (PENDENTE) foi aceito como estado persistido.'; END IF;

    -- 2f5. Resultado operacional INVALIDO (ex.: ErroSap) nao pode sustentar AGUARDANDO_FINALIZACAO.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET resultado_operacional='ErroSap'
         WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: resultado operacional ErroSap foi aceito em AGUARDANDO_FINALIZACAO.'; END IF;

    -- 2f6. DivergenciaSap tambem nao pode sustentar AGUARDANDO_FINALIZACAO.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET resultado_operacional='DivergenciaSap'
         WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: resultado operacional DivergenciaSap foi aceito em AGUARDANDO_FINALIZACAO.'; END IF;

    -- 2f7. ConcluidoLocalmente com concluido_operacional_em preenchido tambem e valido.
    UPDATE desenvolvimento.operacao_producao_apontamento
       SET resultado_operacional='ConcluidoLocalmente',
           concluido_operacional_em=now()
     WHERE codigo_apontamento=v_apont;

    -- Retorna ao resultado ConfirmadoSap para o cenario de termino.
    UPDATE desenvolvimento.operacao_producao_apontamento
       SET resultado_operacional='ConfirmadoSap',
           concluido_operacional_em=now()
     WHERE codigo_apontamento=v_apont;

    -- 2g. Termino completo e aceito (a partir de AGUARDANDO_FINALIZACAO, com resultado valido).
    UPDATE desenvolvimento.operacao_producao_apontamento
       SET status='CONCLUIDA', usuario_termino='validador', estacao_termino='EST01',
           terminado_em=now(), codigo_barras_termino='000001001710005002',
           idempotency_key_termino='OP_OPERACAO_EVENTO_V1|000001001710005002'
     WHERE codigo_apontamento=v_apont;

    -- 2g2. CONCLUIDA tambem exige resultado operacional valido: limpa-lo deve falhar.
    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET resultado_operacional=NULL
         WHERE codigo_apontamento=v_apont;
    EXCEPTION WHEN check_violation THEN v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: CONCLUIDA sem resultado operacional valido foi aceita.'; END IF;

    -- 2h. CODIGO DE TERMINO usado duas vezes deve falhar por unique_violation.
    -- O segundo apontamento e preparado em estado valido para garantir que o teste alcance o indice unico,
    -- e nao uma check constraint anterior.
    INSERT INTO desenvolvimento.operacao_producao_apontamento
        (numero_ordem, sequencia, operacao, suboperacao, usuario_inicio, estacao_inicio,
         codigo_barras_inicio, idempotency_key, status)
    VALUES ('OP_VALIDACAO_039','000000','0060','','validador','EST01',
            '000001001710006001','OP_OPERACAO_EVENTO_V1|000001001710006001','EM_ANDAMENTO')
    RETURNING codigo_apontamento INTO v_apont_termino_duplicado;

    UPDATE desenvolvimento.operacao_producao_apontamento
       SET status='AGUARDANDO_FINALIZACAO',
           resultado_operacional='ConfirmadoSap',
           codigo_registro_processo=12346,
           concluido_operacional_em=now()
     WHERE codigo_apontamento=v_apont_termino_duplicado;

    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='CONCLUIDA', usuario_termino='validador', estacao_termino='EST01',
               terminado_em=now(), codigo_barras_termino='000001001710005002',
               idempotency_key_termino='OP_OPERACAO_EVENTO_V1|000001001710005002'
         WHERE codigo_apontamento=v_apont_termino_duplicado;
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;
            IF v_constraint <> 'uq_apontamento_idempotency_termino' THEN
                RAISE EXCEPTION 'FALHA: termino duplicado violou constraint inesperada: %', v_constraint;
            END IF;
            v_ok := true;
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;
            RAISE EXCEPTION 'FALHA: teste de termino duplicado caiu em check_violation (%) antes do indice unico.', v_constraint;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: codigo de termino reutilizado foi aceito.'; END IF;

    -- 2i. Termino anterior ao inicio deve falhar especificamente em ck_apontamento_ordem_cronologica.
    -- Cenario independente e valido nas demais regras para isolar o check cronologico.
    INSERT INTO desenvolvimento.operacao_producao_apontamento
        (numero_ordem, sequencia, operacao, suboperacao, usuario_inicio, estacao_inicio,
         codigo_barras_inicio, idempotency_key, status)
    VALUES ('OP_VALIDACAO_039','000000','0070','','validador','EST01',
            '000001001710007001','OP_OPERACAO_EVENTO_V1|000001001710007001','EM_ANDAMENTO')
    RETURNING codigo_apontamento INTO v_apont_cronologico;

    UPDATE desenvolvimento.operacao_producao_apontamento
       SET status='AGUARDANDO_FINALIZACAO',
           resultado_operacional='ConfirmadoSap',
           codigo_registro_processo=12347,
           concluido_operacional_em=now()
     WHERE codigo_apontamento=v_apont_cronologico;

    v_ok := false;
    BEGIN
        UPDATE desenvolvimento.operacao_producao_apontamento
           SET status='CONCLUIDA', usuario_termino='v', estacao_termino='E',
               terminado_em = iniciado_em - interval '1 hour',
               codigo_barras_termino='000001001710007002',
               idempotency_key_termino='OP_OPERACAO_EVENTO_V1|000001001710007002'
         WHERE codigo_apontamento=v_apont_cronologico;
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_constraint = CONSTRAINT_NAME;
            IF v_constraint <> 'ck_apontamento_ordem_cronologica' THEN
                RAISE EXCEPTION 'FALHA: termino anterior ao inicio violou check inesperado: %', v_constraint;
            END IF;
            v_ok := true;
    END;
    IF NOT v_ok THEN RAISE EXCEPTION 'FALHA: termino anterior ao inicio foi aceito.'; END IF;

    RAISE NOTICE 'VALIDACAO FUNCIONAL OK - checks/indices comportaram-se como esperado. Descartando dados de teste.';
    RAISE EXCEPTION 'ROLLBACK_INTENCIONAL_039';
EXCEPTION WHEN OTHERS THEN
    IF SQLERRM LIKE '%ROLLBACK_INTENCIONAL_039%' THEN
        RAISE NOTICE 'Cenario de teste descartado com sucesso (rollback intencional).';
    ELSE
        RAISE;
    END IF;
END $$;

-- ---------- 3. Permissoes do app: SELECT/INSERT/UPDATE, SEM DELETE, USAGE nas sequences ----------
DO $$
DECLARE
    v_seq_config text := pg_get_serial_sequence('desenvolvimento.operacao_producao_configuracao', 'codigo_configuracao');
    v_seq_apont  text := pg_get_serial_sequence('desenvolvimento.operacao_producao_apontamento', 'codigo_apontamento');
    v_seq_evento text := pg_get_serial_sequence('desenvolvimento.operacao_producao_evento', 'codigo_evento_apontamento');
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='fugapet_dev_app') THEN
        RAISE EXCEPTION 'VALIDACAO: papel fugapet_dev_app inexistente. A aplicacao DEV nao teria acesso aos objetos do 039.';
    END IF;

    -- SELECT obrigatorio nas tres.
    IF NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_configuracao','SELECT')
       OR NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_apontamento','SELECT')
       OR NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_evento','SELECT') THEN
        RAISE EXCEPTION 'VALIDACAO: fugapet_dev_app sem SELECT em alguma tabela do 039.';
    END IF;

    -- INSERT/UPDATE no apontamento; INSERT no evento.
    IF NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_apontamento','INSERT')
       OR NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_apontamento','UPDATE') THEN
        RAISE EXCEPTION 'VALIDACAO: fugapet_dev_app sem INSERT/UPDATE em operacao_producao_apontamento.';
    END IF;
    IF NOT has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_evento','INSERT') THEN
        RAISE EXCEPTION 'VALIDACAO: fugapet_dev_app sem INSERT em operacao_producao_evento.';
    END IF;

    -- AUSENCIA de DELETE nas tres (historico preservado).
    IF has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_configuracao','DELETE')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_apontamento','DELETE')
       OR has_table_privilege('fugapet_dev_app','desenvolvimento.operacao_producao_evento','DELETE') THEN
        RAISE EXCEPTION 'VALIDACAO: fugapet_dev_app NAO pode ter DELETE nas tabelas do 039.';
    END IF;

    -- USAGE nas sequences identity.
    IF v_seq_apont IS NULL OR v_seq_evento IS NULL OR v_seq_config IS NULL THEN
        RAISE EXCEPTION 'VALIDACAO: sequence identity nao encontrada (config=%, apont=%, evento=%).',
            v_seq_config, v_seq_apont, v_seq_evento;
    END IF;
    IF NOT has_sequence_privilege('fugapet_dev_app', v_seq_apont, 'USAGE')
       OR NOT has_sequence_privilege('fugapet_dev_app', v_seq_evento, 'USAGE')
       OR NOT has_sequence_privilege('fugapet_dev_app', v_seq_config, 'USAGE') THEN
        RAISE EXCEPTION 'VALIDACAO: fugapet_dev_app sem USAGE em alguma sequence do 039 (INSERT identity falharia).';
    END IF;

    RAISE NOTICE 'VALIDACAO OK: privilegios corretos (SELECT/INSERT/UPDATE, sem DELETE, USAGE nas sequences).';
END $$;

-- ---------- 4. Permissoes da rotina e vinculo com o perfil Administrador ----------
DO $$
DECLARE
    v_faltando text;
    v_sem_vinculo int;
    v_total int;
BEGIN
    -- MVP: as 3 acoes com efeito funcional devem existir, ativas e com descricao.
    SELECT string_agg(a.acao, ', ' ORDER BY a.acao) INTO v_faltando
      FROM (VALUES ('VISUALIZAR'),('INICIAR'),('FINALIZAR')) AS a(acao)
     WHERE NOT EXISTS (
        SELECT 1 FROM desenvolvimento.permissao p
         WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS'
           AND p.acao_permissao = a.acao
           AND p.situacao_permissao = true
           AND coalesce(trim(p.descricao_permissao), '') <> '');

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO: permissoes CONTROLE_APONTAMENTOS ausentes/inativas/sem descricao: %', v_faltando;
    END IF;

    -- Nenhuma permissao SEM efeito funcional pode ter sido criada nesta versao.
    SELECT string_agg(p.acao_permissao, ', ' ORDER BY p.acao_permissao) INTO v_faltando
      FROM desenvolvimento.permissao p
     WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
       AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS'
       AND p.acao_permissao NOT IN ('VISUALIZAR','INICIAR','FINALIZAR');

    IF v_faltando IS NOT NULL THEN
        RAISE EXCEPTION 'VALIDACAO: acoes sem efeito funcional criadas nesta versao (remover): %', v_faltando;
    END IF;

    -- Todas devem estar vinculadas ao perfil Administrador.
    IF EXISTS (SELECT 1 FROM desenvolvimento.perfil_acesso WHERE nome_perfil_acesso = 'Administrador') THEN
        SELECT count(*) INTO v_sem_vinculo
          FROM desenvolvimento.permissao p
         WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS'
           AND NOT EXISTS (
                SELECT 1
                  FROM desenvolvimento.perfil_permissao pp
                  JOIN desenvolvimento.perfil_acesso pa ON pa.codigo_perfil_acesso = pp.codigo_perfil_acesso
                 WHERE pp.codigo_permissao = p.codigo_permissao
                   AND pa.nome_perfil_acesso = 'Administrador');

        IF v_sem_vinculo > 0 THEN
            RAISE EXCEPTION 'VALIDACAO: % permissao(oes) CONTROLE_APONTAMENTOS sem vinculo com o perfil Administrador.', v_sem_vinculo;
        END IF;

        -- Quantidade CALCULADA (nunca numero fixo divergente da lista validada).
        SELECT count(*) INTO v_total
          FROM desenvolvimento.permissao p
         WHERE p.modulo_permissao = 'PROCESSO_PRODUCAO'
           AND p.rotina_permissao = 'CONTROLE_APONTAMENTOS'
           AND p.situacao_permissao = true;

        RAISE NOTICE 'VALIDACAO OK: % permissoes ativas e vinculadas ao perfil Administrador.', v_total;
    ELSE
        RAISE NOTICE 'VALIDACAO: perfil Administrador inexistente - checagem de vinculo ignorada.';
    END IF;
END $$;

-- ---------- 5. Idempotencia: reaplicar a PROPOSTA nao duplica permissoes ----------
SELECT 'PERMISSOES_DUPLICADAS' AS grupo, acao_permissao, count(*) AS ocorrencias
  FROM desenvolvimento.permissao
 WHERE modulo_permissao = 'PROCESSO_PRODUCAO' AND rotina_permissao = 'CONTROLE_APONTAMENTOS'
 GROUP BY acao_permissao
HAVING count(*) > 1;
