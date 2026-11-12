using System.Globalization;
using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal static class MapeadorDatasProdutoSapR8C
{
    internal static DatasProdutoSqlR8C? Mapear(CamposProdutoSapR8B campos, bool offsetComprovadoPeloBackend)
    {
        DateOnly? data = null;
        if (campos.LastChangeDateOriginal is not null)
        {
            if (!DateOnly.TryParseExact(campos.LastChangeDateOriginal, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly civil)) return null;
            data = civil;
        }
        DateTime? utc = null;
        if (offsetComprovadoPeloBackend)
        {
            if (campos.LastChangeDateTimeOriginal is not string raw) return null;
            Match formato = Regex.Match(raw,
                @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?<offset>Z|[+-][0-9]{2}:[0-9]{2})$",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!formato.Success || formato.Groups["offset"].Value != campos.OffsetLastChangeDateTimeOriginal
                || !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset instante))
                return null;
            utc = instante.UtcDateTime;
        }
        return new(campos.LastChangeDateTimeOriginal, utc, offsetComprovadoPeloBackend, data);
    }
}
