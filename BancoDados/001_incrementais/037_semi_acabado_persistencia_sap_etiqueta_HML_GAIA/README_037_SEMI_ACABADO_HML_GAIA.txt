============================================================
PACOTE 037 — SEMIACABADO: PERSISTENCIA LOCAL + SAP 101 + ETIQUETA POR PESAGEM
Projeto: FugaPET_Dev
Schema alvo: homologacao
Responsavel pelo projeto: Michael Richard
Autor tecnico lógico: Gaia Dados
============================================================

STATUS:
- Pacote gerado para revisao.
- Nenhum script foi executado.
- Nenhum dado foi alterado.
- Nenhum rollback foi executado.
- Nenhum commit foi realizado.
- Nenhum codigo C# foi alterado.
- HML nao foi alterado.
- PRD nao faz parte deste pacote.

============================================================
1. ESCOPO
============================================================

Este pacote cria a persistencia local do processo de Produto Semiacabado no DEV:

1. homologacao.semi_acabado_lancamento
   Cabeçalho por OP/item produzido.
   Guarda dados da OP, status local/SAP, payload de preview e documento SAP 101.

2. homologacao.semi_acabado_pesagem
   Uma linha por pesagem individual.
   Guarda CodigoEtiqueta persistido, pesos, status, sequencia, origem e saldo_apos_pesagem_kg.

A coluna saldo_apos_pesagem_kg foi incluída para permitir reimpressao idêntica da etiqueta,
mesmo se o saldo da OP mudar depois. A aplicacao deve gravar o saldo restante apos cada
pesagem valida.

============================================================
2. ARQUIVOS
============================================================

037_semi_acabado_HML_PREFLIGHT_GAIA.sql
037_semi_acabado_HML_PROPOSTA_GAIA.sql
037_semi_acabado_HML_VALIDACAO_GAIA.sql
037_semi_acabado_HML_ROLLBACK_GAIA.sql
README_037_SEMI_ACABADO_HML_GAIA.txt

============================================================
3. ORDEM DE EXECUCAO FUTURA
============================================================

Fluxo normal em DEV:

1. Preflight:
   psql -v ON_ERROR_STOP=1 -d <banco_hml> -f 037_semi_acabado_HML_PREFLIGHT_GAIA.sql

2. Aplicacao:
   psql -v ON_ERROR_STOP=1 -d <banco_hml> -f 037_semi_acabado_HML_PROPOSTA_GAIA.sql

3. Validacao:
   psql -v ON_ERROR_STOP=1 -d <banco_hml> -f 037_semi_acabado_HML_VALIDACAO_GAIA.sql

Rollback nao faz parte do fluxo normal.

============================================================
4. PRINCIPAIS REGRAS DE BANCO
============================================================

Lancamento:
- status_lancamento permitido:
  FINALIZADO_LOCAL, ENVIANDO_SAP, CONFIRMADO_SAP, ERRO_SAP, DIVERGENCIA_SAP.
- CONFIRMADO_SAP exige material_document e material_document_year.
- pesos e quantidades nao podem ser negativos.
- no maximo um lancamento aberto por numero_ordem + item_ordem para:
  FINALIZADO_LOCAL, ENVIANDO_SAP, ERRO_SAP, DIVERGENCIA_SAP.
- CONFIRMADO_SAP fica fora do indice de abertura para permitir historico.

Pesagem:
- status_pesagem permitido: VALIDA, CANCELADA.
- sequencia positiva.
- codigo_etiqueta unico globalmente.
- sequencia unica por lancamento.
- origem permitida: MANUAL, BALANCA.
- pesagem VALIDA exige peso_liquido_kg > 0.
- saldo_apos_pesagem_kg nao pode ser negativo.
- CANCELADA exige cancelado_em; VALIDA nao pode ter cancelado_em.

Permissoes:
- Papel de aplicacao HML nao foi identificado em incrementais existentes; nao foi inventado no pacote. Gaia deve validar/conceder grants conforme role real do ambiente antes da execucao.
- DELETE nao e concedido.
- sequences recebem USAGE e SELECT.

============================================================
5. PONTOS AJUSTADOS EM RELACAO AO PACOTE RECEBIDO
============================================================

- Adicionado \set ON_ERROR_STOP on nos 4 scripts.
- Adicionados lock_timeout e statement_timeout em aplicacao e rollback.
- Incluida coluna saldo_apos_pesagem_kg em semi_acabado_pesagem.
- Preflight reforcado para identificar pacote nao aplicado, parcial ou compativel.
- Comentario de propriedade adicionado aos objetos:
  FugaPET incremental 037 semi_acabado
- Rollback protegido por comentario de propriedade.
- Rollback aborta se houver dados, salvo forca explicita.
- Validacao transacional usa BEGIN/ROLLBACK real e confirma residuos zero.
- Validacao confirma SELECT/INSERT/UPDATE, ausencia de DELETE e USAGE nas sequences.
- CREATE INDEX permanece sem qualificar o nome do indice com schema.

============================================================
6. RELACAO COM O CODIGO C#
============================================================

O pacote de banco suporta as correcoes funcionais solicitadas para o Semiacabado:

- reenvio reconstruido pelo banco;
- lancamento persistido imutavel;
- contexto isolado por OP/item;
- bloqueio de DIVERGENCIA_SAP;
- historico confirmado para reimpressao;
- saldo correto por etiqueta via saldo_apos_pesagem_kg;
- reimpressao usando CodigoEtiqueta persistido.

A aplicacao deve mapear a coluna saldo_apos_pesagem_kg e gravar esse valor no momento
da pesagem. Reimpressao deve usar o valor persistido quando disponivel.

============================================================
7. CONFIRMACOES
============================================================

Nenhum script foi executado.
Nenhum dado foi alterado.
Nenhum rollback foi executado.
Nenhum commit foi realizado.
Nenhum codigo C# foi alterado.
HML nao foi alterado.
PRD nao foi incluido.
