using System.Text.Json.Nodes;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class AuditoriaAcaoUsuarioRepositorio : RepositorioBase
{
    public AuditoriaAcaoUsuarioRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task RegistrarAsync(AuditoriaAcaoUsuarioCadastro evento, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO auditoria_acao_usuario
            (codigo_usuario, acao, modulo, tela, resultado, mensagem, ip_origem, nome_maquina, dados_contexto,
             auditoria_acao_usuario_criado_por, situacao_auditoria_acao_usuario)
            VALUES
            (@codigo_usuario, @acao, @modulo, @tela, @resultado, @mensagem, @ip_origem, @nome_maquina,
             CAST(@dados_contexto AS jsonb), @criado_por, true);
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);

        comando.Parameters.Add(ParametroLongoNulo("@codigo_usuario", evento.CodigoUsuario));
        comando.Parameters.Add(ParametroTexto("@acao", evento.Acao));
        comando.Parameters.Add(new NpgsqlParameter("@modulo", string.IsNullOrWhiteSpace(evento.Modulo) ? DBNull.Value : evento.Modulo));
        comando.Parameters.Add(new NpgsqlParameter("@tela", string.IsNullOrWhiteSpace(evento.Tela) ? DBNull.Value : evento.Tela));
        comando.Parameters.Add(ParametroTexto("@resultado", string.IsNullOrWhiteSpace(evento.Resultado) ? "SUCESSO" : evento.Resultado));
        comando.Parameters.Add(new NpgsqlParameter("@mensagem", string.IsNullOrWhiteSpace(evento.Mensagem) ? DBNull.Value : SanitizarTexto(evento.Mensagem)));

        NpgsqlParameter pIp = new("@ip_origem", NpgsqlDbType.Inet)
        {
            Value = (object?)evento.IpOrigem ?? DBNull.Value
        };
        comando.Parameters.Add(pIp);

        comando.Parameters.Add(new NpgsqlParameter("@nome_maquina", string.IsNullOrWhiteSpace(evento.NomeMaquina) ? DBNull.Value : evento.NomeMaquina));
        comando.Parameters.Add(new NpgsqlParameter("@dados_contexto", SanitizarDadosContexto(evento.DadosContextoJson)));
        comando.Parameters.Add(ParametroLongoNulo("@criado_por", evento.CodigoUsuario));

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object SanitizarDadosContexto(string? dadosContextoJson)
    {
        if (string.IsNullOrWhiteSpace(dadosContextoJson))
        {
            return DBNull.Value;
        }

        try
        {
            JsonNode? raiz = JsonNode.Parse(dadosContextoJson);
            if (raiz is null)
            {
                return DBNull.Value;
            }

            SanitizarNoJson(raiz);
            return raiz.ToJsonString();
        }
        catch
        {
            return DBNull.Value;
        }
    }

    private static void SanitizarNoJson(JsonNode no)
    {
        if (no is JsonObject objeto)
        {
            foreach (string chave in objeto.Select(par => par.Key).ToArray())
            {
                if (ContemTermoSensivel(chave))
                {
                    objeto[chave] = "[REMOVIDO]";
                    continue;
                }

                JsonNode? filho = objeto[chave];
                if (filho is not null)
                {
                    SanitizarNoJson(filho);
                }
            }
        }
        else if (no is JsonArray lista)
        {
            foreach (JsonNode? item in lista)
            {
                if (item is not null)
                {
                    SanitizarNoJson(item);
                }
            }
        }
    }

    private static string SanitizarTexto(string texto)
    {
        return ContemTermoSensivel(texto)
            ? "Mensagem omitida por conter termo sensível."
            : texto;
    }

    private static bool ContemTermoSensivel(string valor)
    {
        return valor.Contains("senha", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("password", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("senha_hash", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("token", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("segredo", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("credential", StringComparison.OrdinalIgnoreCase);
    }
}
