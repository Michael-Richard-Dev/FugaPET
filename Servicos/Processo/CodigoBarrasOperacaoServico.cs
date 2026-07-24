using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Definição ÚNICA e centralizada do formato do código de barras da operação. Nenhum outro ponto da
/// aplicação deve conter os tamanhos/sufixos ("números mágicos") — todos leem daqui.
///
/// Formato V1 validado somente para o processo atualmente analisado; futura parametrização por banco prevista.
/// (Enquanto a regra oficial ABAP não for confirmada, este formato NÃO deve ser tratado como universal.)
/// </summary>
public sealed class ConfiguracaoFormatoCodigoOperacao
{
    public const string VersaoV1 = "OP_OPERACAO_EVENTO_V1";

    public static ConfiguracaoFormatoCodigoOperacao V1 { get; } = new();

    private ConfiguracaoFormatoCodigoOperacao()
    {
    }

    public string CodigoFormato => VersaoV1;
    public int TamanhoOrdem => 12;
    public int TamanhoOperacao => 4;
    public int TamanhoEvento => 2;
    public string CodigoInicio => "01";
    public string CodigoTermino => "02";

    public int TamanhoTotal => TamanhoOrdem + TamanhoOperacao + TamanhoEvento;

    /// <summary>Evento configurado → tipo funcional. Null quando o sufixo não é um evento conhecido.</summary>
    public TipoEventoOperacao? ResolverEvento(string codigoEvento)
    {
        if (string.Equals(codigoEvento, CodigoInicio, StringComparison.Ordinal))
        {
            return TipoEventoOperacao.Inicio;
        }

        return string.Equals(codigoEvento, CodigoTermino, StringComparison.Ordinal)
            ? TipoEventoOperacao.Termino
            : null;
    }
}

/// <summary>
/// Interpreta o código de barras da operação da OP. PURO: não acessa SAP/banco e NUNCA exibe MessageBox —
/// devolve o resultado funcional em <see cref="CodigoBarrasOperacao"/>. O código completo jamais é
/// convertido para número e <c>CodigoOriginal</c> é preservado exatamente como lido.
/// </summary>
public sealed class CodigoBarrasOperacaoServico
{
    private readonly ConfiguracaoFormatoCodigoOperacao _formato;

    public CodigoBarrasOperacaoServico()
        : this(ConfiguracaoFormatoCodigoOperacao.V1)
    {
    }

    public CodigoBarrasOperacaoServico(ConfiguracaoFormatoCodigoOperacao formato)
        => _formato = formato ?? throw new ArgumentNullException(nameof(formato));

    public CodigoBarrasOperacao Interpretar(string? codigoLido)
    {
        // Preserva o original exatamente; só o Trim de bordas do leitor é tolerado para a análise.
        string original = codigoLido ?? string.Empty;
        string codigo = original.Trim();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return CodigoBarrasOperacao.Invalido(original, _formato.CodigoFormato, "Código não informado.");
        }

        if (codigo.Length != _formato.TamanhoTotal)
        {
            return CodigoBarrasOperacao.Invalido(
                original,
                _formato.CodigoFormato,
                $"Código deve ter {_formato.TamanhoTotal} caracteres; recebido {codigo.Length}.");
        }

        if (!codigo.All(char.IsAsciiDigit))
        {
            return CodigoBarrasOperacao.Invalido(
                original, _formato.CodigoFormato, "Código deve conter somente números.");
        }

        string ordemNormalizada = codigo[.._formato.TamanhoOrdem];
        string operacao = codigo.Substring(_formato.TamanhoOrdem, _formato.TamanhoOperacao);
        string codigoEvento = codigo.Substring(_formato.TamanhoOrdem + _formato.TamanhoOperacao, _formato.TamanhoEvento);

        // OP funcional = chave SAP (sem zeros à esquerda). A forma normalizada (12 pos.) é preservada.
        string ordemProducao = ordemNormalizada.TrimStart('0');
        if (string.IsNullOrEmpty(ordemProducao))
        {
            return CodigoBarrasOperacao.Invalido(
                original, _formato.CodigoFormato, "Ordem de produção vazia após a normalização.");
        }

        if (string.IsNullOrEmpty(operacao.TrimStart('0')))
        {
            return CodigoBarrasOperacao.Invalido(
                original, _formato.CodigoFormato, "Operação vazia no código.");
        }

        TipoEventoOperacao? tipoEvento = _formato.ResolverEvento(codigoEvento);
        if (tipoEvento is null)
        {
            return CodigoBarrasOperacao.Invalido(
                original,
                _formato.CodigoFormato,
                $"Evento '{codigoEvento}' não é reconhecido "
                + $"({_formato.CodigoInicio} = início, {_formato.CodigoTermino} = término).");
        }

        return new CodigoBarrasOperacao
        {
            CodigoOriginal = original,
            OrdemNormalizada = ordemNormalizada,
            OrdemProducao = ordemProducao,
            Operacao = operacao,
            CodigoEvento = codigoEvento,
            TipoEvento = tipoEvento,
            FormatoVersao = _formato.CodigoFormato,
            Valido = true,
            MensagemValidacao = string.Empty
        };
    }

    /// <summary>
    /// Gera o código no formato V1. Uso restrito: testes/validação/comparação com o código impresso.
    /// NUNCA usar como fonte oficial de um código já lido — nesse caso vale sempre o CodigoOriginal.
    /// </summary>
    public string Gerar(string ordemProducao, string operacao, TipoEventoOperacao tipoEvento)
    {
        string ordem = (ordemProducao ?? string.Empty).Trim();
        string op = (operacao ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(ordem))
        {
            throw new ArgumentException("Ordem de produção não informada.", nameof(ordemProducao));
        }

        if (string.IsNullOrEmpty(op))
        {
            throw new ArgumentException("Operação não informada.", nameof(operacao));
        }

        if (!ordem.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Ordem de produção deve conter somente números.", nameof(ordemProducao));
        }

        if (!op.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Operação deve conter somente números.", nameof(operacao));
        }

        if (ordem.Length > _formato.TamanhoOrdem)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordemProducao), $"Ordem excede {_formato.TamanhoOrdem} posições.");
        }

        if (op.Length > _formato.TamanhoOperacao)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operacao), $"Operação excede {_formato.TamanhoOperacao} posições.");
        }

        // Enum é validado explicitamente: qualquer valor diferente de Inicio NÃO vira Termino por padrão.
        string evento = tipoEvento switch
        {
            TipoEventoOperacao.Inicio => _formato.CodigoInicio,
            TipoEventoOperacao.Termino => _formato.CodigoTermino,
            _ => throw new ArgumentOutOfRangeException(
                nameof(tipoEvento), tipoEvento, "Tipo de evento desconhecido para o formato atual.")
        };

        return ordem.PadLeft(_formato.TamanhoOrdem, '0')
            + op.PadLeft(_formato.TamanhoOperacao, '0')
            + evento;
    }

    /// <summary>Chave de idempotência da leitura: código lido + versão do formato interpretado.</summary>
    public static string MontarIdempotencyKey(string codigoOriginal, string formatoVersao)
        => $"{formatoVersao}|{(codigoOriginal ?? string.Empty).Trim()}";
}
