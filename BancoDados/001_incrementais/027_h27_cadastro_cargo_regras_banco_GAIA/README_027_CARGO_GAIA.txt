# 027 - H27 - Cadastro de Cargo - Regras de Banco - Proposta Gaia

Projeto: FugaPET_Dev  
Banco: PostgreSQL  
Schema alvo da proposta: `desenvolvimento`  
Objeto: `cargo`

## Objetivo

Criar proposta incremental para deixar o Cadastro de Cargo com regra equivalente ao padrão aprovado no Cadastro de Setor:

- `nome_cargo`: de 2 a 80 caracteres úteis após `trim`;
- `descricao_cargo`: até 255 caracteres úteis após `trim`;
- cargo em uso por usuário ativo não pode ser inativado;
- aplicação deve validar antes, mas o banco deve bloquear bypass por SQL direto.

## Arquivos

1. `027_h27_cadastro_cargo_PREFLIGHT_GAIA.sql`
   - Valida dados antes da aplicação.
   - Não altera dados nem estrutura.

2. `027_h27_cadastro_cargo_regras_banco_PROPOSTA_GAIA.sql`
   - Cria CHECKs funcionais.
   - Cria `fn_cargo_dependencias_ativas`.
   - Cria `vw_cargo_diagnostico_inativacao`.
   - Cria `fn_cargo_bloquear_inativacao_em_uso`.
   - Cria `trg_cargo_bloquear_inativacao_em_uso`.

3. `027_h27_cadastro_cargo_validacao_002_APPEND_GAIA.sql`
   - Bloco complementar para append/execução após o `002_validar_banco_desenvolvimento_v1_1.sql`.
   - Verifica constraints, funções, view, trigger e inconsistências.

4. `027_h27_cadastro_cargo_regras_banco_ROLLBACK_GAIA.sql`
   - Remove somente objetos incrementais da proposta 027.
   - Não altera dados.

## Ordem segura de aplicação em DEV

1. Fazer backup/snapshot do banco de desenvolvimento.
2. Executar `027_h27_cadastro_cargo_PREFLIGHT_GAIA.sql`.
3. Se o preflight aprovar, executar `027_h27_cadastro_cargo_regras_banco_PROPOSTA_GAIA.sql`.
4. Executar `027_h27_cadastro_cargo_validacao_002_APPEND_GAIA.sql`.
5. Testar no sistema:
   - nome com 1 caractere deve bloquear;
   - nome com 2 caracteres deve permitir;
   - nome com mais de 80 deve bloquear;
   - descrição com mais de 255 deve bloquear;
   - cargo sem usuário ativo deve permitir inativar;
   - cargo com usuário ativo deve bloquear inativação.

## Preflight necessário antes de HML

Antes de converter/aplicar em HML, ajustar o schema de `desenvolvimento` para o schema oficial de homologação, atualmente `homologacao`, e executar preflight real no HML.

Preflight deve confirmar:

- nomes fora de 2..80 = 0;
- descrições acima de 255 = 0;
- cargos ativos duplicados por `upper(trim(nome_cargo))` = 0;
- usuários ativos vinculados a cargos inativos = 0.

## Riscos

1. Dados legados inconsistentes podem reprovar o preflight.
2. A aplicação precisa consultar a view antes da inativação para exibir mensagem amigável.
3. O banco bloqueará bypass direto, mas a mensagem pode aparecer como erro técnico se o Service não tratar.
4. Para HML/produção, não aplicar com schema incorreto.

## Recomendação Gaia

A proposta está adequada para revisão técnica e aplicação controlada em desenvolvimento.

Não aplicar diretamente em produção. Para HML, gerar pacote específico com schema `homologacao`, preflight e validação próprios.
