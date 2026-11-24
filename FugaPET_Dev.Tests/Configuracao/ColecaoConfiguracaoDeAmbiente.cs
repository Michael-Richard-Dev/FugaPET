using Xunit;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// A coleção dos testes que dependem da CONFIGURAÇÃO DE AMBIENTE do processo.
/// <para>
/// Variável de ambiente é estado GLOBAL do processo, e o xUnit roda classes de teste diferentes em
/// PARALELO. Cada uma dessas classes faz a coisa certa sozinha — tira uma foto da variável, altera,
/// e restaura no <c>Dispose</c> —, mas o <c>Dispose</c> de uma rodando ao lado derruba a variável
/// no meio do teste da outra. O resultado é um teste que passa ou falha conforme o escalonamento.
/// </para>
/// <para>
/// Isso custou uma investigação em 24/11/2026:
/// <c>ConfiguracaoSchemaBancoTests.LeitorConnectionString_SemSearchPath_Q_DeveFalharFechadoSemSchema</c>
/// alternava entre passar e falhar <b>sem nenhuma mudança de código</b> — medido em três rodadas
/// seguidas: 125, 126, 125 falhas. Acrescentar testes novos em outra área bastava para mudar o
/// escalonamento e virar o resultado, o que faz qualquer base de comparação deixar de valer.
/// </para>
/// <para>
/// <b>Por que serializar em vez de "consertar" o teste:</b> ele não estava errado. O defeito era a
/// corrida, e um teste reescrito para tolerar a corrida esconderia o problema em vez de resolvê-lo —
/// e a corrida continuaria, pronta para derrubar o próximo.
/// </para>
/// <para>
/// Toda classe que LÊ ou ESCREVE configuração de ambiente entra aqui. Esquecer de marcar uma
/// devolve a intermitência, então vale conferir esta lista ao criar teste novo que mexa em
/// <c>FUGAPET_*_CONEXAO_POSTGRES</c> ou leia <c>LeitorConfiguracaoBancoPostgreSql</c>.
/// </para>
/// </summary>
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ColecaoConfiguracaoDeAmbiente
{
    public const string Nome = "Configuracao de ambiente do processo";
}
