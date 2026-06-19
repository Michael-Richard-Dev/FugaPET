PACOTE BANCO HOMOLOGACAO V2.0 - FugaPET
===============================================

OBJETIVO
Criar um banco novo, já consolidado com o baseline e os incrementos validados.
O peso da tara e os pesos operacionais estão padronizados em quilogramas (kg).

ORDEM DE EXECUCAO
1. Crie um banco PostgreSQL novo e vazio.
2. Abra o Query Tool conectado ao banco novo.
3. Execute: 000_execucao_completa_homologacao_v2_0_consolidado.sql
4. Gere um hash BCrypt work factor 12 pela aplicação.
5. Abra 001_bootstrap_admin_homologacao_v2_0_pgadmin.sql.
6. Substitua <COLE_HASH_BCRYPT_AQUI> pelo hash e execute.
7. Execute: 002_validar_banco_homologacao_v2_0.sql

NAO EXECUTAR DEPOIS DO 000 V2.0
- 007_010_int012_incremental_pre_ares_v1_0.sql
- 010_criar_permissoes_etiqueta_campos_mapeamento_v1_0.sql
- 011_adicionar_tipo_pedido_compra_sap_v1_0.sql
- 012_criar_pesagem_entrada_item_v1_0.sql
- 013_pesagem_entrada_item_unicidade_e_log_v1_0.sql
- 014_adicionar_peso_item_pedido_compra_sap_v1_0.sql
- 015_renomear_peso_tara_para_grama_v1_0.sql
- 016_permitir_operador_finalizar_leitura_entrada_v1_0.sql
- 017_permitir_origem_pesagem_multipla_v1_0.sql

Todos esses incrementos aplicáveis já foram incorporados no 000 v2.0.
O antigo script 015 foi descartado: tara permanece em peso_kg numeric(14,3).

MODELO DE PESAGEM DE ENTRADA
- pesagem_entrada_item: resumo consolidado, compatível com a aplicação atual.
- pesagem_entrada_item_leitura: cada leitura individual de uma pesagem múltipla.

ADMINISTRADOR
O 000 cria o seed administrativo com placeholder por segurança.
O 001 substitui o placeholder por um hash BCrypt real e garante o perfil Administrador.

CRITERIO DE SUCESSO
- O 000 termina sem ERROR.
- O 001 mostra admin ativo, desbloqueado e com hash de 60 caracteres.
- O 002 termina sem EXCEPTION e as views abrem, mesmo vazias.
