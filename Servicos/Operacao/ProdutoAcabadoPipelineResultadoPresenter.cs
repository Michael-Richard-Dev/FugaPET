using System.Text.RegularExpressions;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

public enum IconeResultadoPipelineProdutoAcabado
{
    Informacao,
    Aviso,
    Erro
}

public sealed record ApresentacaoResultadoPipelineProdutoAcabado(
    string Titulo,
    string Mensagem,
    IconeResultadoPipelineProdutoAcabado Icone);

/// <summary>
/// Converte o resultado já retornado pelo pipeline 045 em mensagem operacional persistente.
/// Não executa SAP, não altera estado e não interpreta body OData bruto: usa apenas campos sanitizados já propagados.
/// </summary>
public static class ProdutoAcabadoPipelineResultadoPresenter
{
    private const string NaoExecutada101 = "A etapa 101 não foi executada.";
    private const string NaoExecutadaHu = "A HU não foi executada.";

    public static ApresentacaoResultadoPipelineProdutoAcabado Construir(ResultadoPipelineProdutoAcabado resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        ProdutoAcabadoPipelineSnapshot? s = resultado.Snapshot;
        if (s is null)
        {
            return ConstruirSemSnapshot(resultado);
        }

        if (s.Estado261 != EstadoMovimentoSap.Confirmado)
        {
            return ConstruirMovimentoNaoConfirmado(
                etapa: "261",
                tituloErroSap: "SAP — Movimento 261 não realizado",
                estado: s.Estado261,
                http: s.HttpStatus261,
                mensagem: s.Mensagem261Sanitizada,
                complemento: [NaoExecutada101, NaoExecutadaHu]);
        }

        if (s.Estado101 != EstadoMovimentoSap.Confirmado)
        {
            List<string> complemento =
            [
                LinhaDocumento("261 confirmado", s.MaterialDocument261, s.MaterialDocumentYear261),
                NaoExecutadaHu
            ];

            return ConstruirMovimentoNaoConfirmado(
                etapa: "101",
                tituloErroSap: "SAP — Entrada 101 não realizada",
                estado: s.Estado101,
                http: s.HttpStatus101,
                mensagem: s.Mensagem101Sanitizada,
                complemento: complemento);
        }

        if (s.EstadoHu != StatusIntegracaoCaixa.ConfirmadaSap)
        {
            return ConstruirHuNaoConfirmada(s, resultado.Mensagem);
        }

        return ConstruirSucesso(s);
    }

    private static ApresentacaoResultadoPipelineProdutoAcabado ConstruirSemSnapshot(ResultadoPipelineProdutoAcabado resultado)
    {
        string mensagem = Sanitizar(resultado.Mensagem);
        bool indeterminado = ContemIndeterminado(mensagem);
        bool reconciliacao = ContemReconciliacao(mensagem);

        string titulo = indeterminado
            ? "Atenção — Resultado indeterminado"
            : reconciliacao || resultado.UltimaEtapa == EtapaPipelineProdutoAcabado.Bloqueada
                ? "Atenção — Reconciliação necessária"
                : "Produto Acabado — Erro local antes do POST";

        string orientacao = indeterminado || reconciliacao
            ? "Não tente enviar novamente. É necessária reconciliação antes de qualquer retry."
            : "Nenhum POST SAP foi executado.";

        return new(titulo, MontarLinhas(new string?[] {
            $"Etapa: {DescreverEtapa(resultado.UltimaEtapa)}",
            $"Resultado: {(indeterminado ? "RESULTADO INDETERMINADO" : "ERRO LOCAL")}",
            $"Mensagem: {mensagem}",
            $"Orientação: {orientacao}"
        }), indeterminado || reconciliacao ? IconeResultadoPipelineProdutoAcabado.Aviso : IconeResultadoPipelineProdutoAcabado.Erro);
    }

