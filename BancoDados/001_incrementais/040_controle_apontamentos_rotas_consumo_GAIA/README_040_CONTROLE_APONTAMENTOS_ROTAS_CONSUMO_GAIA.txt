040_controle_apontamentos_rotas_consumo_GAIA
Projeto: FugaPET_Dev
Schema: desenvolvimento

Objetivo
- Inserir de forma transacional e idempotente as rotas globais iniciais do Controle de Apontamentos:
  - operacao 0050 -> CONSUMO_MATERIA_PRIMA -> ProcessoConsumoMaterialForm -> exige_operacao_anterior = false
  - operacao 0060 -> CONSUMO_QUIMICOS       -> ProcessoConsumoMaterialForm -> exige_operacao_anterior = true

Regra oficial
- A operacao SAP e o criterio funcional de roteamento.
- 0050 direciona para Consumo de Materia-Prima e nao exige operacao anterior local.
- 0060 direciona para Consumo de Quimicos e exige conclusao local da operacao anterior 0050.
- 0010 a 0040 nao possuem apontamento local nesta etapa, portanto nao bloqueiam a 0050.
- Quando a tela e aberta pelo Controle de Apontamentos, a operacao tambem deve orientar o filtro funcional dos componentes apresentados na tela.

Chave global desta etapa
- centro = ''
- tipo_ordem = ''
- sequencia_sap = ''
- suboperacao_sap = ''
- centro_trabalho = ''

Normalizacao de operacao
- 50 e 0050 sao equivalentes.
- 60 e 0060 sao equivalentes.
- O preflight e a validacao verificam duplicidades normalizadas para evitar rotas globais concorrentes.

Premissas
- O pacote 039 deve estar aplicado e validado.
- Este pacote nao altera estrutura, nao altera apontamentos, nao altera eventos e nao altera regras SAP.
- Configuracoes especificas corretas para 0050/0060 podem coexistir.
- Configuracoes especificas conflitantes sao bloqueantes porque podem ganhar por maior especificidade no repository.
- Nao executar automaticamente.

Arquivos
1. 040_controle_apontamentos_rotas_consumo_DEV_PREFLIGHT_GAIA.sql
   - Somente leitura e rollback transacional.
   - Lista rotas globais e configuracoes especificas ativas para 0050/0060.
   - Bloqueia duplicidade global normalizada e conflito de tipo, destino ou exige_operacao_anterior.

2. 040_controle_apontamentos_rotas_consumo_DEV_PROPOSTA_GAIA.sql
   - Transacional e idempotente.
   - Insere somente quando a rota global correta ainda nao existe.
   - Nao atualiza configuracao preexistente.
   - Nao executa DELETE.
   - Nao altera apontamentos.

3. 040_controle_apontamentos_rotas_consumo_DEV_VALIDACAO_GAIA.sql
   - Valida exatamente uma rota global ativa para 0050 com exige_operacao_anterior=false.
   - Valida exatamente uma rota global ativa para 0060 com exige_operacao_anterior=true.
   - Nao conta configuracoes especificas como rota global.
   - Valida ausencia de duplicidade normalizada e conflitos globais/especificos.

4. 040_controle_apontamentos_rotas_consumo_DEV_ROLLBACK_GAIA.sql
   - Nao faz parte do fluxo normal.
   - Remove somente registros globais criados pelo pacote 040.
   - O DELETE exige criado_por do pacote, chave global vazia, operacao normalizada, tipo_processo esperado, tela_destino esperada e exige_operacao_anterior esperado.
   - Nao remove configuracoes manuais ou especificas.

Ordem sugerida somente apos autorizacao formal
1. psql -v ON_ERROR_STOP=1 -f 040_controle_apontamentos_rotas_consumo_DEV_PREFLIGHT_GAIA.sql
2. psql -v ON_ERROR_STOP=1 -f 040_controle_apontamentos_rotas_consumo_DEV_PROPOSTA_GAIA.sql
3. psql -v ON_ERROR_STOP=1 -f 040_controle_apontamentos_rotas_consumo_DEV_VALIDACAO_GAIA.sql

Restricoes desta revisao
- Nenhum SQL foi executado.
- O banco DEV nao foi alterado por esta revisao.
- O incremental 039 nao foi alterado.
- Codigo C# nao foi alterado.
- HML e PRD nao foram acessados.
- Commit e push nao foram realizados.
