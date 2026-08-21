FUGAPET - INCREMENTAL 044 DEV - REV7
HU DE UMA CAIXA INDIVIDUAL DE PRODUTO ACABADO
Projeto: FugaPET - Sistema Local de Producao e Rastreabilidade PET
Responsavel: Michael Richard
Preparacao REV7: 2026-08-05
Agente: Gaia Dados V2

==============================================================================
1. CLASSIFICACAO E LIMITES DA ENTREGA
==============================================================================
CLASSIFICACAO_DA_PREPARACAO=PACOTE_044_DEV_REV7_PREPARADO_PARA_AUDITORIA
REVISAO=REV7_CORRECAO_ACL_UNIDIMENSIONAL
INCREMENTAL_PRESERVADO=044
AMBIENTE_ALVO_DOCUMENTAL=DEV
BANCO_ALVO_DOCUMENTAL=fuga_jales_local_desenvolvimento
SCHEMA_ALVO_DOCUMENTAL=desenvolvimento
POSTGRESQL_ALVO_MAJOR=15
POSTGRESQL_VERSAO_MINIMA=15.5
POSTGRESQL_AMBIENTE_DEV_COMPROVADO=15.5

SQL_EXECUTADO=NAO
PREFLIGHT_EXECUTADO=NAO
PROPOSTA_EXECUTADA=NAO
VALIDACAO_EXECUTADA=NAO
ROLLBACK_EXECUTADO=NAO
BANCO_DEV_ACESSADO=NAO
HML_ACESSADO_OU_ALTERADO=NAO
PRD_ACESSADO_OU_ALTERADO=NAO
SAP_ACESSADO=NAO
HU_CRIADA=NAO
HU_DOCUMENTAL_300014301_INSERIDA=NAO
COMMIT_GIT=NAO
PUSH_GIT=NAO

PACOTE_REV4_AUDITADO=044_produto_acabado_hu_caixa_GAIA_DEV_REV4(1).zip
SHA256_REV4=84838FF6AD715927C95C703D4DF4E09E33E36D60DDD459198DF31DA0C34A6720

PACOTE_BASE_REV5=044_produto_acabado_hu_caixa_GAIA_DEV_REV5.zip
SHA256_PACOTE_BASE_REV5=8269DEB14328D9983B9B5D082D12BEF21FE8B3EF4D6A7C01A4EB6CC36461E222
CODIGO_SAIDA_PSQL_REV5=3
SHA256_LOG_INTEGRAL_REV5=F5637A0F52EA81E11F754909C8D225442E9835EC6C7F8EA6345A7FF2B4618045
HASH_LOG_REV5_VERIFICADO_PELA_GAIA=NAO
ORIGEM_HASH_LOG_REV5=VALOR_INFORMADO_NA_AUTORIZACAO_DE_MICHAEL_RICHARD

Nesta missao de emissao da REV6, o catalogo do DEV nao foi consultado e nenhum
SQL foi executado. A tentativa controlada da REV5 acessou o banco somente para
o PREFLIGHT read only e foi interrompida na linha 587, antes da classificacao
final do script. O hash do log acima foi registrado como valor informado, sem
acesso da Gaia ao arquivo fisico do log integral.

==============================================================================
2. MODELAGEM PRESERVADA DA REV4
==============================================================================
DECISAO=EVOLUIR_ESTRUTURAS_EXISTENTES
TABELA_PARALELA=NAO
ALTERACAO_HU_PALETE=NAO
ALTERACAO_HU_PALETE_ITEM=NAO
DROP_CASCADE=NAO

Estruturas evoluidas:
- desenvolvimento.hu_caixa;
- desenvolvimento.hu_caixa_pesagem;
- desenvolvimento.hu_caixa_integracao_sap.

Correcoes REV4 preservadas integralmente:
- numeracao atomica por OP com pg_advisory_xact_lock;
- UNIQUE de correlation_id, codigo_caixa_local e OP + numero_caixa;
- claim_token UUID exclusivo por tentativa;
- resultado condicionado por codigo, tentativa e claim_token;
- protecao contra sucesso, erro ou timeout atrasados;
- autorizacao inicial auditada;
- reconciliacao somente a partir de INDETERMINADO_TIMEOUT;
- GET 404 sem liberacao automatica de novo POST;
- uma caixa ativa por terminal;
- cancelamento por UPDATE;
- historico append-only por eventos;
- ausencia de DELETE e de UPDATE direto para a role da aplicacao;
- PRECHECK, PROPOSTA, VALIDACAO e ROLLBACK separados.

