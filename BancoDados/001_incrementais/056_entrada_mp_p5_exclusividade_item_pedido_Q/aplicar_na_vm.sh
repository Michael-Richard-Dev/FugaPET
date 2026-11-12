#!/usr/bin/env bash
# FUGAPET / P5 / 056 — aplicacao do incremento na VM do PostgreSQL.
#
# Sem argumento, roda APENAS o preflight, que e somente leitura.
# Com --aplicar, cria a tabela e roda a validacao em seguida.
#
# O papel precisa ser o DONO do schema (fugapet_q_owner): a aplicacao
# (fugapet_q_app) nao tem CREATE em homologacao, e e bom que continue assim.
#
# A senha NAO e passada por argumento nem fica no historico: use ~/.pgpass ou
# deixe o psql perguntar.

set -euo pipefail

BANCO="${BANCO:-fuga_jales_local_homologacao_q_v1_2}"
SERVIDOR="${SERVIDOR:-127.0.0.1}"
PORTA="${PORTA:-5432}"
PAPEL="${PAPEL:-fugapet_q_owner}"

APLICAR=0
[ "${1:-}" = "--aplicar" ] && APLICAR=1

cd "$(dirname "$0")"

for f in 056_PREFLIGHT.sql 056_PROPOSTA.sql 056_VALIDACAO.sql; do
    [ -f "$f" ] || { echo "ERRO: $f nao encontrado em $(pwd)"; exit 1; }
done

command -v psql >/dev/null 2>&1 || { echo "ERRO: psql nao encontrado nesta VM."; exit 1; }

# ON_ERROR_STOP garante que um erro no meio do script nao passe despercebido.
psql_exec() {
    psql --host="$SERVIDOR" --port="$PORTA" --dbname="$BANCO" --username="$PAPEL" \
         --no-psqlrc --set ON_ERROR_STOP=1 --file "$1"
}

echo "=================================================================="
echo " FUGAPET P5 / incremento 056 — exclusividade duravel por pedido+item"
echo "=================================================================="
echo "  banco   : $BANCO"
echo "  servidor: $SERVIDOR:$PORTA"
echo "  papel   : $PAPEL"
echo "  modo    : $([ "$APLICAR" -eq 1 ] && echo 'APLICAR (cria a tabela)' || echo 'PREFLIGHT (nao altera nada)')"
echo

echo "--- PREFLIGHT (somente leitura) ---"
psql_exec 056_PREFLIGHT.sql
echo

if [ "$APLICAR" -eq 0 ]; then
    echo "Nada foi alterado."
    echo "Confira acima: tabela_ja_existe deve ser 'f', dependencia_lancamento_presente 't'"
    echo "e role_app_presente 't'. Estando assim, rode de novo com --aplicar."
    exit 0
fi

echo "--- PROPOSTA (cria a tabela) ---"
psql_exec 056_PROPOSTA.sql
echo

echo "--- VALIDACAO (somente leitura) ---"
psql_exec 056_VALIDACAO.sql
echo

echo "=================================================================="
echo " Esperado na validacao:"
echo "   tabela_criada   = t      chaves_primarias = 1    checks >= 3"
echo "   app_le          = t      app_insere       = t"
echo "   app_sem_delete  = t      app_sem_update   = t"
echo "   linhas          = 0"
echo
echo " Qualquer valor diferente: nao prossiga com a publicacao e me mande"
echo " o retorno. Para desfazer, use 056_ROLLBACK.sql — mas so enquanto"
echo " 'linhas' for 0, porque apagar a tabela com linhas descarta a prova"
echo " de exclusividade ja concedida a algum recebimento."
echo "=================================================================="
