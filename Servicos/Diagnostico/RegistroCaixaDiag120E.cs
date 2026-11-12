using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace FugaPET_Dev.Servicos.Diagnostico;

public enum EtapaRegistroCaixa120E
{
    MONTAR_CAIXA,
    CONSULTAR_CAIXA_ATIVA,
    MONTAR_PREVIEW,
    INSERIR_CAIXA,
    INSERIR_PESAGEM,
    FINALIZAR_LOCAL,
    VERIFICAR_FINALIZACAO_E_BINDING,
    SALVAR_PREVIEW,
    AGUARDAR_AUTORIZACAO,
    RELER_SNAPSHOT
}

internal static class RegistroCaixaDiag120E
{
    private static readonly object Sincronizacao = new();

    internal static string Formatar(EtapaRegistroCaixa120E etapa, Exception? erro = null)
    {
        List<object> excecoes = [];
        for (Exception? atual = erro; atual is not null; atual = atual.InnerException)
        {
            var postgres = atual as PostgresException;
            excecoes.Add(new
            {
                ExceptionType = atual.GetType().FullName,
                Message = Sanitizar(atual.Message),
                SqlState = postgres?.SqlState,
                ConstraintName = Sanitizar(postgres?.ConstraintName),
                TableName = Sanitizar(postgres?.TableName),
                ColumnName = Sanitizar(postgres?.ColumnName)
            });
        }

        return JsonSerializer.Serialize(new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Stage = etapa.ToString(),
            Exceptions = excecoes
        });
    }

    internal static void Registrar(EtapaRegistroCaixa120E etapa, Exception? erro = null)
    {
        try
        {
            string linha = Formatar(etapa, erro);
            lock (Sincronizacao)
            {
                string pasta = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(pasta);
                File.AppendAllText(Path.Combine(pasta, "pa_box_register_120e.log"), linha + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            System.Diagnostics.Trace.TraceWarning("PA120E: falha ao gravar diagnostico local de registro de caixa.");
        }
    }

    private static string? Sanitizar(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        foreach (EnvironmentVariableTarget alvo in Enum.GetValues<EnvironmentVariableTarget>())
        {
            foreach (System.Collections.DictionaryEntry entrada in Environment.GetEnvironmentVariables(alvo))
            {
                string nome = (string)entrada.Key;
                if (Regex.IsMatch(nome, "PASSWORD|SENHA|SECRET|TOKEN|CONEXAO", RegexOptions.IgnoreCase)
                    && entrada.Value is string segredo && segredo.Length > 0)
                {
                    texto = texto.Replace(segredo, "[REDACTED]", StringComparison.Ordinal);
                }
            }
        }

        texto = Regex.Replace(texto,
            @"(?i)\b(?:Host|Server|Data Source)\s*=.*", "[CONNECTION_STRING_REDACTED]");
        texto = Regex.Replace(texto, @"(?i)\b(?:Bearer|Basic)\s+\S+", "[REDACTED]");
        texto = Regex.Replace(texto,
            "(?i)(password|pwd|senha|secret|token|authorization|cookie)\\s*[:=]\\s*(?:\"[^\"]*\"|'[^']*'|[^;\\s]+)",
            "$1=[REDACTED]");
        return texto;
    }
}