==============================================================================
3. REV5 - VIEW DEPENDENTE
==============================================================================
VIEW_CONTROLADA=desenvolvimento.vw_hu_caixas_sem_palete

O PREFLIGHT audita:
- definicao exata por pg_get_viewdef;
- owner;
- comentario;
- ACL e grants efetivos;
- nomes, ordem e tipos das colunas expostas;
- views dependentes;
- funcoes que referenciam nominalmente a view;
- dependencias das quatro tabelas HU de caixa.

A PROPOSTA:
- captura definicao, owner, comentario, colunas e grants em tabelas temporarias;
- bloqueia se houver objeto dependente que exija remocao em cascata;
- remove somente a view, sem CASCADE, dentro da mesma transacao;
- altera os tipos das colunas da tabela;
- recria a view pela definicao exata capturada;
- restaura owner, comentario e grants;
- compara definicao e metadados antes/depois;
- abre a view com SELECT ... LIMIT 1;
- nao altera a regra de palete nem as tabelas hu_palete/hu_palete_item.

O ROLLBACK aplica a mesma estrategia: captura a definicao vigente, remove a
view sem CASCADE, restaura os tipos anteriores e recria exatamente a definicao,
o owner, o comentario, os grants e a ordem das colunas capturados antes do DROP.

==============================================================================
4. REV5 - PESAGEM MANUAL E BALANCA
==============================================================================
Nova coluna:
- desenvolvimento.hu_caixa_pesagem.origem_pesagem varchar(30) NOT NULL.

Contrato:
- valores aceitos: BALANCA e MANUAL;
- normalizacao para uppercase por trigger;
- BALANCA exige codigo_balanca nao nulo;
- MANUAL exige codigo_balanca nulo;
- codigo_balanca passa a aceitar NULL somente pelo contrato acima;
- peso_bruto, peso_liquido e peso_tara usam numeric(15,3);
- peso_bruto = peso_liquido + peso_tara;
- peso_bruto > 0, peso_liquido > 0 e peso_tara >= 0;
- peso_lido preserva o valor bruto historicamente capturado ou informado e nao
  e recalculado pela funcao de normalizacao.

A PROPOSTA bloqueia quando existem registros nas estruturas HU. Portanto nao ha
preenchimento automatico ou classificacao presumida de pesagens historicas.

A VALIDACAO prepara os quatro cenarios obrigatorios:
- MANUAL sem balanca aceito;
- MANUAL com balanca rejeitado;
- BALANCA sem codigo rejeitado;
- BALANCA com codigo ativo aceito.

==============================================================================
5. REV5 - DETECCAO COMPLETA DO ESTADO DO PACOTE
==============================================================================
NUCLEO_REV2_TOTAL_COLUNAS=59
NUCLEO_REV2_TOTAL_OBJETOS=34
REV5_COLUNAS_ADICIONAIS=2
REV5_OBJETOS_ADICIONAIS=3

Os 59/34 sao calculados integralmente antes do primeiro \if da PROPOSTA.
Os dois campos e tres objetos adicionais da REV5 sao calculados em conjunto.

Somente sao aceitos:
A. 0/59 colunas do nucleo, 0/34 objetos do nucleo, 0/2 colunas REV5 e
   0/3 objetos REV5: pacote nao aplicado;
B. 59/59, 34/34, 2/2 e 3/3: pacote REV5 integralmente aplicado.

Qualquer outra combinacao produz:
CLASSIFICACAO_OFICIAL=PROPOSTA_044_DEV_BLOQUEADA_ESTADO_PARCIAL

Nao existe mais encerramento por poucos sentinelas.

==============================================================================
6. REV5 - ETAG E MENSAGENS SAP SANITIZADAS
==============================================================================
A funcao interna fn_hu_caixa_inserir_evento recebe e persiste:
- odata_etag;
- sap_messages_sanitizadas jsonb.

ETag e mensagens sao registrados em:
- sucesso do POST na caixa principal e no evento CONFIRMADO;
- erro e timeout, quando informados;
- GET de reconciliacao;
- confirmacao por reconciliacao.

Contrato JSON:
- objeto JSON ou NULL;
- arrays e valores escalares sao rejeitados;
- chaves de segredo sao bloqueadas, sem diferenca de maiusculas/minusculas:
  Authorization, Cookie, x-csrf-token, password, senha, client_secret e
  access_token.

As mesmas protecoes existem na caixa principal e no historico.

