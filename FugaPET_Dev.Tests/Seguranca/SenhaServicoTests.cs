using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

public sealed class SenhaServicoTests
{
    [Fact]
    public void GerarHash_DeveGerarHashBCrypt_E_NaoGuardarSenhaPura()
    {
        SenhaServico servico = new();
        string senha = "SenhaForte123";

        string hash = servico.GerarHash(senha);

        Assert.NotEqual(senha, hash);
        Assert.True(SenhaServico.EhHashBCrypt(hash));
        Assert.True(servico.Verificar(senha, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GerarHash_DeveRejeitarSenhaVazia(string senha)
    {
        SenhaServico servico = new();

        Assert.Throws<ArgumentException>(() => servico.GerarHash(senha));
    }

    [Theory]
    [InlineData("senha", "")]
    [InlineData("senha", "texto-puro")]
    [InlineData("senha", "$2b$hash-invalido")]
    public void Verificar_DeveRetornarFalse_ParaHashInvalido(string senha, string hash)
    {
        SenhaServico servico = new();

        Assert.False(servico.Verificar(senha, hash));
    }
}
