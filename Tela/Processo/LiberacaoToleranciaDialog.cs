using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// Pede a credencial de quem pode liberar um estouro de tolerancia no Recebimento.
/// <para>
/// O operador continua logado: este dialogo nao troca a sessao, so colhe a credencial de quem
/// autoriza. Montado EM CODIGO, com tamanhos derivados do DPI — sem pixel cravado, que e o que faz
/// tela quebrar em maquina de outra resolucao.
/// </para>
/// <para>
/// A SENHA NUNCA SAI DAQUI em texto para log, arquivo ou mensagem. Ela vai direto para a
/// autenticacao, e o que se registra depois e a IDENTIDADE de quem autorizou.
/// </para>
/// </summary>
public sealed class LiberacaoToleranciaDialog : Form
{
    private readonly TextBox _usuario = new();
    private readonly TextBox _senha = new();
    private readonly Label _erro = new();
    private readonly Button _liberar = new();

    /// <summary>
    /// A senha CONFIRMADA, capturada no fechamento — não lida do controle.
    /// <para>
    /// É assim por um defeito real: <c>Liberar</c> tem <c>DialogResult.OK</c>, então clicar nele
    /// FECHA o diálogo, e o fechamento dispara <c>OnFormClosed</c> ANTES de <c>ShowDialog</c>
    /// retornar. Como ali a senha era apagada por higiene, quem lia <c>Senha</c> depois do
    /// <c>ShowDialog</c> recebia sempre vazio e levava "Informe usuário e senha" — com a senha
    /// corretamente digitada. A liberação nunca funcionou até 24/11/2026.
    /// </para>
    /// </summary>
    private string _senhaConfirmada = string.Empty;

    public string Usuario => _usuario.Text.Trim();
    public string Senha => _senhaConfirmada;

    public LiberacaoToleranciaDialog(string mensagemDoEstouro)
    {
        Text = "Liberação de tolerância";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(Escalar(520), Escalar(300));

        MontarLayout(mensagemDoEstouro);
    }

    private float Fator => DeviceDpi / 96f;

    private int Escalar(int valor) => (int)Math.Round(valor * Fator);

    private void MontarLayout(string mensagemDoEstouro)
    {
        TableLayoutPanel raiz = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(Escalar(16)),
        };

        Label titulo = new()
        {
            Text = "Esta pesagem está fora da tolerância do material.",
            AutoSize = true,
            MaximumSize = new Size(Escalar(480), 0),
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, Escalar(6)),
        };

        // Os numeros do estouro ficam a vista enquanto o gerente decide. Pedir autorizacao sem
        // mostrar o que esta sendo autorizado transformaria a liberacao em formalidade.
        Label detalhe = new()
        {
            Text = mensagemDoEstouro,
            AutoSize = true,
            MaximumSize = new Size(Escalar(480), 0),
            ForeColor = Color.FromArgb(0x6B, 0x72, 0x80),
            Margin = new Padding(0, 0, 0, Escalar(12)),
        };

        Label instrucao = new()
        {
            Text = "Para concluir, um usuário com permissão de liberação precisa informar a própria "
                 + "credencial. A liberação vale somente para esta pesagem e fica registrada.",
            AutoSize = true,
            MaximumSize = new Size(Escalar(480), 0),
            Margin = new Padding(0, 0, 0, Escalar(12)),
        };

        TableLayoutPanel campos = new()
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, Escalar(8)),
        };
        campos.Controls.Add(new Label { Text = "Usuário:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _usuario.Width = Escalar(320);
        // Mesma caixa alta do login (23/11/2026): a busca compara com lower() dos dois lados, entao
        // forcar maiuscula nao tranca quem esta cadastrado em minusculas.
        _usuario.CharacterCasing = CharacterCasing.Upper;
        campos.Controls.Add(_usuario, 1, 0);

        campos.Controls.Add(new Label { Text = "Senha:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _senha.Width = Escalar(320);
        _senha.UseSystemPasswordChar = true;
        campos.Controls.Add(_senha, 1, 1);

        _erro.AutoSize = true;
        _erro.MaximumSize = new Size(Escalar(480), 0);
        _erro.ForeColor = Color.FromArgb(0xB4, 0x50, 0x09);
        _erro.Margin = new Padding(0, 0, 0, Escalar(8));

        FlowLayoutPanel acoes = new()
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };

        Button cancelar = new() { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        _liberar.Text = "Liberar";
        _liberar.AutoSize = true;
        _liberar.DialogResult = DialogResult.OK;
        acoes.Controls.AddRange([_liberar, cancelar]);

        raiz.Controls.Add(titulo, 0, 0);
        raiz.Controls.Add(detalhe, 0, 1);
        raiz.Controls.Add(instrucao, 0, 2);
        raiz.Controls.Add(campos, 0, 3);
        raiz.Controls.Add(_erro, 0, 4);
        raiz.Controls.Add(acoes, 0, 5);

        Controls.Add(raiz);
        AcceptButton = _liberar;
        CancelButton = cancelar;
    }

    /// <summary>Mostra o motivo da recusa e deixa o diálogo aberto para nova tentativa.</summary>
    public void MostrarErro(string mensagem)
    {
        _erro.Text = mensagem;
        _senhaConfirmada = string.Empty;
        _senha.Clear();
        _senha.Focus();
    }

    /// <summary>
    /// CAPTURA a senha antes de limpar o controle. A ordem importa: é o fechamento que entrega a
    /// credencial a quem chamou, e limpar sem capturar foi o que deixou a liberação sempre vazia.
    /// <para>
    /// Limpar o controle não é proteção forte — a string já existe no heap —, mas evita que ela
    /// fique viva num controle enquanto o formulário não é coletado.
    /// </para>
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _senhaConfirmada = DialogResult == DialogResult.OK ? _senha.Text : string.Empty;
        _senha.Clear();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Agora sim, quando ninguem mais vai ler.
            _senhaConfirmada = string.Empty;
        }

        base.Dispose(disposing);
    }

    /// <summary>Texto do estouro para o diálogo, a partir do veredito da regra.</summary>
    public static string DescreverEstouro(string material, VereditoToleranciaTexto veredito)
        => $"Material {material} · pedido {veredito.Pedido} · pesado {veredito.Apurado} · "
         + $"limite {veredito.Limite} (tolerância de {veredito.Percentual}).";
}

/// <summary>Os números do estouro já formatados, para o diálogo não depender da regra.</summary>
public sealed record VereditoToleranciaTexto(string Pedido, string Apurado, string Limite, string Percentual);

/// <summary>
/// Recusa de pesagem por tolerância, para os caminhos que só conseguem recusar lançando.
/// <para>
/// É o caso da janela de pesagens (<c>PesagemMultiplaItemForm</c>): o callback de registro devolve
/// uma pesagem, e não um "não". A janela trata exceção do callback como recusa e publica a mensagem,
/// então é por aí que a tolerância diz não — sem inventar uma pesagem inválida só para recusá-la
/// depois, que deixaria estado sujo no meio do caminho.
/// </para>
/// </summary>
public sealed class PesagemBloqueadaPorToleranciaException : InvalidOperationException
{
    public PesagemBloqueadaPorToleranciaException(string mensagem) : base(mensagem) { }

    public PesagemBloqueadaPorToleranciaException() : base("Pesagem bloqueada pela tolerância do material.") { }

    public PesagemBloqueadaPorToleranciaException(string mensagem, Exception innerException)
        : base(mensagem, innerException) { }
}