==============================================================================
7. REV5 - CLASSIFICACAO CONTROLADA DE ERROS
==============================================================================
Resultados controlados:
- ERRO_DEFINITIVO;
- NAO_AUTORIZADO;
- BLOQUEADO_CONFIGURACAO.

ERRO_DEFINITIVO:
- somente a partir de ENVIANDO_SAP com tentativa e claim_token correntes;
- transiciona para ERRO_SAP;
- pode_reprocessar e parametro booleano controlado;
- novo envio exige fn_hu_caixa_liberar_reprocessamento com usuario, terminal e
  motivo, e somente quando o ultimo resultado permite reprocessamento.

NAO_AUTORIZADO:
- exige HTTP 401 ou 403;
- transiciona para ERRO_SAP;
- pode_reprocessar=false;
- a liberacao automatica ou explicita e bloqueada ate correcao de autenticacao
  e nova decisao formal de modelagem.

BLOQUEADO_CONFIGURACAO:
- usa funcao especifica antes do POST;
- exige usuario, terminal e erro sanitizado;
- nao cria claim_token;
- nao incrementa tentativas;
- transiciona para BLOQUEADA;
- registra evento LOCAL/BLOQUEADO_CONFIGURACAO.

Nao e aceito texto livre fora dos enums/checks definidos.

==============================================================================
8. REV5 - ROLE E PRIVILEGIOS
==============================================================================
PREFLIGHT, PROPOSTA e postcheck bloqueiam fugapet_dev_app quando houver:
- SUPERUSER;
- CREATEDB;
- CREATEROLE;
- REPLICATION;
- BYPASSRLS;
- CREATE efetivo no schema desenvolvimento;
- heranca de role com qualquer capacidade acima.

A PROPOSTA executa, de forma restrita:
REVOKE CREATE ON SCHEMA desenvolvimento FROM fugapet_dev_app;

Permissoes previstas para a aplicacao:
- SELECT em hu_caixa e INSERT somente nas colunas funcionais aprovadas;
- SELECT em hu_caixa_pesagem e INSERT somente nas colunas funcionais aprovadas;
- somente SELECT em hu_caixa_integracao_sap;
- EXECUTE apenas nas funcoes publicas controladas;
- USAGE/SELECT somente nas sequences necessarias.

Nao sao concedidos UPDATE direto na caixa, INSERT/UPDATE/DELETE no historico,
DELETE, TRUNCATE, CREATE, ALTER, DROP, OWNER, SUPERUSER ou BYPASSRLS.

O trigger fn_hu_caixa_validar_update permanece como segunda barreira contra um
grant acidental e exige o uso das funcoes controladas.

==============================================================================
9. REV5 - VALIDACOES PREPARADAS
==============================================================================
A VALIDACAO executara, somente quando futuramente autorizada:
- SET LOCAL ROLE fugapet_dev_app;
- INSERT de caixa e de pesagens pela role da aplicacao;
- quatro cenarios de origem da pesagem;
- EXECUTE das funcoes publicas;
- UPDATE direto da caixa rejeitado;
- escrita direta no historico rejeitada;
- grant UPDATE temporario dentro da transacao para provar a barreira do trigger;
- REVOKE explicito e ROLLBACK final;
- autorizacao sem usuario e sem terminal rejeitadas;
- autorizacao valida com evento AUTORIZACAO_ENVIO;
- claim da tentativa 1, erro, liberacao, claim da tentativa 2;
- sucesso, erro e timeout atrasados da tentativa 1 retornando false;
- persistencia do ETag e das mensagens na tentativa corrente;
- mensagens com segredo e mensagens em array rejeitadas;
- NAO_AUTORIZADO sem reprocessamento;
- BLOQUEADO_CONFIGURACAO sem claim ou consumo de tentativa;
- timeout, GET 404 e reconciliacao controlada;
- segundo claim retornando zero linhas;
- cancelamentos seguros e inseguros;
- HU externa duplicada rejeitada;
- historico sem evento aberto e ligado por claim_token;
- view existente, aberta, com nomes e ordem esperados;
- metadados e grants da view inalterados durante a VALIDACAO;
- regra de hu_palete_item preservada;
- zero residuos e nenhum grant temporario apos ROLLBACK.

A preservacao da view entre o estado anterior e posterior a PROPOSTA e validada
na propria transacao da PROPOSTA. A VALIDACAO posterior cria novo snapshot para
comprovar que seus testes nao alteram definicao, owner, comentario ou grants.

