using System.ComponentModel;
using System.Diagnostics;
using SteamBibliotekRydder.Models;
using SteamBibliotekRydder.Services;

namespace SteamBibliotekRydder;

public sealed class MainForm : Form
{
    private readonly SteamLocator _locator = new();
    private readonly SteamLibraryScanner _scanner;
    private readonly CollectionCategorizer _categorizer = new();
    private readonly SteamCollectionWriter _writer;

    private readonly Label _accountLabel = new();
    private readonly Label _pathLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _gameCountValue = CreateValueLabel();
    private readonly Label _installedCountValue = CreateValueLabel();
    private readonly Label _playedCountValue = CreateValueLabel();
    private readonly Label _collectionCountValue = CreateValueLabel();
    private readonly Label _selectedCountLabel = new();
    private readonly Label _visibleGamesLabel = new();
    private readonly Label _categoryDescription = new();
    private readonly Label _statusLabel = new();
    private readonly Label _statusDot = new();
    private readonly Label _connectionLabel = new();
    private readonly Label _connectionDot = new();
    private readonly ModernCheckedListBox _categoryList = new();
    private readonly SmoothDataGridView _gameGrid = new();
    private readonly TextBox _searchBox = new();
    private readonly ToggleSwitch _replaceCollectionsCheck = new();
    private readonly ModernButton _scanButton = new();
    private readonly ModernButton _writeButton = new();
    private readonly ModernButton _backupButton = new();
    private readonly ModernButton _selectAllButton = new();
    private readonly ModernButton _selectNoneButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly ToolTip _toolTip = new();

    private ScanResult? _scanResult;
    private IReadOnlyList<CategorySuggestion> _suggestions = [];
    private IReadOnlyList<GameRow> _allRows = [];

    public MainForm()
    {
        _scanner = new SteamLibraryScanner(_locator);
        _writer = new SteamCollectionWriter(_locator);

        Text = "Steam Bibliotek Rydder";
        BackColor = AppPalette.BackgroundBottom;
        ForeColor = AppPalette.Text;
        MinimumSize = new Size(1120, 740);
        Size = new Size(1360, 880);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        BuildInterface();
        HandleCreated += (_, _) => WindowsTheme.ApplyDarkTitleBar(this);
        Shown += async (_, _) => await ScanAsync();
    }

