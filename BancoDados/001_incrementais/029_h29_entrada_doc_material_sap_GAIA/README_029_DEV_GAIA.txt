============================================================
README - Incremental 029 (DEV) - Gaia Dados
Projeto: FugaPET_Dev
Ambiente: DESENVOLVIMENTO (DEV)
Banco: PostgreSQL - Schema desenvolvimento
Objeto: Entrada de Produto - rastreabilidade do Documento de Material SAP (movimento 101)
Responsavel tecnico: Equipe FugaPET (proposta para Gaia Dados)
Data: 2026-06-25
============================================================

AVISO
  NAO executar automaticamente. Pacote PROPOSTO para revisao da Gaia Dados.
  Nenhum SQL deste pacote foi executado. Baseline NAO foi alterado.

1. OBJETIVO
  Persistir a rastreabilidade do documento de material SAP criado via movimento 101
  (API_MATERIAL_DOCUMENT_SRV) pela Entrada de Produto, sem alterar a baseline V1.1.
  Hoje o numero/exercicio do documento so vao para log_integracao_sap (operacao
  CRIAR_DOCUMENTO_MATERIAL_101). As colunas abaixo permitem consultar a amarracao
  do lancamento confirmado ao documento SAP.

2. ESCOPO (colunas)
  entrada_produto_lancamento:
    - documento_material_sap            varchar(20)  -> numero do documento de material
    - exercicio_documento_material_sap  varchar(4)   -> ano fiscal do documento
    - enviado_sap_em                    timestamptz  -> instante (UTC) da confirmacao
  entrada_produto_item (opcional):
    - documento_material_item           varchar(20)  -> item do documento de material
  Constraints:
    - ck_entrada_lancamento_documento_material_par  -> documento e exercicio juntos ou ambos nulos
    - ck_entrada_lancamento_exercicio_material_formato -> exercicio com 4 digitos numericos
  Observacao: nenhuma coluna e NOT NULL. Registros antigos e lancamentos ainda nao
  enviados permanecem NULL.

3. ARQUIVOS (ordem de execucao futura)
    1) 029_entrada_doc_material_sap_DEV_PREFLIGHT_GAIA.sql   (somente leitura; valida pre-condicoes)
    2) 029_entrada_doc_material_sap_DEV_PROPOSTA_GAIA.sql    (cria colunas/constraints/comentarios)
    3) 029_entrada_doc_material_sap_DEV_VALIDACAO_GAIA.sql   (somente leitura; comprova o resultado)
    4) 029_entrada_doc_material_sap_DEV_ROLLBACK_GAIA.sql    (apenas se necessario)
    -  README_029_DEV_GAIA.txt                               (este arquivo)

4. CRITERIOS DE ACEITE
    - PREFLIGHT conclui sem erro (schema, tabelas, colunas base, constraint de status,
      permissao para ALTER TABLE).
    - PROPOSTA cria as colunas, constraints e comentarios; e idempotente
      (ADD COLUMN IF NOT EXISTS / ADD CONSTRAINT condicional).
    - VALIDACAO confirma colunas, tipos, comentarios e constraints; comprova que os
      registros existentes continuam integros e que nada fora do escopo foi alterado.
    - ROLLBACK remove somente o que o 029 criou.

5. RISCOS E MITIGACAO
    - Bloqueio por incompatibilidade de status: o PREFLIGHT exige ENVIADO_SAP e
      CONFIRMADO_SAP na constraint ck_entrada_lancamento_status (ja presentes no baseline).
    - Perda de dados no rollback: o ROLLBACK BLOQUEIA se houver rastreabilidade preenchida.
    - Reaplicacao: idempotente; PREFLIGHT apenas avisa (NOTICE) se colunas ja existirem.

6. ROLLBACK COM DADOS PREENCHIDOS
    O ROLLBACK e bloqueado por seguranca quando ha documento material gravado.
    Para forcar (cenario excepcional):
      a) Gerar backup/snapshot do schema.
      b) Exportar as colunas do 029 (documento_material_sap, exercicio_documento_material_sap,
         enviado_sap_em, documento_material_item) para evidencia.
      c) Remover MANUALMENTE o bloco "Preflight proprio do rollback" do script (decisao
         tecnica documentada e aprovada pela Gaia Dados).
      d) Executar o restante do ROLLBACK.

7. EVIDENCIAS ESPERADAS (geradas pela VALIDACAO)
    - information_schema.columns: colunas criadas com tipo/tamanho corretos e nullable.
    - pg_constraint (pg_get_constraintdef): definicoes das 2 constraints do 029.
    - col_description: comentarios aplicados nas colunas.
    - Contagem 0 de lancamentos com rastreabilidade logo apos a PROPOSTA (sem impacto
      em registros existentes).

8. INTEGRACAO COM A APLICACAO (referencia documental, nenhum codigo alterado aqui)
    Apos HTTP 2xx do POST A_MaterialDocumentHeader, a aplicacao deve gravar
    documento_material_sap / exercicio_documento_material_sap / enviado_sap_em no
    lancamento, junto da transicao para CONFIRMADO_SAP. Enquanto este 029 nao for
    aplicado, o numero/exercicio continuam rastreaveis apenas no log_integracao_sap.

9. PENDENCIAS / DUVIDAS PARA GAIA DADOS
    - Confirmar varchar(20) para documento_material_sap (Material Document SAP tem ate
      10 digitos; 20 deixa folga). Alternativa: varchar(10).
    - Confirmar manter exercicio como varchar(4) com check de formato vs integer com check.
    - Confirmar se o campo opcional documento_material_item deve entrar nesta fase ou
      ficar para um 029.1 posterior.
============================================================