    private static ApresentacaoResultadoPipelineProdutoAcabado ConstruirMovimentoNaoConfirmado(
        string etapa,
        string tituloErroSap,
        EstadoMovimentoSap estado,
        int? http,
        string mensagem,
        IReadOnlyList<string> complemento)
    {
        string msg = Sanitizar(mensagem);
        if (estado == EstadoMovimentoSap.IndeterminadoTimeout || ContemIndeterminado(msg))
        {
            return new("Atenção — Resultado indeterminado", MontarLinhas(new string?[] {
                $"Etapa: {etapa}",
                "Resultado: RESULTADO INDETERMINADO",
                Campo("HTTP", http?.ToString()),
                $"Mensagem: {msg}",
                "Orientação: Não tente enviar novamente. É necessária reconciliação antes de qualquer retry."
            }.Concat(complemento)), IconeResultadoPipelineProdutoAcabado.Aviso);
        }

        if (estado == EstadoMovimentoSap.Erro && http.HasValue)
        {
            SapMensagemPartes sap = ExtrairSap(msg);
            return new(tituloErroSap, MontarLinhas(new string?[] {
                $"Etapa: {etapa}",
                "Resultado: ERRO SAP",
                Campo("HTTP", http?.ToString()),
                Campo("Código SAP", sap.Codigo),
                Campo("Mensagem SAP", sap.MensagemSuperior),
                Campo("Detalhe SAP", sap.DetalheCompleto),
                Campo("Mensagem", sap.MensagemSuperior is null && sap.DetalheCompleto is null ? msg : null),
                "Orientação: Corrija a causa SAP indicada antes de tentar novo envio."
            }.Concat(complemento)), IconeResultadoPipelineProdutoAcabado.Erro);
        }

        return new($"Produto Acabado — Etapa {etapa} não realizada", MontarLinhas(new string?[] {
            $"Etapa: {etapa}",
            "Resultado: ERRO LOCAL",
            $"Mensagem: {msg}",
            "Orientação: Nenhum POST SAP foi executado para esta etapa."
        }.Concat(complemento)), IconeResultadoPipelineProdutoAcabado.Aviso);
    }

    private static ApresentacaoResultadoPipelineProdutoAcabado ConstruirHuNaoConfirmada(ProdutoAcabadoPipelineSnapshot s, string mensagemResultado)
    {
        string msg = Sanitizar(mensagemResultado);
        bool indeterminado = s.EstadoHu == StatusIntegracaoCaixa.IndeterminadoTimeout || ContemIndeterminado(msg);
        string titulo = indeterminado ? "Atenção — Resultado indeterminado" : "SAP — HU não criada";
        string resultado = indeterminado ? "RESULTADO INDETERMINADO" : s.EstadoHu == StatusIntegracaoCaixa.ErroSap ? "ERRO SAP" : "BLOQUEIO / RECONCILIAÇÃO 045";
        string orientacao = indeterminado
            ? "Não tente enviar novamente. É necessária reconciliação antes de qualquer retry."
            : "Verifique o estado da HU antes de qualquer nova tentativa.";

        return new(titulo, MontarLinhas(new string?[] {
            LinhaDocumento("261 confirmado", s.MaterialDocument261, s.MaterialDocumentYear261),
            LinhaDocumento("101 confirmado", s.MaterialDocument101, s.MaterialDocumentYear101),
            "Etapa: HU",
            $"Resultado: {resultado}",
            $"Estado HU: {MapeadorStatusHuCaixa.ParaTextoBanco(s.EstadoHu)}",
            Campo("HU SAP", s.HandlingUnitSap),
            $"Mensagem: {msg}",
            $"Orientação: {orientacao}"
        }), indeterminado ? IconeResultadoPipelineProdutoAcabado.Aviso : IconeResultadoPipelineProdutoAcabado.Erro);
    }

    private static ApresentacaoResultadoPipelineProdutoAcabado ConstruirSucesso(ProdutoAcabadoPipelineSnapshot s)
        => new("SAP — Envio concluído", MontarLinhas(new string?[] {
            LinhaDocumento("261 confirmado", s.MaterialDocument261, s.MaterialDocumentYear261),
            LinhaDocumento("101 confirmado", s.MaterialDocument101, s.MaterialDocumentYear101),
            Campo("HU confirmada", s.HandlingUnitSap),
            "Resultado: SUCESSO"
        }), IconeResultadoPipelineProdutoAcabado.Informacao);

