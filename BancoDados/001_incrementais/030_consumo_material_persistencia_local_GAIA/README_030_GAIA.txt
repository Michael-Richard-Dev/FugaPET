Pacote incremental 030 - Consumo de Materia-Prima: persistencia LOCAL
====================================================================

Objetivo
  Persistir LOCALMENTE (PostgreSQL, schema homologacao) o consumo pesado na
  Tela de Consumo de Materia-Prima. NAO envia SAP, NAO cria movimento 261,
  NAO confirma producao. O lancamento nasce com status PENDENTE_SAP.

Tabelas criadas (schema homologacao)
  - consumo_material_lancamento : cabecalho do consumo por OP
  - consumo_material_item       : componente consumido (reserva/item/lote/deposito)
  - consumo_material_pesagem    : detalhe de cada pesagem (BALANCA/MANUAL)
    - status_pesagem permitido por constraint: REGISTRADA_LOCALMENTE

Ciclo de status (lancamento e item)
  PENDENTE_SAP -> ENVIANDO_SAP -> CONFIRMADO_SAP   (sucesso)
  PENDENTE_SAP -> ENVIANDO_SAP -> FALHA_SAP        (falha apos reserva)
  CANCELADO_LOCAL                                  (cancelamento local)
  ENVIANDO_SAP e o "claim" atomico (reserva) que evita envio 261 duplicado entre
  instancias/sessoes: o envio so prossegue para quem conseguir mover PENDENTE_SAP -> ENVIANDO_SAP.

Arquivos
  - 030_consumo_material_persistencia_local_PROPOSTA_GAIA.sql   (cria tabelas/indices/triggers)
  - 030_consumo_material_persistencia_local_VALIDACAO_GAIA.sql  (confere tabelas/PK/FK/indices/triggers)
  - 030_consumo_material_persistencia_local_ROLLBACK_GAIA.sql   (remove as 3 tabelas)

Ordem de execucao
  PROPOSTA -> VALIDACAO -> (se necessario) ROLLBACK

Observacoes
  - NAO executar automaticamente. Revisao Gaia Dados obrigatoria.
  - Idempotente: CREATE TABLE/INDEX IF NOT EXISTS; triggers recriadas com DROP IF EXISTS.
  - Reaproveita a funcao padrao homologacao.fn_definir_atualizado_em() (baseline).
  - Colunas SAP (documento_material_sap, exercicio, confirmation_group/count, enviado_sap_em)
    sao nullable e ficam para a etapa futura de envio (movimento 261).
