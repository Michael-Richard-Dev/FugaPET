namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ContextoAuditoriaEntradaLotes
{
    public required long CodigoUsuario { get; init; }
    public required string LoginUsuario { get; init; }
    public required long CodigoSetorUsuario { get; init; }
    public required DateTime DataReferencia { get; init; }

    public static ContextoAuditoriaEntradaLotes Criar(
        long? codigoUsuario,
        string? loginUsuario,
        long? codigoSetorUsuario,
        DateTime dataReferencia)
    {
        if (codigoUsuario is not long usuario || usuario <= 0)
        {
            throw new InvalidOperationException("Usuario nao autenticado. Faca login para registrar a entrada.");
        }

        string loginNormalizado = ValidadorEntradaProdutoArvoreLotes.ValidarLoginAuditoriaLote(loginUsuario);
        if (codigoSetorUsuario is not long setor || setor <= 0)
        {
            throw new InvalidOperationException("Setor padrao do usuario e obrigatorio para registrar a entrada com lotes.");
        }

        return new ContextoAuditoriaEntradaLotes
        {
            CodigoUsuario = usuario,
            LoginUsuario = loginNormalizado,
            CodigoSetorUsuario = setor,
            DataReferencia = dataReferencia.Date
        };
    }
}