    private static SapMensagemPartes ExtrairSap(string mensagem)
    {
        string? codigo = Regex.Match(mensagem, @"Código:\s*(?<v>[^\.]+)", RegexOptions.IgnoreCase).Groups["v"]?.Value.Trim();
        string? mensagemSuperior = Regex.Match(mensagem, @"Mensagem:\s*(?<v>.*?)(?:\.\s*Detalhes:|$)", RegexOptions.IgnoreCase).Groups["v"]?.Value.Trim().TrimEnd('.');
        string? detalhes = Regex.Match(mensagem, @"Detalhes:\s*(?<v>.+)$", RegexOptions.IgnoreCase).Groups["v"]?.Value.Trim();
        string? detalhe = null;
        string? detalheMensagem = null;

        if (!string.IsNullOrWhiteSpace(detalhes))
        {
            string primeiro = detalhes.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? detalhes;
            Match m = Regex.Match(primeiro, @"(?<detalhe>[A-Z0-9_]+/[0-9]+):\s*(?<msg>.+)");
            if (m.Success)
            {
                detalhe = m.Groups["detalhe"].Value.Trim();
                detalheMensagem = m.Groups["msg"].Value.Trim().TrimEnd('.');
            }
        }

        mensagemSuperior = ValorOuNulo(mensagemSuperior);
        detalhe = ValorOuNulo(detalhe);
        detalheMensagem = ValorOuNulo(detalheMensagem);
        string? detalheCompleto = detalhe is null ? detalheMensagem : detalheMensagem is null ? detalhe : $"{detalhe} — {detalheMensagem}";
        if (string.Equals(mensagemSuperior, detalheMensagem, StringComparison.OrdinalIgnoreCase))
        {
            mensagemSuperior = null;
        }

        return new(ValorOuNulo(codigo), mensagemSuperior, detalheCompleto);
    }

    private static string LinhaDocumento(string prefixo, string? documento, string? ano)
        => string.IsNullOrWhiteSpace(documento)
            ? prefixo
            : string.IsNullOrWhiteSpace(ano) ? $"{prefixo}: {documento}" : $"{prefixo}: {documento}/{ano}";

    private static string? Campo(string nome, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : $"{nome}: {Sanitizar(valor)}";

    private static string MontarLinhas(IEnumerable<string?> linhas)
        => string.Join(Environment.NewLine, linhas.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => SanitizarLinha(l!)));

    private static string SanitizarLinha(string linha)
        => Sanitizar(linha).Trim();

    private static string Sanitizar(string valor)
    {
        string limpo = valor.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        limpo = SanitizarEstruturasSensiveis(limpo);
        return limpo.Length <= 1800 ? limpo : limpo[..1800] + "...";
    }

    private static string SanitizarEstruturasSensiveis(string texto)
    {
        string limpo = texto;
        limpo = Regex.Replace(limpo, @"\b(Authorization)\s*[:=]\s*(?:Basic|Bearer)?\s*[^\s;|,]+", "$1: [REMOVIDO]", RegexOptions.IgnoreCase);
        limpo = Regex.Replace(limpo, @"\b(Set-Cookie|Cookie)\s*[:=]\s*[^\s;|,]+", "$1: [REMOVIDO]", RegexOptions.IgnoreCase);
        limpo = Regex.Replace(limpo, @"\b(X-CSRF-Token|CSRF|claim_token|client_secret|senha|password|passwd|pwd|Host|Username)\s*(:|=)\s*[^\s;|,]+", m => $"{m.Groups[1].Value}{NormalizarSeparador(m.Groups[2].Value)}[REMOVIDO]", RegexOptions.IgnoreCase);
        limpo = Regex.Replace(limpo, @"\b(Connection\s*String)\s*(:|=)\s*[^|]+", m => $"{m.Groups[1].Value}{NormalizarSeparador(m.Groups[2].Value)}[REMOVIDO]", RegexOptions.IgnoreCase);
        return limpo;
    }

    private static string NormalizarSeparador(string separador)
        => separador.Contains(':', StringComparison.Ordinal) ? ": " : "=";

    private static string? ValorOuNulo(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : Sanitizar(valor);

    private static bool ContemIndeterminado(string mensagem)
        => mensagem.Contains("indeterminado", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("timeout", StringComparison.OrdinalIgnoreCase);

    private static bool ContemReconciliacao(string mensagem)
        => mensagem.Contains("reconcil", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("Não tente enviar", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("Nao tente enviar", StringComparison.OrdinalIgnoreCase);

    private static string DescreverEtapa(EtapaPipelineProdutoAcabado etapa)
        => etapa switch
        {
            EtapaPipelineProdutoAcabado.Movimento261 => "261",
            EtapaPipelineProdutoAcabado.Movimento101 => "101",
            EtapaPipelineProdutoAcabado.HandlingUnit => "HU",
            EtapaPipelineProdutoAcabado.Concluido => "Concluído",
            EtapaPipelineProdutoAcabado.Bloqueada => "Bloqueada",
            _ => etapa.ToString()
        };

    private sealed record SapMensagemPartes(string? Codigo, string? MensagemSuperior, string? DetalheCompleto);
}







