PACOTE BANCO DESENVOLVIMENTO V1.1 - FugaPET
=================================================

AMBIENTE
Desenvolvimento

BANCO RECOMENDADO
fuga_jales_local_desenvolvimento

SCHEMA
desenvolvimento

OBJETIVO
Criar um banco novo de desenvolvimento já consolidado com a baseline v1.1,
incluindo os ciclos 018 a 022.

PESO
Todos os pesos operacionais e taras permanecem padronizados em quilogramas:
numeric(14,3).

NOVIDADES DESTA VERSAO
- INTEGRACAO_SAP_ATIVA = true no desenvolvimento.
- log_integracao_sap sanitizado.
- permissoes especificas para ENTRADA_PRODUTO.
- centro, deposito e grupo_material no item do pedido SAP.
- entrada_produto_lancamento.
- entrada_produto_item.
- entrada_produto_pesagem.
- status_lancamento, status_item e status_pesagem.
- payload_balanca e leitura_original para auditoria da balanca.
- views de resumo da Entrada de Produto.

ORDEM DE EXECUCAO EM BANCO NOVO
1. Execute 000_execucao_completa_desenvolvimento_v1_1.sql.
2. Gere um hash BCrypt work factor 12 para o admin.
3. Edite 001_bootstrap_admin_desenvolvimento_v1_1_pgadmin.sql.
4. Execute 001_bootstrap_admin_desenvolvimento_v1_1_pgadmin.sql.
5. Execute 002_validar_banco_desenvolvimento_v1_1.sql.

ATENCAO
Este pacote é exclusivo para schema desenvolvimento.
Não execute este script em homologacao ou producao.

IMPORTANTE
Este é um script geral para banco novo.
Em banco já existente, não rode o 000 novamente; nesse caso, gere um
incremental específico.