    private void BuildInterface()
    {
        var background = new GradientBackgroundPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 20)
        };
        Controls.Add(background);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        background.Controls.Add(root);

        var header = BuildHeader();
        header.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(header, 0, 0);

        var overview = BuildOverview();
        overview.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(overview, 0, 1);

        var content = BuildContent();
        content.Margin = Padding.Empty;
        root.Controls.Add(content, 0, 2);

        var actions = BuildActions();
        actions.Margin = new Padding(0, 12, 0, 0);
        root.Controls.Add(actions, 0, 3);
    }

    private Control BuildHeader()
    {
        var panel = CreateCard(
            AppPalette.SurfaceRaised,
            AppPalette.Surface,
            Color.FromArgb(45, 71, 99),
            22);
        panel.Padding = new Padding(20, 13, 20, 13);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var mark = new SteamMark
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        layout.Controls.Add(mark, 0, 0);

        var titleStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        titleStack.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Steam Bibliotek Rydder",
            Font = new Font("Segoe UI Semibold", 21f),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        titleStack.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Et ryddigere spillbibliotek, uten duplikate samlinger.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = AppPalette.Muted,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);
        layout.Controls.Add(titleStack, 1, 0);

        var statusPill = CreateCard(
            Color.FromArgb(10, 42, 57),
            Color.FromArgb(8, 36, 51),
            Color.FromArgb(34, 91, 109),
            17);
        statusPill.Size = new Size(164, 36);
        statusPill.Anchor = AnchorStyles.Right;
        statusPill.Margin = Padding.Empty;

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(13, 0, 11, 0)
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _connectionDot.Dock = DockStyle.Fill;
        _connectionDot.Text = "●";
        _connectionDot.ForeColor = AppPalette.Muted;
        _connectionDot.Font = new Font("Segoe UI", 8f);
        _connectionDot.TextAlign = ContentAlignment.MiddleLeft;

        _connectionLabel.Dock = DockStyle.Fill;
        _connectionLabel.Text = "Kobler til Steam";
        _connectionLabel.ForeColor = AppPalette.Text;
        _connectionLabel.Font = new Font("Segoe UI Semibold", 8.8f);
        _connectionLabel.TextAlign = ContentAlignment.MiddleLeft;

        statusLayout.Controls.Add(_connectionDot, 0, 0);
        statusLayout.Controls.Add(_connectionLabel, 1, 0);
        statusPill.Controls.Add(statusLayout);
        layout.Controls.Add(statusPill, 2, 0);
        return panel;
    }

    private Control BuildOverview()
    {
        var panel = CreateCard();
        panel.Padding = new Padding(15);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.5f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.5f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.5f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.5f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var account = CreateCard(
            AppPalette.SurfaceRaised,
            AppPalette.SurfaceRaised,
            AppPalette.BorderSoft,
            15);
        account.Margin = new Padding(0, 0, 8, 0);
        account.Padding = new Padding(15, 10, 15, 9);
        var accountLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        accountLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        accountLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        accountLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        accountLayout.Controls.Add(CreateEyebrow("AKTIV STEAM-KONTO"), 0, 0);

        _accountLabel.Dock = DockStyle.Fill;
        _accountLabel.Text = "Finner kontoen din …";
        _accountLabel.Font = new Font("Segoe UI Semibold", 10.5f);
        _accountLabel.ForeColor = AppPalette.Text;
        _accountLabel.TextAlign = ContentAlignment.MiddleLeft;
        _accountLabel.AutoEllipsis = true;

        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.Text = "Leter etter Steam-mappen …";
        _pathLabel.Font = new Font("Segoe UI", 8.6f);
        _pathLabel.ForeColor = AppPalette.Muted;
        _pathLabel.TextAlign = ContentAlignment.TopLeft;
        _pathLabel.AutoEllipsis = true;

        accountLayout.Controls.Add(_accountLabel, 0, 1);
        accountLayout.Controls.Add(_pathLabel, 0, 2);
        account.Controls.Add(accountLayout);
        layout.Controls.Add(account, 0, 0);

        layout.Controls.Add(CreateMetricCard("SPILL", _gameCountValue), 1, 0);
        layout.Controls.Add(CreateMetricCard("INSTALLERT", _installedCountValue), 2, 0);
        layout.Controls.Add(CreateMetricCard("SPILT", _playedCountValue), 3, 0);
        layout.Controls.Add(CreateMetricCard("FORSLAG", _collectionCountValue, true), 4, 0);
        return panel;
    }

    private Control BuildContent()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 382));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var categories = BuildCategoryPanel();
        categories.Margin = new Padding(0, 0, 6, 0);
        layout.Controls.Add(categories, 0, 0);

        var games = BuildGamePanel();
        games.Margin = new Padding(6, 0, 0, 0);
        layout.Controls.Add(games, 1, 0);
        return layout;
    }

    private Control BuildCategoryPanel()
    {
        var panel = CreateCard();
        panel.Padding = new Padding(15);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        panel.Controls.Add(layout);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 61));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 67));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(CreateSectionTitle("Foreslåtte samlinger"), 0, 0);

        _selectedCountLabel.Dock = DockStyle.Fill;
        _selectedCountLabel.Text = "0 valgt";
        _selectedCountLabel.ForeColor = AppPalette.AccentLight;
        _selectedCountLabel.Font = new Font("Segoe UI Semibold", 8.6f);
        _selectedCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        heading.Controls.Add(_selectedCountLabel, 0, 1);

        ConfigureMiniButton(_selectAllButton, "Alle");
        _selectAllButton.Click += (_, _) => SetAllCategories(true);
        heading.SetRowSpan(_selectAllButton, 2);
        heading.Controls.Add(_selectAllButton, 1, 0);

        ConfigureMiniButton(_selectNoneButton, "Ingen");
        _selectNoneButton.Click += (_, _) => SetAllCategories(false);
        heading.SetRowSpan(_selectNoneButton, 2);
        heading.Controls.Add(_selectNoneButton, 2, 0);
        layout.Controls.Add(heading, 0, 0);

        _categoryList.Dock = DockStyle.Fill;
        _categoryList.Margin = new Padding(0, 2, 0, 12);
        _categoryList.SelectedIndexChanged += (_, _) => UpdateCategoryDescription();
        _categoryList.ItemCheck += (_, _) => BeginInvoke(new Action(UpdateSelectedCount));
        _categoryList.HandleCreated +=
            (_, _) => WindowsTheme.ApplyDarkExplorerTheme(_categoryList);
        layout.Controls.Add(_categoryList, 0, 1);

        var descriptionCard = CreateCard(
            AppPalette.SurfaceRaised,
            AppPalette.SurfaceRaised,
            AppPalette.BorderSoft,
            14);
        descriptionCard.Margin = new Padding(0, 0, 0, 10);
        descriptionCard.Padding = new Padding(13, 8, 13, 8);

        _categoryDescription.Dock = DockStyle.Fill;
        _categoryDescription.ForeColor = AppPalette.Muted;
        _categoryDescription.Font = new Font("Segoe UI", 8.8f);
        _categoryDescription.Text =
            "Velg en samling for å se hvilke spill som havner der.";
        _categoryDescription.TextAlign = ContentAlignment.MiddleLeft;
        descriptionCard.Controls.Add(_categoryDescription);
        layout.Controls.Add(descriptionCard, 0, 2);

        var replaceCard = CreateCard(
            Color.FromArgb(11, 39, 56),
            Color.FromArgb(9, 32, 47),
            Color.FromArgb(30, 76, 95),
            15);
        replaceCard.Padding = new Padding(13, 8, 11, 8);

        var replaceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        replaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        replaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        replaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        replaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        replaceLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Unngå duplikater",
            Font = new Font("Segoe UI Semibold", 9.3f),
            ForeColor = AppPalette.Text,
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        replaceLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Erstatter egne Steam-samlinger",
            Font = new Font("Segoe UI", 8.2f),
            ForeColor = AppPalette.Muted,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);

        _replaceCollectionsCheck.Checked = true;
        _replaceCollectionsCheck.Anchor = AnchorStyles.Right;
        replaceLayout.SetRowSpan(_replaceCollectionsCheck, 2);
        replaceLayout.Controls.Add(_replaceCollectionsCheck, 1, 0);
        replaceCard.Controls.Add(replaceLayout);
        layout.Controls.Add(replaceCard, 0, 3);
        return panel;
    }

    private Control BuildGamePanel()
    {
        var panel = CreateCard();
        panel.Padding = new Padding(15);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(CreateSectionTitle("Spillbibliotek"), 0, 0);

        _visibleGamesLabel.Dock = DockStyle.Fill;
        _visibleGamesLabel.Text = "0 spill";
        _visibleGamesLabel.Font = new Font("Segoe UI Semibold", 8.6f);
        _visibleGamesLabel.ForeColor = AppPalette.Muted;
        _visibleGamesLabel.TextAlign = ContentAlignment.MiddleCenter;
        heading.Controls.Add(_visibleGamesLabel, 1, 0);

        var searchHost = CreateCard(
            AppPalette.SurfaceRaised,
            AppPalette.SurfaceRaised,
            AppPalette.Border,
            15);
        searchHost.Margin = new Padding(8, 7, 0, 7);
        searchHost.Padding = new Padding(11, 4, 10, 4);
        var searchLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
        searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "⌕",
            Font = new Font("Segoe UI Symbol", 15f),
            ForeColor = AppPalette.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BackColor = AppPalette.SurfaceRaised;
        _searchBox.ForeColor = AppPalette.Text;
        _searchBox.BorderStyle = BorderStyle.None;
        _searchBox.Font = new Font("Segoe UI", 9.4f);
        _searchBox.PlaceholderText = "Søk etter spill eller kategori";
        _searchBox.Margin = new Padding(0, 7, 0, 0);
        _searchBox.TextChanged += (_, _) => ApplySearch();
        _searchBox.HandleCreated +=
            (_, _) => WindowsTheme.ApplyDarkExplorerTheme(_searchBox);
        searchLayout.Controls.Add(_searchBox, 1, 0);
        searchHost.Controls.Add(searchLayout);
        heading.Controls.Add(searchHost, 2, 0);
        layout.Controls.Add(heading, 0, 0);

        ConfigureGrid();
        _gameGrid.Margin = new Padding(0, 3, 0, 0);
        layout.Controls.Add(_gameGrid, 0, 1);
        return panel;
    }

    private Control BuildActions()
    {
        var panel = CreateCard(
            AppPalette.SurfaceRaised,
            AppPalette.Surface,
            AppPalette.Border,
            20);
        panel.Padding = new Padding(17, 13, 14, 12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 552));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        status.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _statusDot.Dock = DockStyle.Fill;
        _statusDot.Text = "●";
        _statusDot.ForeColor = AppPalette.Accent;
        _statusDot.Font = new Font("Segoe UI", 8f);
        _statusDot.TextAlign = ContentAlignment.MiddleLeft;
        status.Controls.Add(_statusDot, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Klar";
        _statusLabel.ForeColor = AppPalette.Text;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9.4f);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;
        status.Controls.Add(_statusLabel, 1, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Text = "Biblioteket skannes automatisk ved oppstart.";
        _summaryLabel.ForeColor = AppPalette.Muted;
        _summaryLabel.Font = new Font("Segoe UI", 8.5f);
        _summaryLabel.TextAlign = ContentAlignment.TopLeft;
        status.Controls.Add(_summaryLabel, 1, 1);

        _progressBar.Dock = DockStyle.Top;
        _progressBar.Height = 4;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 24;
        _progressBar.Visible = false;
        status.Controls.Add(_progressBar, 1, 2);
        layout.Controls.Add(status, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 0)
        };

        _writeButton.Text = "Organiser biblioteket";
        _writeButton.Size = new Size(190, 43);
        _writeButton.Primary = true;
        _writeButton.Enabled = false;
        _writeButton.Margin = new Padding(10, 0, 0, 0);
        _writeButton.Click += async (_, _) => await WriteCollectionsAsync();

        _scanButton.Text = "Skann på nytt";
        _scanButton.Size = new Size(132, 43);
        _scanButton.Margin = new Padding(10, 0, 0, 0);
        _scanButton.Click += async (_, _) => await ScanAsync();

        _backupButton.Text = "Sikkerhetskopier";
        _backupButton.Size = new Size(145, 43);
        _backupButton.Margin = Padding.Empty;
        _backupButton.Click += (_, _) => OpenBackups();

        buttons.Controls.Add(_writeButton);
        buttons.Controls.Add(_scanButton);
        buttons.Controls.Add(_backupButton);
        layout.Controls.Add(buttons, 1, 0);
        return panel;
    }

    private void ConfigureGrid()
    {
        _gameGrid.Dock = DockStyle.Fill;
        _gameGrid.BackgroundColor = AppPalette.Surface;
        _gameGrid.BorderStyle = BorderStyle.None;
        _gameGrid.ReadOnly = true;
        _gameGrid.AllowUserToAddRows = false;
        _gameGrid.AllowUserToDeleteRows = false;
        _gameGrid.AllowUserToResizeRows = false;
        _gameGrid.AllowUserToResizeColumns = true;
        _gameGrid.AutoGenerateColumns = false;
        _gameGrid.RowHeadersVisible = false;
        _gameGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gameGrid.MultiSelect = false;
        _gameGrid.EnableHeadersVisualStyles = false;
        _gameGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _gameGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _gameGrid.ColumnHeadersHeight = 42;
        _gameGrid.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _gameGrid.RowTemplate.Height = 42;
        _gameGrid.ColumnHeadersDefaultCellStyle.BackColor = AppPalette.SurfaceSoft;
        _gameGrid.ColumnHeadersDefaultCellStyle.ForeColor = AppPalette.Muted;
        _gameGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppPalette.SurfaceSoft;
        _gameGrid.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI Semibold", 8.7f);
        _gameGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
        _gameGrid.DefaultCellStyle.BackColor = AppPalette.Surface;
        _gameGrid.DefaultCellStyle.ForeColor = AppPalette.Text;
        _gameGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(27, 66, 94);
        _gameGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        _gameGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        _gameGrid.DefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
        _gameGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(17, 32, 55);
        _gameGrid.GridColor = AppPalette.BorderSoft;
        _gameGrid.HandleCreated +=
            (_, _) => WindowsTheme.ApplyDarkExplorerTheme(_gameGrid);

        _gameGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(GameRow.Name),
            HeaderText = "SPILL",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 35,
            MinimumWidth = 170
        });
        _gameGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(GameRow.Installed),
            HeaderText = "STATUS",
            Width = 88
        });
        _gameGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(GameRow.Hours),
            HeaderText = "TIMER",
            Width = 70
        });
        _gameGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(GameRow.LastPlayed),
            HeaderText = "SIST SPILT",
            Width = 105
        });
        _gameGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(GameRow.Categories),
            HeaderText = "FORESLÅTTE SAMLINGER",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 45,
            MinimumWidth = 210
        });
    }

    private async Task ScanAsync()
    {
        SetConnectionState("Kobler til Steam", AppPalette.Muted);
        SetBusy(true, "Skanner Steam-biblioteket …");
        _summaryLabel.Text = "Leser spill, spilletid og metadata.";
        _writeButton.Enabled = false;

        try
        {
            var progress = new Progress<string>(message => _statusLabel.Text = message);
            _scanResult = await Task.Run(() => _scanner.Scan(progress));
            _suggestions = _categorizer.BuildSuggestions(_scanResult.Games);

            PopulateAccountInfo(_scanResult);
            PopulateCategories(_suggestions);
            BuildRows();
            ApplySearch();
            _writeButton.Enabled = _suggestions.Count > 0;

            SetConnectionState("Steam er klar", AppPalette.Success);
            _statusLabel.Text =
                $"Klar til å organisere {_scanResult.Games.Count} spill";
            _summaryLabel.Text =
                $"{_suggestions.Count} smarte samlinger er foreslått. Sikkerhetskopi tas alltid først.";
        }
        catch (Exception exception)
        {
            _scanResult = null;
            _suggestions = [];
            _allRows = [];
            _categoryList.Items.Clear();
            _gameGrid.DataSource = null;
            ResetMetrics();
            SetConnectionState("Steam ikke funnet", Color.FromArgb(251, 113, 133));
            _statusLabel.Text = "Kunne ikke lese biblioteket";
            _summaryLabel.Text = "Kontroller Steam-installasjonen og prøv på nytt.";
            ModernDialog.Show(
                this,
                "Kunne ikke skanne Steam",
                "Programmet fikk ikke lest Steam-biblioteket.\n\n" +
                exception.Message +
                "\n\nPrøv å lukke Steam helt og skann på nytt dersom filene er låst.",
                "Lukk",
                DialogKind.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateAccountInfo(ScanResult result)
    {
        _accountLabel.Text = result.Context.Account.ToString();
        _pathLabel.Text = result.Context.SteamPath;
        _toolTip.SetToolTip(_pathLabel, result.Context.SteamPath);
        _gameCountValue.Text = result.Games.Count.ToString();
        _installedCountValue.Text =
            result.Games.Count(game => game.IsInstalled).ToString();
        _playedCountValue.Text =
            result.Games.Count(game => game.PlaytimeMinutes > 0).ToString();
        _collectionCountValue.Text = _suggestions.Count.ToString();
    }

    private void PopulateCategories(IReadOnlyList<CategorySuggestion> suggestions)
    {
        _categoryList.BeginUpdate();
        _categoryList.Items.Clear();
        for (var index = 0; index < suggestions.Count; index++)
        {
            _categoryList.Items.Add(
                suggestions[index],
                suggestions[index].IsCheckedByDefault);
        }
        _categoryList.EndUpdate();

        if (_categoryList.Items.Count > 0)
        {
            _categoryList.SelectedIndex = 0;
        }

        UpdateSelectedCount();
    }

    private void BuildRows()
    {
        if (_scanResult is null)
        {
            _allRows = [];
            return;
        }

        var categoryNamesByApp = new Dictionary<uint, List<string>>();
        foreach (var suggestion in _suggestions)
        {
            var shortName = suggestion.Name.Replace(
                CollectionCategorizer.CollectionPrefix,
                string.Empty,
                StringComparison.Ordinal);
            foreach (var appId in suggestion.AppIds)
            {
                if (!categoryNamesByApp.TryGetValue(appId, out var names))
                {
                    names = [];
                    categoryNamesByApp[appId] = names;
                }

                names.Add(shortName);
            }
        }

        _allRows = _scanResult.Games
            .Select(game => new GameRow
            {
                Name = game.Name,
                Installed = game.IsInstalled ? "Installert" : "Ikke installert",
                Hours = game.PlaytimeHours.ToString("0.#"),
                LastPlayed =
                    game.LastPlayed?.ToLocalTime().ToString("dd.MM.yyyy") ?? "—",
                Categories = categoryNamesByApp.TryGetValue(game.AppId, out var names)
                    ? string.Join(
                        ", ",
                        names.Order(StringComparer.CurrentCultureIgnoreCase))
                    : "—"
            })
            .ToArray();
    }

    private void ApplySearch()
    {
        var query = _searchBox.Text.Trim();
        var rows = string.IsNullOrWhiteSpace(query)
            ? _allRows
            : _allRows.Where(row =>
                    row.Name.Contains(
                        query,
                        StringComparison.CurrentCultureIgnoreCase) ||
                    row.Categories.Contains(
                        query,
                        StringComparison.CurrentCultureIgnoreCase))
                .ToArray();

        _gameGrid.DataSource = new BindingList<GameRow>(rows.ToList());
        _visibleGamesLabel.Text = rows.Count == 1 ? "1 spill" : $"{rows.Count} spill";
    }

    private void UpdateCategoryDescription()
    {
        _categoryDescription.Text =
            _categoryList.SelectedItem is CategorySuggestion suggestion
                ? $"{suggestion.AppIds.Count} spill  •  {suggestion.Description}"
                : "Velg en samling for å se hvilke spill som havner der.";
    }

    private void UpdateSelectedCount()
    {
        var selected = _categoryList.CheckedItems.Count;
        _selectedCountLabel.Text = $"{selected} valgt";
        _writeButton.Text = selected > 0
            ? $"Organiser {selected} samlinger"
            : "Velg samlinger først";
        _writeButton.Enabled =
            _scanResult is not null && selected > 0 && !_progressBar.Visible;
    }

    private void SetAllCategories(bool isChecked)
    {
        _categoryList.BeginUpdate();
        for (var index = 0; index < _categoryList.Items.Count; index++)
        {
            _categoryList.SetItemChecked(index, isChecked);
        }
        _categoryList.EndUpdate();
        BeginInvoke(new Action(UpdateSelectedCount));
    }

    private async Task WriteCollectionsAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        var selected = _categoryList.CheckedItems
            .Cast<CategorySuggestion>()
            .ToArray();
        if (selected.Length == 0)
        {
            ModernDialog.Show(
                this,
                "Velg samlinger",
                "Velg minst én samling før du organiserer biblioteket.",
                "Skjønner",
                DialogKind.Information);
            return;
        }

        var confirmation = ModernDialog.Confirm(
            this,
            "Klar til å organisere?",
            $"Programmet vil skrive {selected.Length} samlinger til Steam.\n\n" +
            (_replaceCollectionsCheck.Checked
                ? "Dine eksisterende egendefinerte samlinger erstattes for å unngå duplikater. "
                : "Dine eksisterende egendefinerte samlinger beholdes. ") +
            "Favoritter og Skjulte påvirkes ikke.\n\n" +
            "Det tas en full sikkerhetskopi før noe endres.",
            "Organiser nå",
            "Avbryt");
        if (!confirmation)
        {
            return;
        }

        SetBusy(true, "Forbereder Steam …");
        _summaryLabel.Text = "Ingen filer endres før sikkerhetskopien er ferdig.";
        try
        {
            if (_locator.IsSteamRunning())
            {
                var closeSteam = ModernDialog.Confirm(
                    this,
                    "Steam må avsluttes",
                    "Steam-klienten må være helt lukket mens samlingene oppdateres.\n\n" +
                    "Vil du at programmet skal avslutte Steam kontrollert nå?",
                    "Avslutt Steam",
                    "Ikke nå");
                if (!closeSteam)
                {
                    _statusLabel.Text = "Avbrutt – Steam kjører fortsatt";
                    _summaryLabel.Text = "Lukk Steam og prøv igjen når du er klar.";
                    return;
                }

                _statusLabel.Text = "Venter på at Steam avsluttes …";
                var stopped = await _locator.ShutdownSteamAsync(
                    _scanResult.Context,
                    TimeSpan.FromSeconds(30));
                if (!stopped)
                {
                    throw new InvalidOperationException(
                        "Steam ble ikke avsluttet innen 30 sekunder. " +
                        "Avslutt klienten manuelt og prøv igjen.");
                }
            }

            _statusLabel.Text = "Sikkerhetskopierer og skriver samlinger …";
            var result = await Task.Run(() => _writer.Write(
                _scanResult.Context,
                selected,
                _replaceCollectionsCheck.Checked));

            _statusLabel.Text = "Biblioteket er organisert";
            _summaryLabel.Text =
                $"{result.Created} opprettet, {result.Updated} oppdatert og {result.Removed} fjernet.";

            var restart = ModernDialog.Confirm(
                this,
                "Alt er ryddet",
                $"Steam-biblioteket er oppdatert.\n\n" +
                $"Opprettet:  {result.Created}\n" +
                $"Oppdatert:  {result.Updated}\n" +
                $"Fjernet:  {result.Removed}\n\n" +
                $"Sikkerhetskopien ligger her:\n{result.BackupDirectory}\n\n" +
                "Vil du starte Steam nå?",
                "Start Steam",
                "Senere",
                DialogKind.Success);

            if (restart)
            {
                _locator.StartSteam(_scanResult.Context);
            }
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Kunne ikke skrive samlingene";
            _summaryLabel.Text = "Steam-dataene ble ikke ferdig oppdatert.";
            ModernDialog.Show(
                this,
                "Noe gikk galt",
                "Programmet kunne ikke skrive samlingene.\n\n" + exception.Message,
                "Lukk",
                DialogKind.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenBackups()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamBibliotekRydder",
            "Backups");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _progressBar.Visible = busy;
        _scanButton.Enabled = !busy;
        _backupButton.Enabled = !busy;
        _selectAllButton.Enabled = !busy;
        _selectNoneButton.Enabled = !busy;
        _categoryList.Enabled = !busy;
        _replaceCollectionsCheck.Enabled = !busy;
        _searchBox.Enabled = !busy;
        _writeButton.Enabled =
            !busy &&
            _scanResult is not null &&
            _categoryList.CheckedItems.Count > 0;
        _statusDot.ForeColor = busy ? AppPalette.AccentLight : AppPalette.Accent;
        UseWaitCursor = busy;

        if (!string.IsNullOrWhiteSpace(status))
        {
            _statusLabel.Text = status;
        }
    }

    private void SetConnectionState(string text, Color color)
    {
        _connectionLabel.Text = text;
        _connectionDot.ForeColor = color;
    }

    private void ResetMetrics()
    {
        _accountLabel.Text = "Steam-konto ikke funnet";
        _pathLabel.Text = "—";
        _gameCountValue.Text = "—";
        _installedCountValue.Text = "—";
        _playedCountValue.Text = "—";
        _collectionCountValue.Text = "—";
        _visibleGamesLabel.Text = "0 spill";
        _selectedCountLabel.Text = "0 valgt";
    }

    private static RoundedPanel CreateCard(
        Color? fillColor = null,
        Color? gradientColor = null,
        Color? borderColor = null,
        int radius = 20) =>
        new()
        {
            Dock = DockStyle.Fill,
            FillColor = fillColor ?? AppPalette.Surface,
            GradientColor = gradientColor ?? Color.FromArgb(12, 24, 44),
            BorderColor = borderColor ?? AppPalette.BorderSoft,
            Radius = radius
        };

    private static Label CreateEyebrow(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font("Segoe UI Semibold", 7.7f),
            ForeColor = AppPalette.Dim,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Label CreateSectionTitle(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font("Segoe UI Semibold", 12.2f),
            ForeColor = AppPalette.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Label CreateValueLabel() =>
        new()
        {
            Dock = DockStyle.Fill,
            Text = "—",
            Font = new Font("Segoe UI Semibold", 18f),
            ForeColor = AppPalette.Text,
            TextAlign = ContentAlignment.MiddleCenter
        };

    private static Control CreateMetricCard(
        string label,
        Label valueLabel,
        bool accent = false)
    {
        var panel = CreateCard(
            accent ? Color.FromArgb(12, 45, 63) : AppPalette.SurfaceRaised,
            accent ? Color.FromArgb(9, 35, 50) : AppPalette.SurfaceRaised,
            accent ? Color.FromArgb(33, 91, 111) : AppPalette.BorderSoft,
            15);
        panel.Margin = new Padding(8, 0, 0, 0);
        panel.Padding = new Padding(8, 8, 8, 7);

        valueLabel.ForeColor = accent ? AppPalette.AccentLight : AppPalette.Text;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var eyebrow = CreateEyebrow(label);
        eyebrow.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(eyebrow, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void ConfigureMiniButton(ModernButton button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Height = 31;
        button.Radius = 11;
        button.Font = new Font("Segoe UI Semibold", 8.2f);
        button.Margin = new Padding(5, 9, 0, 10);
    }

    private sealed class GameRow
    {
        public string Name { get; init; } = string.Empty;

        public string Installed { get; init; } = string.Empty;

        public string Hours { get; init; } = string.Empty;

        public string LastPlayed { get; init; } = string.Empty;

        public string Categories { get; init; } = string.Empty;
    }
}
