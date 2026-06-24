README — 028 H28 CADASTRO DE BALANCA — GAIA DADOS

Projeto:
FugaPET_Dev — Banco PostgreSQL — Cadastro de Balanca.

Schema alvo desta proposta:
desenvolvimento

Arquivos:
1. 028_h28_cadastro_balanca_PREFLIGHT_GAIA.sql
2. 028_h28_cadastro_balanca_regras_banco_PROPOSTA_GAIA.sql
3. 028_h28_cadastro_balanca_validacao_002_APPEND_GAIA.sql
4. 028_h28_cadastro_balanca_regras_banco_ROLLBACK_GAIA.sql

Objetivo:
Endurecer o cadastro de balanca com regras equivalentes ao padrao aprovado em Setor e Cargo, acrescentando validacoes tecnicas por tipo de conexao e bloqueio de inativacao quando existir uso operacional ativo.

Regras implementadas:
- nome_balanca: 2 a 80 caracteres uteis apos trim.
- identificacao_local: ate 120 caracteres uteis.
- porta_serial: ate 50 caracteres uteis.
- tipo_conexao: SERIAL, TCP_IP, USB ou MANUAL.
- paridade: NONE, EVEN, ODD, MARK ou SPACE.
- stop_bits: 1, 1.5 ou 2.
- flow_control: opcional; quando preenchido, NONE, XON_XOFF, RTS_CTS ou DTR_DSR.
- protocolo: ate 50 caracteres uteis.
- observacao: ate 255 caracteres uteis.
- TCP_IP exige endereco_ip e porta_tcp valida entre 1 e 65535.
- SERIAL exige porta_serial, baud_rate, data_bits, paridade e stop_bits validos.
- USB exige identificacao_local.
- MANUAL nao exige dados fisicos.
- Balanca em uso operacional nao pode ser inativada.

Dependencias operacionais consideradas:
- entrada_produto_pesagem ativa.
- hu_caixa ativa.
- hu_caixa_pesagem ativa.
- pesagem_entrada_item ativa.
- pesagem_entrada_item_leitura ativa.

Objetos criados:
- ck_balanca_nome_tamanho_funcional
- ck_balanca_identificacao_local_tamanho
- ck_balanca_porta_serial_tamanho
- ck_balanca_paridade_valores
- ck_balanca_stop_bits_valores
- ck_balanca_flow_control_valores
- ck_balanca_protocolo_tamanho
- ck_balanca_observacao_tamanho
- ck_balanca_tcp_ip_campos_obrigatorios
- ck_balanca_serial_campos_obrigatorios
- ck_balanca_usb_identificacao_obrigatoria
- fn_balanca_dependencias_ativas(bigint)
- vw_balanca_diagnostico_inativacao
- fn_balanca_bloquear_inativacao_em_uso()
- trg_balanca_bloquear_inativacao_em_uso

Ordem segura recomendada:
1. Fazer backup/snapshot do banco DEV.
2. Executar 028_h28_cadastro_balanca_PREFLIGHT_GAIA.sql.
3. Se o preflight aprovar, executar 028_h28_cadastro_balanca_regras_banco_PROPOSTA_GAIA.sql.
4. Executar 028_h28_cadastro_balanca_validacao_002_APPEND_GAIA.sql.
5. Testar o Cadastro de Balanca na aplicacao.
6. Testar cadastro SERIAL/TCP_IP/USB/MANUAL.
7. Testar tentativa de inativacao de balanca sem uso operacional.
8. Testar tentativa de inativacao de balanca com pesagem/HU/leituras ativas.
9. Somente depois preparar versao HML com schema homologacao.

Riscos conhecidos:
- Dados ja existentes fora dos limites funcionais fazem o preflight reprovar.
- Balanças SERIAL existentes sem porta_serial/baud_rate/data_bits/paridade/stop_bits validos serao bloqueadas.
- Balanças TCP_IP existentes sem endereco_ip/porta_tcp validos serao bloqueadas.
- Balanças USB existentes sem identificacao_local serao bloqueadas.
- Regra de inativacao pode impactar telas que tentavam inativar balancas usadas sem diagnostico previo.

Recomendacao Gaia:
A aplicacao deve consultar desenvolvimento.vw_balanca_diagnostico_inativacao antes de tentar inativar. Se pode_inativar = false, exibir mensagem amigavel com as dependencias. O trigger existe para bloquear bypass por SQL direto.

Observacao:
Nao aplicar este pacote diretamente em HML ou producao. Para HML, gerar pacote especifico trocando schema desenvolvimento por homologacao e validando no banco fuga_jales_local_homologacao_v1_2.
