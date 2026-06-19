namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Fonte UNICA das strings de permissao (modulo / rotina / acao), espelhando o seed do
/// banco (perfil_permissao). Use SEMPRE estas constantes em vez de strings soltas — foi a
/// string solta que gerou bugs como "SEGURANCA/PERFIL" (em vez de PERFIL_ACESSO) e
/// "CADASTRO/ETIQUETA" (em vez de ETIQUETA/ETIQUETA).
///
/// Os valores devem casar exatamente com o baseline de permissoes em
/// BancoDados/000_baseline e os incrementais (ex.: 010_..._campos_mapeamento).
/// </summary>
public static class PermissoesSistema
{
    public static class Modulos
    {
        public const string Inicio = "INICIO";
        public const string Cadastro = "CADASTRO";
        public const string Seguranca = "SEGURANCA";
        public const string ProcessoProducao = "PROCESSO_PRODUCAO";
        public const string Etiqueta = "ETIQUETA";
        public const string Relatorio = "RELATORIO";
        public const string Historico = "HISTORICO";
        public const string IntegracaoSap = "INTEGRACAO_SAP";
        public const string Configuracao = "CONFIGURACAO";
    }

    public static class Rotinas
    {
        public const string Dashboard = "DASHBOARD";
        public const string Setor = "SETOR";
        public const string Cargo = "CARGO";
        public const string Usuario = "USUARIO";
        public const string PerfilAcesso = "PERFIL_ACESSO";
        public const string Permissao = "PERMISSAO";
        public const string Balanca = "BALANCA";
        public const string Tara = "TARA";
        public const string TipoTara = "TIPO_TARA";
        public const string LeituraProducao = "LEITURA_PRODUCAO";
        public const string EntradaProduto = "ENTRADA_PRODUTO";
        public const string OrdemAndamento = "ORDEM_ANDAMENTO";
        public const string Etiqueta = "ETIQUETA";
        public const string ModeloEtiqueta = "MODELO_ETIQUETA";
        public const string ImpressaoEtiqueta = "IMPRESSAO_ETIQUETA";
        public const string CampoEtiqueta = "CAMPO_ETIQUETA";
        public const string MapeamentoCampoEtiqueta = "MAPEAMENTO_CAMPO_ETIQUETA";
        public const string Movimentacao = "MOVIMENTACAO";
        public const string Auditoria = "AUDITORIA";
        public const string CacheSap = "CACHE_SAP";
        public const string Sincronizacao = "SINCRONIZACAO";
        public const string MonitoramentoSap = "MONITORAMENTO";
        public const string ProdutoReferencia = "PRODUTO_REFERENCIA";
        public const string Geral = "GERAL";
        public const string ParametroOperacao = "PARAMETRO_OPERACAO";

        // Rotinas do modulo RELATORIO (os valores colidem de proposito com nomes de modulos).
        public const string RelatorioProducao = "PRODUCAO";
        public const string RelatorioCadastro = "CADASTRO";
        public const string RelatorioSap = "SAP";
    }

    public static class Acoes
    {
        public const string Consultar = "CONSULTAR";
        public const string Criar = "CRIAR";
        public const string Editar = "EDITAR";
        public const string Excluir = "EXCLUIR";
        public const string Gerenciar = "GERENCIAR";
        public const string Executar = "EXECUTAR";
        public const string Cancelar = "CANCELAR";
        public const string Finalizar = "FINALIZAR";
        public const string Imprimir = "IMPRIMIR";
        public const string Reimprimir = "REIMPRIMIR";
        public const string PesoManual = "PESO_MANUAL";
        public const string SincronizarCache = "SINCRONIZAR_CACHE";
        public const string EnviarSap = "ENVIAR_SAP";
        public const string Visualizar = "VISUALIZAR";
        public const string Exportar = "EXPORTAR";
        public const string Liberar = "LIBERAR";
        public const string Sincronizar = "SINCRONIZAR";
        public const string Bloquear = "BLOQUEAR";
        public const string Desbloquear = "DESBLOQUEAR";
    }
}
