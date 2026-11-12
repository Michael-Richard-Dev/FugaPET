using System.Text.Encodings.Web;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ProdutoAcabadoPaletePayloadBuilder
{
    /// <summary>GATE 126V: precisao do contrato SAP de peso, identica a usada no 261.</summary>
    public const int CasasDecimaisContratoSap = ResolvedorTaraFisicaEmbalagemPalete.CasasDecimaisContratoSap;

    /// <summary>
    /// GATE 126V: monta o payload INT012 da HU PAI compondo a tara FISICA do material de embalagem
    /// EXCLUSIVAMENTE no outbound.
    /// <para>
    /// OutboundGross = LocalGross + TaraFisica / OutboundNet = LocalNet (INTOCADO) /
    /// OutboundTare = LocalTare + TaraFisica (TARE_WEIGHT_SEMANTICS=TOTAL_PARENT_TARE: a tara das caixas
    /// permanece somada, nunca e substituida).
    /// </para>
    /// <para>
    /// A composicao ocorre EXATAMENTE UMA VEZ, aqui, e NAO e persistida: o palete local permanece com os
    /// pesos de rascunho. Por isso reload, preview repetido e reconciliacao futura nunca acumulam tara.
    /// </para>
    /// <para>
    /// FAIL-CLOSED: tara nao resolvida (ausente, invalida ou ambigua) ⇒ preview sem sucesso e SEM payload.
    /// Nao existe fallback para os pesos locais quando a tara fisica e exigida.
    /// </para>
    /// </summary>
    public ResultadoPreviewProdutoAcabadoPalete MontarPreview(
        ProdutoAcabadoPalete palete,
        ResultadoTaraFisicaEmbalagem taraFisicaEmbalagem)
    {
        ArgumentNullException.ThrowIfNull(palete);
        ArgumentNullException.ThrowIfNull(taraFisicaEmbalagem);

        if (palete.PrimeiraCaixa > palete.UltimaCaixa)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Intervalo de caixas invÃ¡lido.");
        }

        if (palete.Caixas.Count == 0)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Nenhuma caixa informada para o palete.");
        }

        if (palete.PesoBrutoKg <= palete.PesoLiquidoKg || palete.PesoLiquidoKg <= 0m || palete.TaraKg < 0m)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Pesos do palete invÃ¡lidos.");
        }

        // Â§12/Â§19: PackagingMaterial real Ã© obrigatÃ³rio (nunca PALLET01 hardcoded). Ausente â‡’ DEPENDENCIA_ARES / fail-closed.
        if (string.IsNullOrWhiteSpace(palete.PackagingMaterial))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Material de embalagem do palete nÃ£o informado (DEPENDENCIA_ARES: PackagingMaterial real).");
        }

        // Â§10/Â§11/Â§18: SOMENTE caixa CONFIRMADA_SAP entra no palete.
        if (palete.Caixas.Any(caixa => caixa.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Todas as caixas do palete precisam estar CONFIRMADA_SAP.");
        }

        // Â§10 (problema 2): HU SAP individual OBRIGATÃ“RIA. SEM fallback para CodigoCaixaLocal â€” HU ausente â‡’ fail-closed.
        if (palete.Caixas.Any(caixa => string.IsNullOrWhiteSpace(caixa.HandlingUnitExternalId)))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Caixa sem HU SAP individual: palete bloqueado (o payload sÃ³ aceita HUs SAP reais).");
        }

        // GATE 126V: a tara fisica e resolvida ANTES da construcao do DTO. Desconhecida ⇒ bloqueia aqui,
        // sem payload enviavel. As validacoes acima continuam avaliando os pesos LOCAIS, nao os compostos.
        if (!taraFisicaEmbalagem.Disponivel)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Tara fisica do material de embalagem do palete nao disponivel: "
                + taraFisicaEmbalagem.Mensagem);
        }

        decimal taraFisicaKg = taraFisicaEmbalagem.PesoKg!.Value;
        decimal pesoLiquidoOutbound = palete.PesoLiquidoKg;
        decimal taraOutbound = Arredondar(palete.TaraKg + taraFisicaKg);
        decimal pesoBrutoOutbound = Arredondar(palete.PesoBrutoKg + taraFisicaKg);

        // Invariante 1: GROSS = NET + TARE (apos a composicao).
        if (pesoBrutoOutbound != Arredondar(pesoLiquidoOutbound + taraOutbound))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Pesos compostos do palete inconsistentes: GROSS diferente de NET + TARE.");
        }

        // Invariante 2: GROSS > NET > TARE.
        if (pesoBrutoOutbound <= pesoLiquidoOutbound || pesoLiquidoOutbound <= taraOutbound)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Pesos compostos do palete inconsistentes: exigido GROSS > NET > TARE.");
        }

        ProdutoAcabadoPaleteRequest payload = new()
        {
            HandlingUnitExternalID = "$1", // GATE 046-K: contrato INT012 exige literal "$1"; CodigoPaleteLocal NAO vai neste campo
            GrossWeight = pesoBrutoOutbound,
            NetWeight = pesoLiquidoOutbound,
            TareWeight = taraOutbound,
            WeightUnit = "KG",
            Plant = palete.Plant,
            StorageLocation = palete.StorageLocation,
            PackagingMaterial = palete.PackagingMaterial,
            // Â§18/Â§27: _HandlingUnitItem contÃ©m EXATAMENTE as HUs SAP das caixas (HandlingUnitExternalId), sem fallback.
            HandlingUnitItems = palete.Caixas
                .Select(caixa => new ProdutoAcabadoPaleteItemRequest { HandlingUnit = caixa.HandlingUnitExternalId! })
                .ToArray()
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        return ResultadoPreviewProdutoAcabadoPalete.Ok(payload, json);
    }

    private static decimal Arredondar(decimal valor)
        => Math.Round(valor, CasasDecimaisContratoSap, MidpointRounding.AwayFromZero);
}

