032 - Cadastro de Tara - regras de banco (PROPOSTA)
===================================================

STATUS: PROPOSTA. NÃO aplicada. Aguardando validação do Richard.
Ambiente: corrigir/validar primeiro no DEV. HML depois. Sem commit.

O que a APLICAÇÃO já faz (nesta tarefa, sem SQL aplicado)
--------------------------------------------------------
- TaraRepositorio.ExisteNomeNoSetorTipoAsync: verificação GLOBAL por setor+tipo
  (removido o filtro situacao_tara = true).
- TaraServico: Inserir/Atualizar/Reativar bloqueiam duplicidade global por setor+tipo
  e orientam a reativar o existente; ValidarENormalizar (nome 2..80, tamanho <=80,
  observacao <=255, peso_kg > 0, KG sem gramas); situação muda apenas por
  Inativar/Reativar (Atualizar não altera situação).
- TaraRepositorio.AtualizarAsync não atualiza mais situacao_tara.
- TaraForm: combos DropDownList (Tipo/Setor/Situação), MaxLength (80/255/tamanho 80),
  peso validado por TryParsePesoKg (vírgula/ponto, > 0, nunca zero silencioso),
  botão F8 alterna Inativar/Reativar, operação protegida, F5/F6/F8, controle de
  acesso direto, modo de card.

O que este SCRIPT propõe (endurecimento no banco, NÃO aplicado)
--------------------------------------------------------------
1. Preflight: falha se houver duplicados (setor+tipo+nome) ou tamanhos inválidos.
2. Checks: nome 2..80; tamanho <=80; observacao <=255; (peso_kg > 0 opcional).
3. Índice UNICO GLOBAL: uq_tara_setor_tipo_nome_global em
   (codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))).
   ATENÇÃO: se existir hoje um índice PARCIAL WHERE situacao_tara
   (uq_tara_setor_tipo_nome), removê-lo antes (permitia ativo+inativo).

DUPLICADOS EXISTENTES (tratar ANTES do índice único)
----------------------------------------------------
Consulta de diagnóstico (somente leitura, não corrige):

    SELECT codigo_setor, codigo_tipo_tara, upper(trim(nome_tara)) AS nome_normalizado,
           count(*) AS quantidade,
           string_agg(codigo_tara::text || ':' || nome_tara || ':' || situacao_tara::text, ', ' ORDER BY codigo_tara) AS registros
      FROM desenvolvimento.tara
     GROUP BY codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))
    HAVING count(*) > 1;

DEPENDÊNCIAS DE INATIVAÇÃO
--------------------------
Não foi identificado vínculo ativo/operacional persistente a codigo_tara que
justifique bloquear a inativação (o uso da tara na pesagem/entrada é histórico).
Por isso NÃO há bloqueio de inativação por dependência para Tara (diferente do
Tipo de Tara → tara ativa). Se surgir vínculo ativo real, adicionar aqui e no serviço.
