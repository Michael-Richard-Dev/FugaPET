#!/usr/bin/env bash
# FUGAPET / P5 / 056 — bloco unico para colar na VM. Escreve os SQLs e roda o preflight.
# Com --aplicar, cria a tabela e valida. Papel padrao: fugapet_q_owner.
set -euo pipefail
BANCO="${BANCO:-fuga_jales_local_homologacao_q_v1_2}"
SERVIDOR="${SERVIDOR:-127.0.0.1}"; PORTA="${PORTA:-5432}"; PAPEL="${PAPEL:-fugapet_q_owner}"
APLICAR=0; [ "${1:-}" = "--aplicar" ] && APLICAR=1
DIR=/tmp/fugapet_056; mkdir -p "$DIR"; cd "$DIR"
command -v psql >/dev/null || { echo "ERRO: psql ausente nesta VM."; exit 1; }

cat > 056_PREFLIGHT.sql <<'FIM_056_PREFLIGHT'
-- FUGAPET / ENTRADA MP / P5 / 056 / PREFLIGHT  (somente leitura)
-- Executar ANTES da proposta. Nenhuma linha deve surpreender.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT current_database()                               AS banco,
       current_user                                     AS papel_conectado,
       current_setting('port')                          AS porta,
       to_regclass('homologacao.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
                                                        AS tabela_ja_existe,
       to_regclass('homologacao.entrada_produto_lancamento') IS NOT NULL
                                                        AS dependencia_lancamento_presente,
       EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fugapet_q_app')
                                                        AS role_app_presente;

ROLLBACK;
FIM_056_PREFLIGHT

cat > 056_PROPOSTA.sql <<'FIM_056_PROPOSTA'
-- FUGAPET / ENTRADA MP / P5 / 056 / PROPOSTA
-- Exclusividade DURAVEL por pedido+item para o recebimento ROH.
--
-- Por que esta tabela existe: a reserva atual e por LANCAMENTO e nao impede que dois
-- lancamentos distintos do MESMO item do pedido enviem o movimento 101 com a quantidade
-- integral, duplicando o recebimento no SAP. A unicidade abaixo e o que torna essa
-- duplicidade impossivel entre lancamentos, instancias e reinicializacoes.
--
-- A linha NAO e removida apos o envio. Isso e deliberado: uma falha DEPOIS do POST deixa
-- a marca gravada, de modo que um reenvio cego fica bloqueado ate reconciliacao humana.
--
-- Idempotente. Nenhuma tabela existente e alterada.

BEGIN;

CREATE TABLE IF NOT EXISTS homologacao.entrada_mp_p5_exclusividade_item_pedido (
    numero_pedido      text        NOT NULL,
    numero_item        text        NOT NULL,
    codigo_lancamento  bigint      NOT NULL,
    obtida_em          timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT pk_entrada_mp_p5_exclusividade_item_pedido
        PRIMARY KEY (numero_pedido, numero_item),
    -- O item chega normalizado em 5 digitos ('00010'); o pedido, somente digitos.
    CONSTRAINT ck_entrada_mp_p5_exclusividade_pedido_formato
        CHECK (numero_pedido ~ '^[0-9]{1,10}$'),
    CONSTRAINT ck_entrada_mp_p5_exclusividade_item_formato
        CHECK (numero_item ~ '^[0-9]{5}$'),
    CONSTRAINT ck_entrada_mp_p5_exclusividade_lancamento_positivo
        CHECK (codigo_lancamento > 0)
);

COMMENT ON TABLE homologacao.entrada_mp_p5_exclusividade_item_pedido IS
    'P5/056: unicidade duravel por pedido+item do recebimento. A linha permanece apos o '
    'envio para que falha pos-POST nao autorize reenvio cego.';

-- Consulta de quem detem a exclusividade de um lancamento (diagnostico e recuperacao).
CREATE INDEX IF NOT EXISTS ix_entrada_mp_p5_exclusividade_lancamento
    ON homologacao.entrada_mp_p5_exclusividade_item_pedido (codigo_lancamento);

-- Somente leitura e insercao. A aplicacao NAO recebe DELETE nem UPDATE: liberar uma
-- exclusividade e ato administrativo deliberado, nunca efeito colateral do fluxo.
GRANT SELECT, INSERT ON homologacao.entrada_mp_p5_exclusividade_item_pedido TO fugapet_q_app;

COMMIT;
FIM_056_PROPOSTA

cat > 056_VALIDACAO.sql <<'FIM_056_VALIDACAO'
-- FUGAPET / ENTRADA MP / P5 / 056 / VALIDACAO  (somente leitura)
-- Executar DEPOIS da proposta. Esperado: tudo true, zero linhas na tabela.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT to_regclass('homologacao.entrada_mp_p5_exclusividade_item_pedido') IS NOT NULL
           AS tabela_criada,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'homologacao.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'p')                                        AS chaves_primarias,
       (SELECT count(*) FROM pg_constraint
         WHERE conrelid = 'homologacao.entrada_mp_p5_exclusividade_item_pedido'::regclass
           AND contype = 'c')                                        AS checks,
       has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'SELECT') AS app_le,
       has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'INSERT') AS app_insere,
       -- A aplicacao NAO pode apagar nem alterar: liberar exclusividade e ato administrativo.
       NOT has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'DELETE') AS app_sem_delete,
       NOT has_table_privilege('fugapet_q_app',
           'homologacao.entrada_mp_p5_exclusividade_item_pedido', 'UPDATE') AS app_sem_update,
       (SELECT count(*) FROM homologacao.entrada_mp_p5_exclusividade_item_pedido)
                                                                     AS linhas;

ROLLBACK;
FIM_056_VALIDACAO

cat > 056_ROLLBACK.sql <<'FIM_056_ROLLBACK'
-- FUGAPET / ENTRADA MP / P5 / 056 / ROLLBACK
-- Reverte a proposta. So e seguro enquanto nenhum recebimento tiver usado a trava:
-- apagar a tabela com linhas descarta a prova de exclusividade ja concedida.
-- Confira a contagem na VALIDACAO antes de executar.
BEGIN;

DROP TABLE IF EXISTS homologacao.entrada_mp_p5_exclusividade_item_pedido;

COMMIT;
FIM_056_ROLLBACK

psql_exec() { psql -h "$SERVIDOR" -p "$PORTA" -d "$BANCO" -U "$PAPEL" --no-psqlrc -v ON_ERROR_STOP=1 -f "$1"; }

echo "=== FUGAPET P5 / 056 — banco=$BANCO papel=$PAPEL modo=$([ $APLICAR -eq 1 ] && echo APLICAR || echo PREFLIGHT) ==="
echo "--- PREFLIGHT (somente leitura) ---"; psql_exec 056_PREFLIGHT.sql; echo

if [ "$APLICAR" -eq 0 ]; then
  echo "Nada alterado. Esperado: tabela_ja_existe=f, dependencia_lancamento_presente=t, role_app_presente=t."
  echo "Estando assim, rode: bash /tmp/fugapet_056/056.sh --aplicar"
  exit 0
fi

echo "--- PROPOSTA (cria a tabela) ---"; psql_exec 056_PROPOSTA.sql; echo
echo "--- VALIDACAO (somente leitura) ---"; psql_exec 056_VALIDACAO.sql; echo
echo "Esperado: tabela_criada=t  chaves_primarias=1  checks>=3  app_le=t  app_insere=t"
echo "          app_sem_delete=t  app_sem_update=t  linhas=0"
echo "Para desfazer (apenas enquanto linhas=0): psql ... -f /tmp/fugapet_056/056_ROLLBACK.sql"
