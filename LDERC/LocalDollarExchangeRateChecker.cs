using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Local Dollar Exchange Rate Checker")]
[assembly: System.Reflection.AssemblyProduct("Local Dollar Exchange Rate Checker")]
[assembly: System.Reflection.AssemblyDescription("Track live USD exchange rates in a native Windows app.")]

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly List<CurrencyInfo> _currencies = CurrencyCatalog.All
        .OrderBy(currency => currency.EnglishName)
        .ToList();

    private readonly HttpClient _httpClient = new HttpClient();
    private readonly Timer _refreshTimer = new Timer();
    private readonly TextBox _searchBox = new TextBox();
    private readonly TextBox _amountBox = new TextBox();
    private readonly Button _refreshButton = new Button();
    private readonly Label _statusLabel = new Label();
    private readonly Label _resultsLabel = new Label();
    private readonly Label _creditLabel = new Label();
    private readonly LinkLabel _githubLink = new LinkLabel();
    private readonly Label _heroCodeLabel = new Label();
    private readonly Label _heroNameLabel = new Label();
    private readonly Label _heroConversionLabel = new Label();
    private readonly Label _heroRateLabel = new Label();
    private readonly Label _heroUpdatedLabel = new Label();
    private readonly ListView _currencyList = new ListView();

    private readonly Dictionary<string, decimal> _rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    private string _selectedCode = "EUR";
    private bool _isRenderingList;

    public MainForm()
    {
        Text = "Local Dollar Exchange Rate Checker";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1200, 780);
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(8, 14, 24);
        ForeColor = Color.FromArgb(245, 247, 251);
        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowIcon = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        BuildLayout();

        _refreshTimer.Interval = 60000;
        _refreshTimer.Tick += async (sender, e) => await RefreshRatesAsync();
        Shown += async (sender, e) => await RefreshRatesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _httpClient.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 290F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        root.Controls.Add(BuildHeroPanel(), 0, 0);
        root.Controls.Add(BuildListPanel(), 0, 1);
    }

    private Control BuildHeroPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.FromArgb(12, 22, 37),
            Padding = new Padding(24)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var eyebrow = new Label
        {
            Text = "LIVE USD EXCHANGE RATES",
            ForeColor = Color.FromArgb(255, 209, 102),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var title = new Label
        {
            Text = "Check the dollar against major world currencies.",
            Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = ForeColor,
            AutoSize = false,
            Size = new Size(560, 100),
            Location = new Point(0, 28)
        };

        var subtitle = new Label
        {
            Text = "Search by code or name. Auto-refresh every 60 seconds.",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(141, 163, 191),
            AutoSize = false,
            Size = new Size(520, 40),
            Location = new Point(0, 132)
        };

        var amountLabel = new Label
        {
            Text = "USD amount for calculator",
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(216, 226, 240),
            AutoSize = true,
            Location = new Point(0, 168)
        };

        _amountBox.Text = "1";
        _amountBox.BorderStyle = BorderStyle.FixedSingle;
        _amountBox.BackColor = Color.FromArgb(7, 17, 31);
        _amountBox.ForeColor = ForeColor;
        _amountBox.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point);
        _amountBox.Location = new Point(0, 194);
        _amountBox.Size = new Size(220, 36);
        _amountBox.TextChanged += HandleAmountChanged;

        left.Controls.Add(eyebrow);
        left.Controls.Add(title);
        left.Controls.Add(subtitle);
        left.Controls.Add(amountLabel);
        left.Controls.Add(_amountBox);

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 35, 58),
            Padding = new Padding(20)
        };

        var selectedLabel = new Label
        {
            Text = "SELECTED CURRENCY",
            ForeColor = Color.FromArgb(255, 209, 102),
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _heroCodeLabel.Text = _selectedCode;
        _heroCodeLabel.Font = new Font("Segoe UI", 34F, FontStyle.Bold, GraphicsUnit.Point);
        _heroCodeLabel.AutoSize = true;
        _heroCodeLabel.Location = new Point(0, 26);

        _heroNameLabel.Text = "Euro / Евро";
        _heroNameLabel.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        _heroNameLabel.ForeColor = Color.FromArgb(216, 226, 240);
        _heroNameLabel.AutoSize = true;
        _heroNameLabel.Location = new Point(0, 92);

        _heroConversionLabel.Text = "1 USD = Loading...";
        _heroConversionLabel.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold, GraphicsUnit.Point);
        _heroConversionLabel.ForeColor = Color.FromArgb(135, 224, 255);
        _heroConversionLabel.AutoSize = true;
        _heroConversionLabel.Location = new Point(0, 130);

        _heroRateLabel.Text = "1 USD = Loading...";
        _heroRateLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _heroRateLabel.ForeColor = Color.FromArgb(216, 226, 240);
        _heroRateLabel.AutoSize = true;
        _heroRateLabel.Location = new Point(0, 166);

        _heroUpdatedLabel.Text = "Waiting for data...";
        _heroUpdatedLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _heroUpdatedLabel.ForeColor = Color.FromArgb(141, 163, 191);
        _heroUpdatedLabel.AutoSize = true;
        _heroUpdatedLabel.Location = new Point(0, 204);

        right.Controls.Add(selectedLabel);
        right.Controls.Add(_heroCodeLabel);
        right.Controls.Add(_heroNameLabel);
        right.Controls.Add(_heroConversionLabel);
        right.Controls.Add(_heroRateLabel);
        right.Controls.Add(_heroUpdatedLabel);

        panel.Controls.Add(left, 0, 0);
        panel.Controls.Add(right, 1, 0);

        return panel;
    }

    private Control BuildListPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 22, 37),
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 4
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var head = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136F));

        var headerText = new Label
        {
            Text = "Currency Search",
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = ForeColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _refreshButton.Text = "Refresh now";
        StyleButton(_refreshButton, Color.FromArgb(30, 76, 109));
        _refreshButton.Click += async (sender, e) => await RefreshRatesAsync();

        var creditPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 10, 0, 0),
            AutoSize = true
        };

        _creditLabel.Text = "App created by GMD_HORUS.";
        _creditLabel.ForeColor = Color.FromArgb(141, 163, 191);
        _creditLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _creditLabel.AutoSize = true;
        _creditLabel.Margin = new Padding(0, 3, 6, 0);

        _githubLink.Text = "GitHub";
        _githubLink.LinkColor = Color.FromArgb(135, 224, 255);
        _githubLink.ActiveLinkColor = Color.FromArgb(255, 209, 102);
        _githubLink.VisitedLinkColor = Color.FromArgb(135, 224, 255);
        _githubLink.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
        _githubLink.AutoSize = true;
        _githubLink.Margin = new Padding(0, 3, 0, 0);
        _githubLink.LinkClicked += HandleGithubLinkClicked;

        creditPanel.Controls.Add(_creditLabel);
        creditPanel.Controls.Add(_githubLink);

        head.Controls.Add(headerText, 0, 0);
        head.Controls.Add(creditPanel, 1, 0);
        head.Controls.Add(_refreshButton, 2, 0);

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.BackColor = Color.FromArgb(7, 17, 31);
        _searchBox.ForeColor = ForeColor;
        _searchBox.Font = new Font("Segoe UI", 11.5F, FontStyle.Regular, GraphicsUnit.Point);
        _searchBox.TextChanged += (sender, e) => RenderCurrencyList();

        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(141, 163, 191);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "Loading live rates...";

        _resultsLabel.Dock = DockStyle.Fill;
        _resultsLabel.ForeColor = Color.FromArgb(141, 163, 191);
        _resultsLabel.TextAlign = ContentAlignment.MiddleRight;

        statusRow.Controls.Add(_statusLabel, 0, 0);
        statusRow.Controls.Add(_resultsLabel, 1, 0);

        _currencyList.Dock = DockStyle.Fill;
        _currencyList.View = View.Details;
        _currencyList.FullRowSelect = true;
        _currencyList.GridLines = false;
        _currencyList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _currencyList.HideSelection = false;
        _currencyList.MultiSelect = false;
        _currencyList.BackColor = Color.FromArgb(7, 17, 31);
        _currencyList.ForeColor = ForeColor;
        _currencyList.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _currencyList.BorderStyle = BorderStyle.FixedSingle;
        _currencyList.Columns.Add("Code", 90);
        _currencyList.Columns.Add("Currency", 430);
        _currencyList.Columns.Add("Rate per 1 USD", 160, HorizontalAlignment.Right);
        _currencyList.Columns.Add("Value for amount", 190, HorizontalAlignment.Right);
        _currencyList.SelectedIndexChanged += (sender, e) => HandleSelectionChanged();

        panel.Controls.Add(head, 0, 0);
        panel.Controls.Add(_searchBox, 0, 1);
        panel.Controls.Add(statusRow, 0, 2);
        panel.Controls.Add(_currencyList, 0, 3);

        RenderCurrencyList();
        UpdateHero();

        return panel;
    }

    private void StyleButton(Button button, Color backColor)
    {
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = backColor;
        button.ForeColor = ForeColor;
        button.Cursor = Cursors.Hand;
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
    }

    private void HandleAmountChanged(object sender, EventArgs e)
    {
        UpdateHero();
        RenderCurrencyList();
    }

    private void HandleGithubLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/kirpi4blin-GD",
            UseShellExecute = true
        });
    }

    private void HandleSelectionChanged()
    {
        if (_isRenderingList)
        {
            return;
        }

        if (_currencyList.SelectedItems.Count == 0)
        {
            return;
        }

        var selectedCode = _currencyList.SelectedItems[0].Tag as string;
        if (!string.IsNullOrEmpty(selectedCode))
        {
            _selectedCode = selectedCode;
        }

        UpdateHero();
        RenderCurrencyList();
    }

    private void RenderCurrencyList()
    {
        var query = (_searchBox.Text ?? string.Empty).Trim();
        var filtered = _currencies.Where(currency => currency.Matches(query)).ToList();

        _isRenderingList = true;
        _currencyList.BeginUpdate();
        _currencyList.Items.Clear();

        foreach (var currency in filtered)
        {
            var item = new ListViewItem(currency.Code);
            item.Tag = currency.Code;
            item.SubItems.Add(currency.DisplayName);
            item.SubItems.Add(FormatRate(currency.Code));
            item.SubItems.Add(FormatConvertedAmount(currency.Code));
            _currencyList.Items.Add(item);

            if (string.Equals(currency.Code, _selectedCode, StringComparison.OrdinalIgnoreCase))
            {
                item.Selected = true;
                item.Focused = true;
            }
        }

        _currencyList.EndUpdate();
        _isRenderingList = false;
        _resultsLabel.Text = filtered.Count + " currencies";
    }

    private void UpdateHero()
    {
        var currency = _currencies.FirstOrDefault(item => item.Code == _selectedCode) ?? _currencies.First();
        _heroCodeLabel.Text = currency.Code;
        _heroNameLabel.Text = currency.DisplayName;
        _heroConversionLabel.Text = FormatConversionHeadline(currency.Code);
        _heroRateLabel.Text = "1 USD = " + FormatRate(currency.Code) + " " + currency.Code;
    }

    private string FormatConversionHeadline(string code)
    {
        decimal amount;
        if (!TryGetUsdAmount(out amount))
        {
            return "Enter a valid USD amount";
        }

        decimal rate;
        if (!_rates.TryGetValue(code, out rate))
        {
            return FormatNumber(amount) + " USD = N/A";
        }

        return FormatNumber(amount) + " USD = " + FormatNumber(amount * rate) + " " + code;
    }

    private string FormatConvertedAmount(string code)
    {
        decimal amount;
        decimal rate;

        if (!TryGetUsdAmount(out amount))
        {
            return "-";
        }

        if (!_rates.TryGetValue(code, out rate))
        {
            return "N/A";
        }

        return FormatNumber(amount * rate);
    }

    private string FormatRate(string code)
    {
        decimal rate;
        if (!_rates.TryGetValue(code, out rate))
        {
            return "N/A";
        }

        return rate.ToString("N4", CultureInfo.InvariantCulture);
    }

    private string FormatNumber(decimal value)
    {
        return value.ToString("N2", CultureInfo.InvariantCulture);
    }

    private bool TryGetUsdAmount(out decimal amount)
    {
        var text = (_amountBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
        {
            amount = 1M;
            return true;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private async Task RefreshRatesAsync()
    {
        _refreshButton.Enabled = false;
        _statusLabel.Text = "Refreshing live rates...";

        try
        {
            using (var response = await _httpClient.GetAsync("https://open.er-api.com/v6/latest/USD"))
            {
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadAsStringAsync();
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<ExchangeRateResponse>(payload);

                if (data == null || data.Rates == null || data.Rates.Count == 0)
                {
                    throw new InvalidOperationException("Rate payload was empty.");
                }

                _rates.Clear();
                foreach (var pair in data.Rates)
                {
                    _rates[pair.Key] = pair.Value;
                }

                _statusLabel.Text = "Live rates loaded successfully.";
                _heroUpdatedLabel.Text = "Last updated: " + DateTime.Now.ToString("g");
                UpdateHero();
                RenderCurrencyList();
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Could not load live rates. Check your internet connection.";
            _heroUpdatedLabel.Text = "Last update failed. Please try again later.";
            Debug.WriteLine("Rate refresh failed: " + ex);
            UpdateHero();
            RenderCurrencyList();
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }
}

internal sealed class ExchangeRateResponse
{
    public Dictionary<string, decimal> Rates { get; set; }
}

internal sealed class CurrencyInfo
{
    private readonly string _code;
    private readonly string _englishName;
    private readonly string _russianName;

    public CurrencyInfo(string code, string englishName, string russianName)
    {
        _code = code;
        _englishName = englishName;
        _russianName = russianName;
    }

    public string Code
    {
        get { return _code; }
    }

    public string EnglishName
    {
        get { return _englishName; }
    }

    public string RussianName
    {
        get { return _russianName; }
    }

    public string DisplayName
    {
        get { return EnglishName + " / " + RussianName; }
    }

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        return Code.IndexOf(query, comparison) >= 0
            || EnglishName.IndexOf(query, comparison) >= 0
            || RussianName.IndexOf(query, comparison) >= 0;
    }
}

internal static class CurrencyCatalog
{
    public static readonly IList<CurrencyInfo> All = new List<CurrencyInfo>
    {
        new CurrencyInfo("AUD", "Australian Dollar", "Австралийский доллар"),
        new CurrencyInfo("BGN", "Bulgarian Lev", "Болгарский лев"),
        new CurrencyInfo("BRL", "Brazilian Real", "Бразильский реал"),
        new CurrencyInfo("CAD", "Canadian Dollar", "Канадский доллар"),
        new CurrencyInfo("CHF", "Swiss Franc", "Швейцарский франк"),
        new CurrencyInfo("CNY", "Chinese Yuan", "Китайский юань"),
        new CurrencyInfo("CZK", "Czech Koruna", "Чешская крона"),
        new CurrencyInfo("DKK", "Danish Krone", "Датская крона"),
        new CurrencyInfo("EUR", "Euro", "Евро"),
        new CurrencyInfo("GBP", "British Pound", "Британский фунт"),
        new CurrencyInfo("HKD", "Hong Kong Dollar", "Гонконгский доллар"),
        new CurrencyInfo("HUF", "Hungarian Forint", "Венгерский форинт"),
        new CurrencyInfo("IDR", "Indonesian Rupiah", "Индонезийская рупия"),
        new CurrencyInfo("ILS", "Israeli New Shekel", "Израильский новый шекель"),
        new CurrencyInfo("INR", "Indian Rupee", "Индийская рупия"),
        new CurrencyInfo("JPY", "Japanese Yen", "Японская иена"),
        new CurrencyInfo("KRW", "South Korean Won", "Южнокорейская вона"),
        new CurrencyInfo("KZT", "Kazakhstani Tenge", "Казахстанский тенге"),
        new CurrencyInfo("MXN", "Mexican Peso", "Мексиканское песо"),
        new CurrencyInfo("MYR", "Malaysian Ringgit", "Малайзийский ринггит"),
        new CurrencyInfo("NOK", "Norwegian Krone", "Норвежская крона"),
        new CurrencyInfo("NZD", "New Zealand Dollar", "Новозеландский доллар"),
        new CurrencyInfo("PHP", "Philippine Peso", "Филиппинское песо"),
        new CurrencyInfo("PLN", "Polish Zloty", "Польский злотый"),
        new CurrencyInfo("RON", "Romanian Leu", "Румынский лей"),
        new CurrencyInfo("RUB", "Russian Ruble", "Российский рубль"),
        new CurrencyInfo("SEK", "Swedish Krona", "Шведская крона"),
        new CurrencyInfo("SGD", "Singapore Dollar", "Сингапурский доллар"),
        new CurrencyInfo("THB", "Thai Baht", "Тайский бат"),
        new CurrencyInfo("TRY", "Turkish Lira", "Турецкая лира"),
        new CurrencyInfo("UAH", "Ukrainian Hryvnia", "Украинская гривна"),
        new CurrencyInfo("USD", "US Dollar", "Доллар США"),
        new CurrencyInfo("ZAR", "South African Rand", "Южноафриканский рэнд")
    };
}
