# Pendencias de testes com banco

Suite automatizada (xUnit) cobre, sem banco, as guardas de validacao/autorizacao que rodam
ANTES de tocar repositorio e tambem contratos estaticos dos fluxos criticos:

- SenhaServico (hash BCrypt, rejeicao de senha vazia, verificacao)
- PermissoesSistema (modulos/rotinas oficiais usados pelo banco)
- AutorizacaoServico (permissao por modulo/rotina/acao, GERENCIAR, sem bypass por nome de perfil, sem equivalencias antigas)
- UsuarioServico (guardas de InserirComVinculos/Atualizar)
- UsuarioPerfilServico (SincronizarPerfilUnico/Vincular/Remover — ids invalidos)
- PermissaoServico (InserirAsync, AtualizarAsync, SincronizarPermissoesDoPerfilAsync — perfil invalido)

Testes COMPORTAMENTAIS (nao contratuais) ja cobertos:

- AcessoMenuSegurancaTests: perfil so-SEGURANCA acessa o menu Seguranca e NAO depende de CADASTRO. (#1/#2)
- TelaSimuladaBloqueioTests: fora do modo demonstracao, tela simulada e bloqueada (PodeUsarDadosSimulados=false). (#7)
- PermissoesSistemaTests.TodaPermissaoSeedadaNoSql_DeveExistirComoConstante: PermissoesSistema cobre 100% do seed. (#5)
- AcaoNegadaAuditoriaTests: acao operacional negada registra acesso negado (spy de AuditoriaServico). (#3)
- MapeamentoCampoEtiquetaServicoTests: excecao generica em SalvarAsync vira falha amigavel (repo fake que lanca). (#4)
- UsuarioAtualizacaoVinculosTests: AtualizarComVinculosAsync repassa EXATAMENTE 1 perfil ao repositorio (orquestracao). (#6)
- RepositorioIntegracaoBancoTests: quando `FUGAPET_DEV_TESTE_CONNECTION_STRING` aponta para banco
  dedicado de teste, prova com PostgreSQL real:
  - UsuarioPerfilRepositorio.SincronizarPerfilUnicoAsync mantem exatamente um perfil ativo.
  - UsuarioRepositorio.AtualizarComVinculosAsync atualiza usuario, perfil unico e setor padrao.
  - PerfilPermissaoRepositorio.SincronizarPermissoesAsync bloqueia remocao das permissoes essenciais do ultimo administrador.

Como executar os testes reais de repositorio:

1. Criar/provisionar um banco exclusivo de teste, nunca homologacao/producao.
2. Garantir que o nome do database contenha `teste` ou `test`.
3. Aplicar o schema `homologacao` no banco de teste.
4. Configurar a variavel:

   `FUGAPET_DEV_TESTE_CONNECTION_STRING=Host=localhost;Port=5432;Database=fuga_balanca_teste;Username=...;Password=...`

5. Rodar `dotnet test`.

Sem essa variavel, os testes de integracao nao executam operacoes no banco, evitando escrita acidental
em ambiente real.

Seams criados para esses testes (sem extrair interface): AuditoriaServico e os repositorios
MapeamentoCampoEtiquetaRepositorio/UsuarioRepositorio sao nao-sealed com metodos virtuais,
permitindo spy/fake por heranca (construidos com base(null!)).
- UsuarioRepositorio.AtualizarComVinculosAsync (contrato: transacao, usuario, perfil unico, setor padrao)
- UsuarioPerfilRepositorio.SincronizarPerfilUnicoAsync (contrato: inativa ativos, reativa/inserir selecionado)
- PerfilPermissaoRepositorio.SincronizarPermissoesAsync (contrato: protecao de permissoes administrativas essenciais)
- MapeamentoCampoEtiquetaServico.SalvarAsync (contrato: consulta existente antes de decidir CRIAR/EDITAR)

Ainda precisam de abstracao (interface de repositorio) ou banco de teste controlado para
cobrir os FLUXOS COMPLETOS com acesso real:

- InserirComVinculosAsync de ponta a ponta

Nao criar testes contra o banco local de homologacao/desenvolvimento, para evitar falso positivo
e alteracao de dados reais. Caminho recomendado: extrair interfaces dos repositorios e usar fakes,
ou provisionar banco efemero dedicado para testes de integracao.
