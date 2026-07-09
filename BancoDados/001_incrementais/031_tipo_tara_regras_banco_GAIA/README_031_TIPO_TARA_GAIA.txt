031 - Cadastro de Tipo de Tara - regras de banco (PROPOSTA)
==========================================================

STATUS: PROPOSTA. NAO aplicada. Aguardando validacao do Richard.
Ambiente: corrigir/validar primeiro no DEV. HML somente apos validacao. Sem commit.

Contexto
--------
Foi possivel cadastrar dois Tipos de Tara com o mesmo nome (um Ativo e outro
Inativo). A regra antiga bloqueava apenas duplicidade ATIVA. A regra definitiva
e: nome unico GLOBAL por upper(trim(nome_tipo_tara)), independente da situacao.

O que a APLICACAO ja faz (nesta tarefa, sem SQL aplicado)
--------------------------------------------------------
- TipoTaraRepositorio.ExisteNomeAsync: verificacao GLOBAL (removido o filtro
  situacao_tipo_tara = true).
- TipoTaraServico: Inserir/Atualizar/Reativar bloqueiam duplicidade global e
  orientam a reativar o registro existente; validacao de nome 2..80 e
  descricao <=255; situacao muda apenas por Inativar/Reativar (nao pela edicao);
  inativacao bloqueada quando ha tara ativa vinculada.
- TipoTaraForm: combo Situacao = DropDownList; MaxLength (80/255); botao de
  status alterna Inativar/Reativar; situacao nao e editavel no registro
  selecionado.

O que este SCRIPT propoe (endurecimento no banco, NAO aplicado)
---------------------------------------------------------------
1. Preflight: falha se ja houver duplicados globais / tamanhos invalidos.
2. Checks: nome 2..80; descricao <=255.
3. Indice UNICO GLOBAL: uq_tipo_tara_nome_global em upper(trim(nome_tipo_tara)).
4. Trigger: bloqueia inativacao (Ativo->Inativo) se houver tara ativa vinculada.

DUPLICADOS EXISTENTES NO DEV (tratar ANTES de aplicar o indice unico)
---------------------------------------------------------------------
Consulta de diagnostico (somente leitura, nao corrige nada):

    SELECT upper(trim(nome_tipo_tara)) AS nome_normalizado,
           count(*) AS quantidade,
           string_agg(codigo_tipo_tara::text || ':' || nome_tipo_tara || ':' || situacao_tipo_tara::text, ', ' ORDER BY codigo_tipo_tara) AS registros
      FROM desenvolvimento.tipo_tara
     GROUP BY upper(trim(nome_tipo_tara))
    HAVING count(*) > 1;

Como resolver (manual, sem correcao automatica):
- Escolher qual registro permanece; reativar/renomear conforme o caso.
- So depois aplicar o indice unico global.

A mesma consulta esta exposta na aplicacao:
  TipoTaraController.ListarNomesDuplicadosAsync() -> TipoTaraServico -> Repositorio.