==============================================================================
10. ROLLBACK DE CONTINGENCIA
==============================================================================
O ROLLBACK:
- exige confirmacao textual explicita;
- bloqueia se o pacote estiver incompleto;
- bloqueia se existir qualquer caixa, pesagem, historico ou etiqueta;
- nao apaga historico ou dados;
- remove somente objetos do incremental;
- restaura tipos, constraints, FKs e comentarios anteriores;
- restaura exatamente a view capturada, sem DROP CASCADE;
- valida abertura, definicao e ordem das colunas restauradas;
- termina com COMMIT somente se todos os postchecks forem aprovados.

ROLLBACK_EXECUTADO_NESTA_ENTREGA=NAO

==============================================================================
11. RISCOS E PENDENCIAS
==============================================================================
- CATALOGO_REAL_DEV=NAO_COMPROVADO;
- CONTAGENS_REAIS_DEV=NAO_COMPROVADO;
- OWNER_REAL_DA_VIEW=NAO_COMPROVADO;
- GRANTS_REAIS_DA_VIEW=NAO_COMPROVADO;
- DEPENDENTES_REAIS_DA_VIEW=NAO_COMPROVADO;
- ESTADO_REAL_DA_ROLE_FUGAPET_DEV_APP=NAO_COMPROVADO;
- CONCORRENCIA_REAL_EM_DUAS_SESSOES=NAO_EXECUTADA.

O PREFLIGHT futuro deve ser executado isoladamente e auditado antes de backup,
PROPOSTA ou VALIDACAO. A existencia deste ZIP nao autoriza nenhuma execucao.

==============================================================================
12. ORDEM FUTURA CONTROLADA
==============================================================================
1. auditoria do ZIP REV6 e do MANIFEST_SHA256.txt;
2. autorizacao isolada do PREFLIGHT somente leitura;
3. auditoria integral da saida e classificacao;
4. backup DEV aprovado e restaurado em banco temporario;
5. autorizacao expressa da PROPOSTA;
6. auditoria do log da PROPOSTA;
7. autorizacao expressa da VALIDACAO com ROLLBACK;
8. ROLLBACK oficial somente em contingencia formal.

==============================================================================
13. ARTEFATOS
==============================================================================
- 044_produto_acabado_hu_caixa_DEV_PREFLIGHT_GAIA.sql
- 044_produto_acabado_hu_caixa_DEV_PROPOSTA_GAIA.sql
- 044_produto_acabado_hu_caixa_DEV_VALIDACAO_GAIA.sql
- 044_produto_acabado_hu_caixa_DEV_ROLLBACK_GAIA.sql
- README_044_PRODUTO_ACABADO_HU_CAIXA_GAIA.txt
- MANIFEST_SHA256.txt

Os hashes e tamanhos oficiais estao no MANIFEST_SHA256.txt.

====================================================================
CORRECOES DA REV4 PRESERVADAS INTEGRALMENTE NA REV5
====================================================================

1. PRIVILEGIOS DE INSERT POR COLUNA
- fugapet_dev_app nao recebe INSERT em nivel de tabela em hu_caixa ou hu_caixa_pesagem.
- hu_caixa: INSERT restrito exclusivamente as 22 colunas funcionais aprovadas.
- hu_caixa_pesagem: INSERT restrito exclusivamente as 10 colunas funcionais aprovadas.
- codigo_hu_caixa, numero_caixa, codigo_caixa_local, status, HU SAP, campos de integracao,
  auditoria, situacao e identidades nao sao gravaveis diretamente pela aplicacao.
- has_table_privilege(...,'INSERT') deve ser false; has_column_privilege valida a lista fechada.

2. VALIDACAO COM IDS NEGATIVOS
- a VALIDACAO concede temporariamente apenas INSERT(codigo_hu_caixa) e
  INSERT(codigo_hu_caixa_pesagem), insere IDs negativos, revoga imediatamente e comprova
  ausencia do privilegio antes de prosseguir.
- hu_caixa, codigo_caixa_local, numero_caixa e status_hu_caixa sao omitidos e gerados pelo trigger.
- o snapshot das sequences comprova que nenhuma sequence foi avancada.

3. PESAGEM COMPLETA
- peso_bruto, peso_liquido, peso_tara e codigo_usuario passam a NOT NULL.
- check fechado: peso_bruto>0, peso_liquido>0, peso_tara>=0 e
  peso_bruto=peso_liquido+peso_tara.
- origem MANUAL exige codigo_balanca NULL; BALANCA exige codigo_balanca presente.
- peso_lido permanece o valor historico efetivamente lido ou informado.

