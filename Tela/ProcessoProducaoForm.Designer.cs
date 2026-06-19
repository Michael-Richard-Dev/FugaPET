using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

partial class ProcessoProducaoForm
{
    private System.ComponentModel.IContainer components = null;
    private Panel contentPanel;
    private Label sectionTitleLabel;
    private RoundedPanel processoProdutoAcabadoCard;
    private RoundedPanel processIconPanel;
    private Label processIconLabel;
    private Label processTitleLabel;
    private Label processDescriptionLabel;
    private Label processStatusLabel;
    private Label processShortcutLabel;
    private Label processArrowLabel;
    private RoundedPanel processoPesagemApontamentoCard;
    private RoundedPanel pesagemIconPanel;
    private Label pesagemIconLabel;
    private Label pesagemTitleLabel;
    private Label pesagemDescriptionLabel;
    private Label pesagemStatusLabel;
    private Label pesagemShortcutLabel;
    private Label pesagemArrowLabel;
    private RoundedPanel ordensAndamentoCard;
    private RoundedPanel ordensIconPanel;
    private Label ordensIconLabel;
    private Label ordensTitleLabel;
    private Label ordensDescriptionLabel;
    private Label ordensStatusLabel;
    private Label ordensShortcutLabel;
    private Label ordensArrowLabel;
    private RoundedPanel entradaProdutoCard;
    private RoundedPanel entradaIconPanel;
    private Label entradaIconLabel;
    private Label entradaTitleLabel;
    private Label entradaDescriptionLabel;
    private Label entradaStatusLabel;
    private Label entradaShortcutLabel;
    private Label entradaArrowLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        contentPanel = new Panel();
        sectionTitleLabel = new Label();
        processoProdutoAcabadoCard = new RoundedPanel();
        processIconPanel = new RoundedPanel();
        processIconLabel = new Label();
        processTitleLabel = new Label();
        processDescriptionLabel = new Label();
        processStatusLabel = new Label();
        processShortcutLabel = new Label();
        processArrowLabel = new Label();
        processoPesagemApontamentoCard = new RoundedPanel();
        pesagemIconPanel = new RoundedPanel();
        pesagemIconLabel = new Label();
        pesagemTitleLabel = new Label();
        pesagemDescriptionLabel = new Label();
        pesagemStatusLabel = new Label();
        pesagemShortcutLabel = new Label();
        pesagemArrowLabel = new Label();
        ordensAndamentoCard = new RoundedPanel();
        ordensIconPanel = new RoundedPanel();
        ordensIconLabel = new Label();
        ordensTitleLabel = new Label();
        ordensDescriptionLabel = new Label();
        ordensStatusLabel = new Label();
        ordensShortcutLabel = new Label();
        ordensArrowLabel = new Label();
        entradaProdutoCard = new RoundedPanel();
        entradaIconPanel = new RoundedPanel();
        entradaIconLabel = new Label();
        entradaTitleLabel = new Label();
        entradaDescriptionLabel = new Label();
        entradaStatusLabel = new Label();
        entradaShortcutLabel = new Label();
        entradaArrowLabel = new Label();
        contentPanel.SuspendLayout();
        processoProdutoAcabadoCard.SuspendLayout();
        processIconPanel.SuspendLayout();
        processoPesagemApontamentoCard.SuspendLayout();
        pesagemIconPanel.SuspendLayout();
        ordensAndamentoCard.SuspendLayout();
        ordensIconPanel.SuspendLayout();
        entradaProdutoCard.SuspendLayout();
        entradaIconPanel.SuspendLayout();
        SuspendLayout();
        // 
        // contentPanel
        // 
        contentPanel.BackColor = Color.FromArgb(247, 248, 250);
        contentPanel.Controls.Add(sectionTitleLabel);
        contentPanel.Controls.Add(entradaProdutoCard);
        contentPanel.Controls.Add(processoProdutoAcabadoCard);
        contentPanel.Controls.Add(processoPesagemApontamentoCard);
        contentPanel.Controls.Add(ordensAndamentoCard);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 0);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(28, 24, 28, 24);
        contentPanel.Size = new Size(900, 560);
        contentPanel.TabIndex = 0;
        // 
        // sectionTitleLabel
        // 
        sectionTitleLabel.BackColor = Color.Transparent;
        sectionTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sectionTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        sectionTitleLabel.Location = new Point(28, 24);
        sectionTitleLabel.Name = "sectionTitleLabel";
        sectionTitleLabel.Size = new Size(280, 28);
        sectionTitleLabel.TabIndex = 0;
        sectionTitleLabel.Text = "Módulos de Leitura";
        sectionTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // processoProdutoAcabadoCard
        // 
        processoProdutoAcabadoCard.BackColor = Color.Transparent;
        processoProdutoAcabadoCard.BorderColor = Color.FromArgb(226, 232, 240);
        processoProdutoAcabadoCard.Controls.Add(processIconPanel);
        processoProdutoAcabadoCard.Controls.Add(processTitleLabel);
        processoProdutoAcabadoCard.Controls.Add(processDescriptionLabel);
        processoProdutoAcabadoCard.Controls.Add(processStatusLabel);
        processoProdutoAcabadoCard.Controls.Add(processShortcutLabel);
        processoProdutoAcabadoCard.Controls.Add(processArrowLabel);
        processoProdutoAcabadoCard.Cursor = Cursors.Hand;
        processoProdutoAcabadoCard.Location = new Point(284, 70);
        processoProdutoAcabadoCard.Name = "processoProdutoAcabadoCard";
        processoProdutoAcabadoCard.ShadowBlur = 0;
        processoProdutoAcabadoCard.ShadowOffsetY = 0;
        processoProdutoAcabadoCard.Size = new Size(240, 250);
        processoProdutoAcabadoCard.TabIndex = 1;
        // 
        // processIconPanel
        // 
        processIconPanel.BackColor = Color.Transparent;
        processIconPanel.BorderRadius = 9;
        processIconPanel.Controls.Add(processIconLabel);
        processIconPanel.Cursor = Cursors.Hand;
        processIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        processIconPanel.Location = new Point(92, 20);
        processIconPanel.Name = "processIconPanel";
        processIconPanel.ShadowBlur = 0;
        processIconPanel.ShadowOffsetY = 0;
        processIconPanel.Size = new Size(56, 56);
        processIconPanel.TabIndex = 0;
        // 
        // processIconLabel
        // 
        processIconLabel.BackColor = Color.Transparent;
        processIconLabel.Cursor = Cursors.Hand;
        processIconLabel.Dock = DockStyle.Fill;
        processIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        processIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        processIconLabel.Image = (Image)Properties.Resources.ResourceManager.GetObject("producao_24x_red");
        processIconLabel.ImageAlign = ContentAlignment.MiddleCenter;
        processIconLabel.Location = new Point(0, 0);
        processIconLabel.Name = "processIconLabel";
        processIconLabel.Size = new Size(56, 56);
        processIconLabel.TabIndex = 0;
        processIconLabel.Text = "";
        processIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processTitleLabel
        // 
        processTitleLabel.BackColor = Color.Transparent;
        processTitleLabel.Cursor = Cursors.Hand;
        processTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        processTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        processTitleLabel.Location = new Point(20, 92);
        processTitleLabel.Name = "processTitleLabel";
        processTitleLabel.Size = new Size(202, 62);
        processTitleLabel.TabIndex = 1;
        processTitleLabel.Text = "Produto\r\nAcabado";
        processTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processDescriptionLabel
        // 
        processDescriptionLabel.BackColor = Color.Transparent;
        processDescriptionLabel.Cursor = Cursors.Hand;
        processDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        processDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        processDescriptionLabel.Location = new Point(20, 156);
        processDescriptionLabel.Name = "processDescriptionLabel";
        processDescriptionLabel.Size = new Size(175, 46);
        processDescriptionLabel.TabIndex = 2;
        processDescriptionLabel.Text = "Leitura e controle do\r\nproduto acabado.";
        processDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processStatusLabel
        // 
        processStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        processStatusLabel.Cursor = Cursors.Hand;
        processStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        processStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        processStatusLabel.Location = new Point(20, 214);
        processStatusLabel.Name = "processStatusLabel";
        processStatusLabel.Size = new Size(82, 28);
        processStatusLabel.TabIndex = 3;
        processStatusLabel.Text = "Disponível";
        processStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processShortcutLabel
        // 
        processShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        processShortcutLabel.Cursor = Cursors.Hand;
        processShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        processShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        processShortcutLabel.Location = new Point(110, 214);
        processShortcutLabel.Name = "processShortcutLabel";
        processShortcutLabel.Size = new Size(38, 28);
        processShortcutLabel.TabIndex = 4;
        processShortcutLabel.Text = "F2";
        processShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processArrowLabel
        // 
        processArrowLabel.BackColor = Color.Transparent;
        processArrowLabel.Cursor = Cursors.Hand;
        processArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        processArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        processArrowLabel.Location = new Point(190, 207);
        processArrowLabel.Name = "processArrowLabel";
        processArrowLabel.Size = new Size(32, 36);
        processArrowLabel.TabIndex = 5;
        processArrowLabel.Text = "→";
        processArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // processoPesagemApontamentoCard
        // 
        processoPesagemApontamentoCard.BackColor = Color.Transparent;
        processoPesagemApontamentoCard.BorderColor = Color.FromArgb(226, 232, 240);
        processoPesagemApontamentoCard.Controls.Add(pesagemIconPanel);
        processoPesagemApontamentoCard.Controls.Add(pesagemTitleLabel);
        processoPesagemApontamentoCard.Controls.Add(pesagemDescriptionLabel);
        processoPesagemApontamentoCard.Controls.Add(pesagemStatusLabel);
        processoPesagemApontamentoCard.Controls.Add(pesagemShortcutLabel);
        processoPesagemApontamentoCard.Controls.Add(pesagemArrowLabel);
        processoPesagemApontamentoCard.Cursor = Cursors.Hand;
        processoPesagemApontamentoCard.Location = new Point(796, 70);
        processoPesagemApontamentoCard.Name = "processoPesagemApontamentoCard";
        processoPesagemApontamentoCard.ShadowBlur = 0;
        processoPesagemApontamentoCard.ShadowOffsetY = 0;
        processoPesagemApontamentoCard.Size = new Size(240, 250);
        processoPesagemApontamentoCard.TabIndex = 2;
        // 
        // pesagemIconPanel
        // 
        pesagemIconPanel.BackColor = Color.Transparent;
        pesagemIconPanel.BorderRadius = 9;
        pesagemIconPanel.Controls.Add(pesagemIconLabel);
        pesagemIconPanel.Cursor = Cursors.Hand;
        pesagemIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        pesagemIconPanel.Location = new Point(92, 20);
        pesagemIconPanel.Name = "pesagemIconPanel";
        pesagemIconPanel.ShadowBlur = 0;
        pesagemIconPanel.ShadowOffsetY = 0;
        pesagemIconPanel.Size = new Size(56, 56);
        pesagemIconPanel.TabIndex = 0;
        // 
        // pesagemIconLabel
        // 
        pesagemIconLabel.BackColor = Color.Transparent;
        pesagemIconLabel.Cursor = Cursors.Hand;
        pesagemIconLabel.Dock = DockStyle.Fill;
        pesagemIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        pesagemIconLabel.ForeColor = Color.FromArgb(37, 99, 235);
        pesagemIconLabel.Image = (Image)Properties.Resources.ResourceManager.GetObject("producao_24x_red");
        pesagemIconLabel.ImageAlign = ContentAlignment.MiddleCenter;
        pesagemIconLabel.Location = new Point(0, 0);
        pesagemIconLabel.Name = "pesagemIconLabel";
        pesagemIconLabel.Size = new Size(56, 56);
        pesagemIconLabel.TabIndex = 0;
        pesagemIconLabel.Text = "";
        pesagemIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pesagemTitleLabel
        // 
        pesagemTitleLabel.BackColor = Color.Transparent;
        pesagemTitleLabel.Cursor = Cursors.Hand;
        pesagemTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        pesagemTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        pesagemTitleLabel.Location = new Point(20, 92);
        pesagemTitleLabel.Name = "pesagemTitleLabel";
        pesagemTitleLabel.Size = new Size(202, 62);
        pesagemTitleLabel.TabIndex = 1;
        pesagemTitleLabel.Text = "Pesagem\r\nApontamento";
        pesagemTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pesagemDescriptionLabel
        // 
        pesagemDescriptionLabel.BackColor = Color.Transparent;
        pesagemDescriptionLabel.Cursor = Cursors.Hand;
        pesagemDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        pesagemDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        pesagemDescriptionLabel.Location = new Point(20, 156);
        pesagemDescriptionLabel.Name = "pesagemDescriptionLabel";
        pesagemDescriptionLabel.Size = new Size(175, 46);
        pesagemDescriptionLabel.TabIndex = 2;
        pesagemDescriptionLabel.Text = "Leitura e apontamento da\r\npesagem operacional.";
        pesagemDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pesagemStatusLabel
        // 
        pesagemStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        pesagemStatusLabel.Cursor = Cursors.Hand;
        pesagemStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        pesagemStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        pesagemStatusLabel.Location = new Point(20, 214);
        pesagemStatusLabel.Name = "pesagemStatusLabel";
        pesagemStatusLabel.Size = new Size(82, 28);
        pesagemStatusLabel.TabIndex = 3;
        pesagemStatusLabel.Text = "Disponível";
        pesagemStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pesagemShortcutLabel
        // 
        pesagemShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        pesagemShortcutLabel.Cursor = Cursors.Hand;
        pesagemShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        pesagemShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        pesagemShortcutLabel.Location = new Point(110, 214);
        pesagemShortcutLabel.Name = "pesagemShortcutLabel";
        pesagemShortcutLabel.Size = new Size(38, 28);
        pesagemShortcutLabel.TabIndex = 4;
        pesagemShortcutLabel.Text = "F4";
        pesagemShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // pesagemArrowLabel
        // 
        pesagemArrowLabel.BackColor = Color.Transparent;
        pesagemArrowLabel.Cursor = Cursors.Hand;
        pesagemArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        pesagemArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        pesagemArrowLabel.Location = new Point(190, 207);
        pesagemArrowLabel.Name = "pesagemArrowLabel";
        pesagemArrowLabel.Size = new Size(32, 36);
        pesagemArrowLabel.TabIndex = 5;
        pesagemArrowLabel.Text = "→";
        pesagemArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensAndamentoCard
        //
        ordensAndamentoCard.BackColor = Color.Transparent;
        ordensAndamentoCard.BorderColor = Color.FromArgb(226, 232, 240);
        ordensAndamentoCard.Controls.Add(ordensIconPanel);
        ordensAndamentoCard.Controls.Add(ordensTitleLabel);
        ordensAndamentoCard.Controls.Add(ordensDescriptionLabel);
        ordensAndamentoCard.Controls.Add(ordensStatusLabel);
        ordensAndamentoCard.Controls.Add(ordensShortcutLabel);
        ordensAndamentoCard.Controls.Add(ordensArrowLabel);
        ordensAndamentoCard.Cursor = Cursors.Hand;
        ordensAndamentoCard.Location = new Point(540, 70);
        ordensAndamentoCard.Name = "ordensAndamentoCard";
        ordensAndamentoCard.ShadowBlur = 0;
        ordensAndamentoCard.ShadowOffsetY = 0;
        ordensAndamentoCard.Size = new Size(240, 250);
        ordensAndamentoCard.TabIndex = 3;
        //
        // ordensIconPanel
        //
        ordensIconPanel.BackColor = Color.Transparent;
        ordensIconPanel.BorderRadius = 9;
        ordensIconPanel.Controls.Add(ordensIconLabel);
        ordensIconPanel.Cursor = Cursors.Hand;
        ordensIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        ordensIconPanel.Location = new Point(92, 20);
        ordensIconPanel.Name = "ordensIconPanel";
        ordensIconPanel.ShadowBlur = 0;
        ordensIconPanel.ShadowOffsetY = 0;
        ordensIconPanel.Size = new Size(56, 56);
        ordensIconPanel.TabIndex = 0;
        //
        // ordensIconLabel
        //
        ordensIconLabel.BackColor = Color.Transparent;
        ordensIconLabel.Cursor = Cursors.Hand;
        ordensIconLabel.Dock = DockStyle.Fill;
        ordensIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ordensIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        ordensIconLabel.ImageAlign = ContentAlignment.MiddleCenter;
        ordensIconLabel.Location = new Point(0, 0);
        ordensIconLabel.Name = "ordensIconLabel";
        ordensIconLabel.Size = new Size(56, 56);
        ordensIconLabel.TabIndex = 0;
        ordensIconLabel.Text = "";
        ordensIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensTitleLabel
        //
        ordensTitleLabel.BackColor = Color.Transparent;
        ordensTitleLabel.Cursor = Cursors.Hand;
        ordensTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ordensTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        ordensTitleLabel.Location = new Point(20, 92);
        ordensTitleLabel.Name = "ordensTitleLabel";
        ordensTitleLabel.Size = new Size(202, 62);
        ordensTitleLabel.TabIndex = 1;
        ordensTitleLabel.Text = "Ordens em\r\nAndamento";
        ordensTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensDescriptionLabel
        //
        ordensDescriptionLabel.BackColor = Color.Transparent;
        ordensDescriptionLabel.Cursor = Cursors.Hand;
        ordensDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ordensDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        ordensDescriptionLabel.Location = new Point(20, 156);
        ordensDescriptionLabel.Name = "ordensDescriptionLabel";
        ordensDescriptionLabel.Size = new Size(175, 46);
        ordensDescriptionLabel.TabIndex = 2;
        ordensDescriptionLabel.Text = "Consulta das ordens em\r\nexecução / Integração SAP.";
        ordensDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensStatusLabel
        //
        ordensStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        ordensStatusLabel.Cursor = Cursors.Hand;
        ordensStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ordensStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        ordensStatusLabel.Location = new Point(20, 214);
        ordensStatusLabel.Name = "ordensStatusLabel";
        ordensStatusLabel.Size = new Size(82, 28);
        ordensStatusLabel.TabIndex = 3;
        ordensStatusLabel.Text = "Disponível";
        ordensStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensShortcutLabel
        //
        ordensShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        ordensShortcutLabel.Cursor = Cursors.Hand;
        ordensShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ordensShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        ordensShortcutLabel.Location = new Point(110, 214);
        ordensShortcutLabel.Name = "ordensShortcutLabel";
        ordensShortcutLabel.Size = new Size(38, 28);
        ordensShortcutLabel.TabIndex = 4;
        ordensShortcutLabel.Text = "F3";
        ordensShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ordensArrowLabel
        //
        ordensArrowLabel.BackColor = Color.Transparent;
        ordensArrowLabel.Cursor = Cursors.Hand;
        ordensArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        ordensArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        ordensArrowLabel.Location = new Point(190, 207);
        ordensArrowLabel.Name = "ordensArrowLabel";
        ordensArrowLabel.Size = new Size(32, 36);
        ordensArrowLabel.TabIndex = 5;
        ordensArrowLabel.Text = "→";
        ordensArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaProdutoCard
        //
        entradaProdutoCard.BackColor = Color.Transparent;
        entradaProdutoCard.BorderColor = Color.FromArgb(226, 232, 240);
        entradaProdutoCard.Controls.Add(entradaIconPanel);
        entradaProdutoCard.Controls.Add(entradaTitleLabel);
        entradaProdutoCard.Controls.Add(entradaDescriptionLabel);
        entradaProdutoCard.Controls.Add(entradaStatusLabel);
        entradaProdutoCard.Controls.Add(entradaShortcutLabel);
        entradaProdutoCard.Controls.Add(entradaArrowLabel);
        entradaProdutoCard.Cursor = Cursors.Hand;
        entradaProdutoCard.Location = new Point(28, 70);
        entradaProdutoCard.Name = "entradaProdutoCard";
        entradaProdutoCard.ShadowBlur = 0;
        entradaProdutoCard.ShadowOffsetY = 0;
        entradaProdutoCard.Size = new Size(240, 250);
        entradaProdutoCard.TabIndex = 4;
        //
        // entradaIconPanel
        //
        entradaIconPanel.BackColor = Color.Transparent;
        entradaIconPanel.BorderRadius = 9;
        entradaIconPanel.Controls.Add(entradaIconLabel);
        entradaIconPanel.Cursor = Cursors.Hand;
        entradaIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        entradaIconPanel.Location = new Point(92, 20);
        entradaIconPanel.Name = "entradaIconPanel";
        entradaIconPanel.ShadowBlur = 0;
        entradaIconPanel.ShadowOffsetY = 0;
        entradaIconPanel.Size = new Size(56, 56);
        entradaIconPanel.TabIndex = 0;
        //
        // entradaIconLabel
        //
        entradaIconLabel.BackColor = Color.Transparent;
        entradaIconLabel.Cursor = Cursors.Hand;
        entradaIconLabel.Dock = DockStyle.Fill;
        entradaIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        entradaIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        entradaIconLabel.ImageAlign = ContentAlignment.MiddleCenter;
        entradaIconLabel.Location = new Point(0, 0);
        entradaIconLabel.Name = "entradaIconLabel";
        entradaIconLabel.Size = new Size(56, 56);
        entradaIconLabel.TabIndex = 0;
        entradaIconLabel.Text = "";
        entradaIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaTitleLabel
        //
        entradaTitleLabel.BackColor = Color.Transparent;
        entradaTitleLabel.Cursor = Cursors.Hand;
        entradaTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        entradaTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        entradaTitleLabel.Location = new Point(20, 92);
        entradaTitleLabel.Name = "entradaTitleLabel";
        entradaTitleLabel.Size = new Size(202, 62);
        entradaTitleLabel.TabIndex = 1;
        entradaTitleLabel.Text = "Entrada de\r\nProduto";
        entradaTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaDescriptionLabel
        //
        entradaDescriptionLabel.BackColor = Color.Transparent;
        entradaDescriptionLabel.Cursor = Cursors.Hand;
        entradaDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        entradaDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        entradaDescriptionLabel.Location = new Point(20, 156);
        entradaDescriptionLabel.Name = "entradaDescriptionLabel";
        entradaDescriptionLabel.Size = new Size(175, 46);
        entradaDescriptionLabel.TabIndex = 2;
        entradaDescriptionLabel.Text = "Entrada de produto via\r\npedido de compra / SAP.";
        entradaDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaStatusLabel
        //
        entradaStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        entradaStatusLabel.Cursor = Cursors.Hand;
        entradaStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        entradaStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        entradaStatusLabel.Location = new Point(20, 214);
        entradaStatusLabel.Name = "entradaStatusLabel";
        entradaStatusLabel.Size = new Size(82, 28);
        entradaStatusLabel.TabIndex = 3;
        entradaStatusLabel.Text = "Disponível";
        entradaStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaShortcutLabel
        //
        entradaShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        entradaShortcutLabel.Cursor = Cursors.Hand;
        entradaShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        entradaShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        entradaShortcutLabel.Location = new Point(110, 214);
        entradaShortcutLabel.Name = "entradaShortcutLabel";
        entradaShortcutLabel.Size = new Size(38, 28);
        entradaShortcutLabel.TabIndex = 4;
        entradaShortcutLabel.Text = "F1";
        entradaShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // entradaArrowLabel
        //
        entradaArrowLabel.BackColor = Color.Transparent;
        entradaArrowLabel.Cursor = Cursors.Hand;
        entradaArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        entradaArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        entradaArrowLabel.Location = new Point(190, 207);
        entradaArrowLabel.Name = "entradaArrowLabel";
        entradaArrowLabel.Size = new Size(32, 36);
        entradaArrowLabel.TabIndex = 5;
        entradaArrowLabel.Text = "→";
        entradaArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // ProcessoProducaoForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        Controls.Add(contentPanel);
        Name = "ProcessoProducaoForm";
        Size = new Size(900, 560);
        contentPanel.ResumeLayout(false);
        processoProdutoAcabadoCard.ResumeLayout(false);
        processIconPanel.ResumeLayout(false);
        processoPesagemApontamentoCard.ResumeLayout(false);
        pesagemIconPanel.ResumeLayout(false);
        ordensAndamentoCard.ResumeLayout(false);
        ordensIconPanel.ResumeLayout(false);
        entradaProdutoCard.ResumeLayout(false);
        entradaIconPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}





