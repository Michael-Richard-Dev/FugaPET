============================================================
034_etiqueta_regras_banco_GAIA
Cadastro de Etiqueta - regras de banco (PROPOSTA)
============================================================

PROPOSTA - NAO EXECUTAR.
Este pacote e documentacao/proposta para revisao do Richard/Gaia Dados.
Nenhum SQL deve ser aplicado automaticamente.

------------------------------------------------------------
O QUE ESTE PACOTE PROPOE
------------------------------------------------------------
1. Diagnostico de codigo interno duplicado por upper(trim(codigo_interno)),
   incluindo etiquetas ativas e inativas.
2. Validacoes de dados:
   - codigo_interno nao vazio e maximo 80 caracteres uteis;
   - nome_etiqueta entre 2 e 80 caracteres uteis;
   - descricao_etiqueta com maximo 255 caracteres uteis.
3. Indice unico GLOBAL por upper(trim(codigo_interno)), sem filtro de situacao.
4. Trigger BEFORE UPDATE para bloquear inativacao de etiqueta quando houver
   produto_etiqueta ativo vinculado.
5. Validacao pos-proposta e rollback completo.

------------------------------------------------------------
PASSOS QUANDO/SE APROVADO
------------------------------------------------------------
1. Fazer backup/snapshot do schema.
2. Rodar o preflight e resolver duplicidades antes da proposta.
3. Aplicar a proposta primeiro em DESENVOLVIMENTO.
4. Validar cadastro, edicao, inativacao e reativacao pelo sistema.
5. Adaptar schema para homologacao somente apos validacao.

------------------------------------------------------------
ARQUIVOS
------------------------------------------------------------
034_etiqueta_PREFLIGHT_GAIA.sql
034_etiqueta_regras_banco_PROPOSTA_GAIA.sql
034_etiqueta_regras_banco_ROLLBACK_GAIA.sql
034_etiqueta_validacao_GAIA.sql
README_034_ETIQUETA_GAIA.txt

------------------------------------------------------------
OBSERVACAO
------------------------------------------------------------
Nao executar este pacote pelo Codex nesta tarefa.
============================================================