4. ERROS 401/403
- HTTP 401 ou 403 exige resultado NAO_AUTORIZADO.
- NAO_AUTORIZADO exige HTTP 401/403 e pode_reprocessar=false.
- NAO_AUTORIZADO nao habilita liberacao de reprocessamento.

5. CLAIM COM ATOR E TERMINAL
- assinatura oficial: fn_hu_caixa_claim_envio(bigint,bigint,text).
- valida usuario ativo e nao bloqueado, terminal obrigatorio e igualdade com o terminal da caixa.
- evento INICIADO registra o ator e terminal do claim.
- sucesso, erro e timeout recuperam o ator pelo evento INICIADO do claim_token, evitando
  assumir o usuario original da pesagem.

6. DETECCAO INTEGRAL
- 61 colunas e 37 objetos nominalmente completos ainda nao bastam.
- o caminho JA_APLICADO valida tipos, tamanhos, nulabilidade, defaults, constraints, FKs,
  indices/predicados, triggers/tabelas, owners, grants de tabela e coluna, sequences,
  EXECUTE, ausencia de PUBLIC, ausencia de UPDATE/CREATE e abertura/metadados da view.
- divergencia: PROPOSTA_044_DEV_BLOQUEADA_ESTADO_PARCIAL_OU_DIVERGENTE.
- o ROLLBACK aplica a mesma filosofia e recusa sentinelas parciais.

7. VIEW
- PREFLIGHT audita pg_attribute.attacl, comentarios por coluna, regras adicionais e triggers.
- a PROPOSTA bloqueia antes do DROP VIEW quando qualquer metadado adicional nao preservado existe.
- DROP CASCADE permanece proibido; regra de palete e objetos hu_palete/hu_palete_item nao mudam.


====================================================================
REV5 — COMPATIBILIDADE COM O AMBIENTE DEV REAL
====================================================================

EVIDENCIA_OPERACIONAL_RECEBIDA:
- maquina SRVFSARQUIVOS, IP 192.168.4.160;
- cluster PostgreSQL 15;
- servidor PostgreSQL 15.5;
- cliente psql PostgreSQL 15.5;
- porta 5432;
- incremental 044 ainda nao executado.

AJUSTE_EXCLUSIVO_DA_REV5:
- PREFLIGHT, PROPOSTA, VALIDACAO e ROLLBACK exigem PostgreSQL major 15;
- a versao minima aceita e 15.5 (server_version_num >= 150005);
- outras majors continuam bloqueadas;
- PostgreSQL 15.0 a 15.4 continuam bloqueados;
- nenhuma modelagem, constraint, indice, trigger, funcao, grant, view, regra SAP,
  regra de claim, timeout, reconciliacao, cancelamento ou palete foi removida;
- os identificadores documentais e classificacoes de execucao foram promovidos
  para REV5, evitando confusao com o pacote REV4 preservado.

AUDITORIA_ESTATICA_DE_COMPATIBILIDADE:
- nao foi identificado uso de sintaxe exclusiva de PostgreSQL 16, 17 ou 18;
- recursos utilizados pelo pacote estao disponiveis no PostgreSQL 15, incluindo
  jsonb, aclexplode, pg_get_functiondef, pg_advisory_xact_lock,
  hashtextextended, gen_random_uuid, indices parciais, triggers e SECURITY DEFINER;
- a validacao definitiva permanece condicionada ao PREFLIGHT real e auditado.

CLASSIFICACAO_DOCUMENTAL=PACOTE_044_DEV_REV7_PREPARADO_PARA_AUDITORIA
EXECUCAO_SQL=NAO_REALIZADA
ACESSO_DEV=NAO_REALIZADO
ACESSO_HML=NAO_REALIZADO
ACESSO_PRD=NAO_REALIZADO
CHAMADA_SAP=NAO_REALIZADA
COMMIT_GIT=NAO_REALIZADO
PUSH_GIT=NAO_REALIZADO

====================================================================
REV6 — CORRECAO MINIMA DO PREFLIGHT
====================================================================

ORIGEM_DA_CORRECAO:
- PREFLIGHT REV5 interrompido com CODIGO_SAIDA_PSQL=3;
- erro na linha 587: column "tabela_origem" does not exist;
- trecho REV5: ORDER BY tabela_origem::text, conname;
- trecho REV6: ORDER BY conrelid::regclass::text, conname;

