using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Comportamento: ao negar uma acao operacional por falta de permissao, o evento deve ser
/// REGISTRADO em auditoria (acesso negado), com o usuario corrente e a descricao modulo/rotina/acao.
/// Usa um spy de AuditoriaServico injetado em AcaoNegadaHelper.
/// </summary>
public sealed class AcaoNegadaAuditoriaTests : IDisposable
{
    public AcaoNegadaAuditoriaTests()
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 42,
            Login = "operador",
            Nome = "Operador",
            Permissoes = [],
            IntegracaoBancoHabilitada = true
        });
    }

    [Fact] // #3
    public async Task RegistrarAcaoNegadaSeguroAsync_DeveRegistrarAcessoNegado()
    {
        AuditoriaSpy spy = new();

        await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.LeituraProducao,
            PermissoesSistema.Acoes.Executar,
            "executar leitura",
            "ProcessoProdutoAcabadoForm",
            spy);

        Assert.Single(spy.AcessosNegados);
        (long codigoUsuario, string motivo) = spy.AcessosNegados[0];
        Assert.Equal(42, codigoUsuario);
        Assert.Contains("executar leitura", motivo);
        Assert.Contains("PROCESSO_PRODUCAO/LEITURA_PRODUCAO/EXECUTAR", motivo);
    }

    [Fact]
    public async Task RegistrarAcaoNegadaSeguroAsync_SemSessao_NaoRegistra()
    {
        EstadoSessaoUsuarioAtual.Limpar();
        AuditoriaSpy spy = new();

        await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.LeituraProducao,
            PermissoesSistema.Acoes.Cancelar,
            "cancelar leitura",
            "ProcessoConsumoMaterialForm",
            spy);

        Assert.Empty(spy.AcessosNegados);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public List<(long CodigoUsuario, string Motivo)> AcessosNegados { get; } = new();

        public AuditoriaSpy() : base(null!) { }

        public override Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default)
        {
            AcessosNegados.Add((codigoUsuario, motivo));
            return Task.CompletedTask;
        }
    }
}

