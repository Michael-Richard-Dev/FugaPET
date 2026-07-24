============================================================
033_modelo_etiqueta_regras_banco_GAIA
Cadastro de Modelo de Etiqueta - regras de banco (PROPOSTA)
============================================================

PROPOSTA — NÃO EXECUTAR.
Este pacote é documentação/proposta para revisão do Richard. Nenhum SQL foi
executado. Não aplicar sem backup/snapshot e sem validar em DESENVOLVIMENTO primeiro.

------------------------------------------------------------
O QUE ESTE PACOTE PROPÕE
------------------------------------------------------------
Endurecer no banco as regras já validadas pela aplicação no Cadastro de Modelo de
Etiqueta, alinhando ao padrão maduro Setor/Cargo/Tara/Tipo de Tara:

1. Duplicidade GLOBAL: índice único por (upper(trim(nome_modelo_etiqueta)), versao),
   INDEPENDENTE da situação (ativo OU inativo). Mesmo nome com versão diferente é permitido.
2. Checks:
   - nome entre 2 e 80 caracteres úteis (após trim);
   - versao > 0;
   - dpi > 0 (quando informado);
   - largura_mm / altura_mm > 0 (quando informadas);
   - observacao <= 255;
   - conteudo_zpl não vazio.
3. Proteção de inativação: trigger BEFORE UPDATE que bloqueia inativar um modelo
   quando existe etiqueta ATIVA vinculada (etiqueta.situacao_etiqueta = true).
   Etiquetas inativas NÃO bloqueiam. Histórico de impressão / hu_caixa_etiqueta /
   hu_palete_etiqueta NÃO são considerados (guardam o ZPL renderizado, não dependem do modelo).

------------------------------------------------------------
POR QUE ISTO É NECESSÁRIO
------------------------------------------------------------
A inativação do modelo é um UPDATE de situacao_modelo_etiqueta; a FK RESTRICT da
etiqueta NÃO dispara nesse UPDATE. Por isso a verificação de "etiqueta ativa vinculada"
é feita explicitamente:
  - na aplicação (serviço + UPDATE atômico com NOT EXISTS);
  - e, opcionalmente, reforçada no banco por este trigger.

------------------------------------------------------------
PASSOS DE APLICAÇÃO (quando/se aprovado)
------------------------------------------------------------
1. Backup/snapshot do schema.
2. Rodar o bloco de PREFLIGHT (dentro do .sql). Se acusar duplicados nome+versão,
   RESOLVER manualmente antes (a aplicação orienta o usuário a localizar o registro
   existente e usar a ação Reativar). Não apagar dados automaticamente.
3. Aplicar o .sql em DESENVOLVIMENTO. Validar a aplicação (cadastro/edição/inativação/reativação).
4. Só então avaliar replicação para HML (ajustando o schema para 'homologacao').

------------------------------------------------------------
DIAGNÓSTICO (duplicados existentes)
------------------------------------------------------------
SELECT upper(trim(nome_modelo_etiqueta)) AS nome, versao, count(*)
  FROM desenvolvimento.modelo_etiqueta
 GROUP BY upper(trim(nome_modelo_etiqueta)), versao
HAVING count(*) > 1;

------------------------------------------------------------
ROLLBACK
------------------------------------------------------------
Ver o bloco de ROLLBACK comentado no final do arquivo .sql.
============================================================