ALTERACAO_REALIZADA=SUBSTITUICAO_EXCLUSIVA_DA_EXPRESSAO_INVALIDA_NO_ORDER_BY
LOGICA_FUNCIONAL_ALTERADA=NAO
MODELAGEM_ALTERADA=NAO
POSTGRESQL_ALVO_MAJOR=15
POSTGRESQL_VERSAO_MINIMA=15.5

REVISAO_ORDER_BY_COMPLETA=SIM
TOTAL_ORDER_BY_REVISADOS=26
ALIAS_USADO_DENTRO_DE_CAST_OU_EXPRESSAO=1
OUTRAS_OCORRENCIAS_EQUIVALENTES=0
OUTRAS_CORRECOES_NECESSARIAS=NAO

PREFLIGHT_REV6_TAMANHO_BYTES=41663
PREFLIGHT_REV6_SHA256=9FF7BE709EE7CA1DECBA92FC11820C990854AD65BF29DBEE450A26CF12B9BB67
PROPOSTA_PRESERVADA_BYTE_A_BYTE=SIM
PROPOSTA_SHA256=CE5A984B2C78C9907C3F7A7F0DC126E3864BA18E89B1CBA581E3BE33958B2E2F
VALIDACAO_PRESERVADA_BYTE_A_BYTE=SIM
VALIDACAO_SHA256=8AA408A697E064DA5E548954E2A6F6769D6504363045FC74185445F87EA8D756
ROLLBACK_PRESERVADO_BYTE_A_BYTE=SIM
ROLLBACK_SHA256=0D4A9F343F0FD1FAA7AA39295060595CB2EAED874B25212C01E1CB8E61DE6336

PREFLIGHT_EXECUTADO=NAO
PROPOSTA_EXECUTADA=NAO
VALIDACAO_EXECUTADA=NAO
ROLLBACK_EXECUTADO=NAO
BANCO_ACESSADO_NESTA_MISSAO=NAO
SAP_ACESSADO=NAO
CPI_ACESSADO=NAO
HU_CRIADA=NAO
HML_ACESSADO=NAO
PRD_ACESSADO=NAO
COMMIT_GIT=NAO
PUSH_GIT=NAO

CLASSIFICACAO_DOCUMENTAL_REV6=PACOTE_044_DEV_REV6_PREPARADO_PARA_AUDITORIA
====================================================================
REV7 - CORRECAO CONTROLADA DA ACL UNIDIMENSIONAL
====================================================================

ORIGEM_DA_CORRECAO:
- PROPOSTA REV6 executada de forma controlada e interrompida com codigo psql 3;
- falha reportada na linha 855: ACL arrays must be one-dimensional;
- SHA256_PROPOSTA_REV6=CE5A984B2C78C9907C3F7A7F0DC126E3864BA18E89B1CBA581E3BE33958B2E2F;
- SHA256_LOG_OFICIAL_INFORMADO=2AEFB967CED0F638F021F735AB7BBBC7480656016B7E62A067A873F69A1BD382.

CAUSA_RAIZ:
- COALESCE(c.relacl, ARRAY[]::aclitem[]) fornecia um array vazio de dimensao zero
  quando relacl era NULL;
- aclexplode exige uma ACL unidimensional e recusou esse valor;
- a linha 855 encerra o INSERT iniciado na linha 842; o ORDER BY nao era a causa.

CORRECAO:
- aclexplode passa a receber c.relacl diretamente;
- NULL produz conjunto vazio, preservando a semantica de ausencia de grants explicitos;
- nao foi usado acldefault, pois isso materializaria privilegios implicitos do owner
  como grants explicitos e alteraria a assinatura auditada;
- o mesmo padrao foi corrigido nos pontos equivalentes da PROPOSTA, VALIDACAO e ROLLBACK;
- PREFLIGHT nao continha COALESCE com ARRAY[]::aclitem[] e foi preservado byte a byte.

CONTAGEM_DE_AJUSTES:
PROPOSTA_OCORRENCIAS_CORRIGIDAS=2
VALIDACAO_OCORRENCIAS_CORRIGIDAS=2
ROLLBACK_OCORRENCIAS_CORRIGIDAS=1
PREFLIGHT_OCORRENCIAS_CORRIGIDAS=0

BANCO_ACESSADO=NAO
SQL_EXECUTADO=NAO
CSHARP_ALTERADO=NAO
COMMIT_GIT_EXECUTADO=NAO
PUSH_GIT_EXECUTADO=NAO
CLASSIFICACAO_DOCUMENTAL_REV7=PACOTE_044_DEV_REV7_PREPARADO_PARA_AUDITORIA
