using System.Security.Cryptography;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class ApiSettingsForm : Form
{
    private static readonly string SavedCredentialMask = new('•', 20);
    private readonly OutfitSettingsStore _store;
    private readonly CredentialStore _credentials;
    private OutfitAppSettings _settings;
    private readonly ComboBox _defaultProvider = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _editingProvider = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly TextBox _key = new() { Name = "ImageApiKey", Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Button _reveal = new() { Name = "ImageReveal", Text = "显示", Width = 56, Height = 27 };
    private readonly ComboBox _model = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _saveFeedback = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly ComboBox _region = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _workspace = new() { Dock = DockStyle.Fill };
    private readonly TextBox _baseUrl = new() { Dock = DockStyle.Fill };
    private readonly Label _regionLabel = new() { Text = "Region", AutoSize = true };
    private readonly Label _workspaceLabel = new() { Text = "Workspace ID", AutoSize = true };
    private readonly Label _baseUrlLabel = new() { Text = "Base URL", AutoSize = true };
    private bool _showingSavedCredentialMask;
    private readonly TextBox _translationKey = new() { Name = "TranslationApiKey", Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Button _translationReveal = new() { Name = "TranslationReveal", Text = "显示", Width = 56, Height = 27 };
    private readonly ComboBox _translationModel = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _translationLanguage = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _translationStatus = new() { Dock = DockStyle.Fill };
    private readonly Label _translationFeedback = new() { Dock = DockStyle.Fill };
    private bool _translationMask;

    public ApiSettingsForm(OutfitSettingsStore store, CredentialStore credentials)
    {
        _store = store;
        _credentials = credentials;
        _settings = store.Load();
        Text = "AI 接口设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(500, 510);
        Size = new Size(600, 530);
        Font = new Font("Microsoft YaHei UI", 9f);

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 11, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 2 ? 58 : 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        TabControl tabs = new() { Dock = DockStyle.Fill };
        TabPage imageTab = new("图像 API");
        TabPage translationTab = new("翻译 API");
        imageTab.Controls.Add(layout);
        tabs.TabPages.Add(imageTab);
        tabs.TabPages.Add(translationTab);
        Controls.Add(tabs);
        BuildTranslationSettings(translationTab);
        AddRow(layout, 0, "默认 AI 服务", _defaultProvider);
        AddRow(layout, 1, "编辑服务商", _editingProvider);
        layout.Controls.Add(_status, 0, 2);
        layout.SetColumnSpan(_status, 2);
        TableLayoutPanel keyRow = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        _reveal.Click += (_, _) =>
        {
            if (_showingSavedCredentialMask) return;
            _key.UseSystemPasswordChar = !_key.UseSystemPasswordChar;
            _reveal.Text = _key.UseSystemPasswordChar ? "显示" : "隐藏";
        };
        _key.Enter += (_, _) => BeginEditingCredential();
        _key.MouseDown += (_, _) => BeginEditingCredential();
        _key.KeyDown += (_, _) => BeginEditingCredential();
        _key.TextChanged += (_, _) =>
        {
            if (_showingSavedCredentialMask && _key.Text != SavedCredentialMask)
            {
                _showingSavedCredentialMask = false;
                _reveal.Enabled = true;
            }
        };
        keyRow.Controls.Add(_key, 0, 0);
        keyRow.Controls.Add(_reveal, 1, 0);
        AddRow(layout, 3, "API Key", keyRow);
        AddRow(layout, 4, "Model", _model);
        layout.Controls.Add(_regionLabel, 0, 5);
        layout.Controls.Add(_region, 1, 5);
        layout.Controls.Add(_workspaceLabel, 0, 6);
        layout.Controls.Add(_workspace, 1, 6);
        layout.Controls.Add(_baseUrlLabel, 0, 7);
        layout.Controls.Add(_baseUrl, 1, 7);
        Label note = new() { Text = "测试连接不上传截图，也不消耗图片生成额度。", Dock = DockStyle.Fill };
        layout.Controls.Add(note, 0, 8);
        layout.SetColumnSpan(note, 2);
        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, WrapContents = false };
        Button save = new() { Text = "保存", Width = 82, Height = 30 };
        save.Click += (_, _) => SaveSettings();
        Button delete = new() { Text = "删除 Key", Width = 92, Height = 30 };
        delete.Click += (_, _) => Delete();
        Button test = new() { Text = "测试连接", Width = 92, Height = 30 };
        test.Click += (_, _) => MessageBox.Show(this, "配置已保存，将在首次生成时验证。", "连接状态",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        buttons.Controls.Add(save);
        buttons.Controls.Add(delete);
        buttons.Controls.Add(test);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(_saveFeedback, 0, 10);
        layout.SetColumnSpan(_saveFeedback, 2);

        foreach (ImageEditProviderKind provider in Enum.GetValues<ImageEditProviderKind>())
        {
            _defaultProvider.Items.Add(new ProviderChoice(provider));
            _editingProvider.Items.Add(new ProviderChoice(provider));
        }
        _region.Items.AddRange([new RegionChoice("Beijing", "中国北京"), new RegionChoice("Singapore", "新加坡"),
            new RegionChoice("Virginia", "美国 Virginia"), new RegionChoice("Custom", "自定义")]);
        _defaultProvider.SelectedIndex = (int)_settings.DefaultProvider;
        _editingProvider.SelectedIndexChanged += (_, _) => LoadEditor();
        _region.SelectedIndexChanged += (_, _) => UpdateQwenFields();
        _editingProvider.SelectedIndex = (int)_settings.DefaultProvider;
        RefreshStatus();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string name, Control control)
    {
        layout.Controls.Add(new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void BuildTranslationSettings(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 0 ? 54 : 44));
        page.Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "DeepSeek", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        layout.Controls.Add(_translationStatus, 0, 1);
        layout.SetColumnSpan(_translationStatus, 2);
        TableLayoutPanel keyRow = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        keyRow.Controls.Add(_translationKey, 0, 0);
        keyRow.Controls.Add(_translationReveal, 1, 0);
        AddRow(layout, 2, "API Key", keyRow);
        _translationKey.Enter += (_, _) => BeginTranslationKeyEdit();
        _translationKey.MouseDown += (_, _) => BeginTranslationKeyEdit();
        _translationKey.KeyDown += (_, _) => BeginTranslationKeyEdit();
        _translationReveal.Click += (_, _) =>
        {
            if (_translationMask) return;
            _translationKey.UseSystemPasswordChar = !_translationKey.UseSystemPasswordChar;
            _translationReveal.Text = _translationKey.UseSystemPasswordChar ? "显示" : "隐藏";
        };
        _translationModel.Items.AddRange(["deepseek-v4-flash", "deepseek-v4-pro"]);
        _translationModel.SelectedItem = _translationModel.Items.Contains(_settings.DeepSeekModel) ? _settings.DeepSeekModel : "deepseek-v4-flash";
        AddRow(layout, 3, "Model", _translationModel);
        _translationLanguage.Items.AddRange(["简体中文", "English", "日本語", "한국어"]);
        _translationLanguage.SelectedItem = _translationLanguage.Items.Contains(_settings.TranslationTargetLanguage)
            ? _settings.TranslationTargetLanguage : "简体中文";
        AddRow(layout, 4, "目标语言", _translationLanguage);
        FlowLayoutPanel actions = new() { Dock = DockStyle.Fill };
        Button save = new() { Text = "保存", Width = 82 };
        Button delete = new() { Text = "删除 Key", Width = 92 };
        save.Click += (_, _) => SaveTranslationSettings();
        delete.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "删除 DeepSeek API Key？", "确认删除", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { _credentials.DeleteTranslationCredential(); RefreshTranslationCredentialDisplay(); }
            catch (Exception) { _translationFeedback.Text = "删除失败，请重试"; }
        };
        actions.Controls.Add(save);
        actions.Controls.Add(delete);
        layout.Controls.Add(actions, 0, 5);
        layout.SetColumnSpan(actions, 2);
        layout.Controls.Add(_translationFeedback, 0, 6);
        layout.SetColumnSpan(_translationFeedback, 2);
        RefreshTranslationCredentialDisplay();
    }

    private void BeginTranslationKeyEdit()
    {
        if (!_translationMask) return;
        _translationMask = false;
        _translationKey.Clear();
        _translationReveal.Enabled = true;
    }

    private void RefreshTranslationCredentialDisplay()
    {
        _translationMask = false;
        _translationKey.Clear();
        _translationKey.UseSystemPasswordChar = true;
        _translationReveal.Text = "显示";
        bool configured = _credentials.HasTranslationCredential();
        if (configured) { _translationMask = true; _translationKey.Text = SavedCredentialMask; }
        _translationReveal.Enabled = !configured;
        _translationStatus.Text = configured ? "DeepSeek：已配置" : "DeepSeek：未配置";
    }

    internal bool SaveTranslationSettings()
    {
        try
        {
            OutfitAppSettings settings = _store.Load();
            settings.DeepSeekModel = (string)_translationModel.SelectedItem!;
            settings.TranslationTargetLanguage = (string)_translationLanguage.SelectedItem!;
            if (!_translationMask && !string.IsNullOrWhiteSpace(_translationKey.Text))
                _credentials.SaveTranslationCredential(_translationKey.Text);
            _store.Save(settings);
            _settings = settings;
            RefreshTranslationCredentialDisplay();
            _translationFeedback.ForeColor = Color.ForestGreen;
            _translationFeedback.Text = "✓ 已保存";
            return true;
        }
        catch (Exception)
        {
            _translationFeedback.ForeColor = Color.Firebrick;
            _translationFeedback.Text = "保存失败，请重试";
            return false;
        }
    }

    private ImageEditProviderKind SelectedProvider => (ImageEditProviderKind)_editingProvider.SelectedIndex;

    internal void SelectProviderForTesting(ImageEditProviderKind provider) => _editingProvider.SelectedIndex = (int)provider;

    internal bool ShowingSavedCredentialMaskForTesting => _showingSavedCredentialMask;

    private void BeginEditingCredential()
    {
        if (!_showingSavedCredentialMask) return;
        _showingSavedCredentialMask = false;
        _key.Clear();
        _reveal.Enabled = true;
    }

    private void UpdateCredentialDisplay()
    {
        _showingSavedCredentialMask = false;
        _key.Clear();
        _key.UseSystemPasswordChar = true;
        _reveal.Text = "显示";
        bool saved;
        try { saved = _credentials.HasCredential(SelectedProvider); }
        catch (Exception ex) when (ex is IOException or CryptographicException or UnauthorizedAccessException) { saved = false; }
        if (saved)
        {
            _showingSavedCredentialMask = true;
            _key.Text = SavedCredentialMask;
        }
        _reveal.Enabled = !saved;
    }

    private void LoadEditor()
    {
        UpdateCredentialDisplay();
        _saveFeedback.Text = string.Empty;
        _model.Items.Clear();
        switch (SelectedProvider)
        {
            case ImageEditProviderKind.Volcano:
                _model.Items.AddRange([AiOutfitPreviewService.ProModel, AiOutfitPreviewService.DefaultModel]);
                _model.SelectedItem = _model.Items.Contains(_settings.VolcanoModel)
                    ? _settings.VolcanoModel : AiOutfitPreviewService.DefaultModel;
                break;
            case ImageEditProviderKind.Qwen:
                _model.Items.AddRange(["qwen-image-3.0-pro", "qwen-image-3.0"]);
                _model.SelectedItem = _model.Items.Contains(_settings.QwenModel) ? _settings.QwenModel : "qwen-image-3.0-pro";
                _workspace.Text = _settings.QwenWorkspaceId;
                _baseUrl.Text = _settings.QwenCustomBaseUrl;
                _region.SelectedIndex = Math.Max(0, _region.Items.Cast<RegionChoice>().ToList().FindIndex(x => x.Id == _settings.QwenRegion));
                break;
            case ImageEditProviderKind.OpenAI:
                _model.Items.AddRange(["gpt-image-2.5-sunburst", "gpt-image-2.5-flare", "gpt-image-2"]);
                _model.SelectedItem = _model.Items.Contains(_settings.OpenAiModel) ? _settings.OpenAiModel : "gpt-image-2.5-sunburst";
                break;
        }
        UpdateQwenFields();
    }

    private void UpdateQwenFields()
    {
        bool qwen = SelectedProvider == ImageEditProviderKind.Qwen;
        _region.Visible = _regionLabel.Visible = qwen;
        _workspace.Visible = _workspaceLabel.Visible = qwen && (_region.SelectedItem as RegionChoice)?.Id != "Custom";
        _baseUrl.Visible = _baseUrlLabel.Visible = qwen && (_region.SelectedItem as RegionChoice)?.Id == "Custom";
    }

    internal bool SaveSettings()
    {
        if (_model.SelectedItem is not string selectedModel)
        {
            _saveFeedback.Text = "保存失败，请重试";
            return false;
        }
        try
        {
            OutfitAppSettings latest = _store.Load();
            latest.DefaultProvider = (ImageEditProviderKind)_defaultProvider.SelectedIndex;
            switch (SelectedProvider)
            {
                case ImageEditProviderKind.Volcano: latest.VolcanoModel = selectedModel; break;
                case ImageEditProviderKind.Qwen:
                    latest.QwenModel = selectedModel;
                    latest.QwenRegion = (_region.SelectedItem as RegionChoice)?.Id ?? "Beijing";
                    latest.QwenWorkspaceId = _workspace.Text.Trim();
                    latest.QwenCustomBaseUrl = _baseUrl.Text.Trim();
                    QwenEndpointBuilder.Build(latest);
                    break;
                case ImageEditProviderKind.OpenAI: latest.OpenAiModel = selectedModel; break;
            }
            if (!_showingSavedCredentialMask && !string.IsNullOrWhiteSpace(_key.Text))
                _credentials.SaveCredential(SelectedProvider, _key.Text);
            _store.Save(latest);
            _settings = latest;
            UpdateCredentialDisplay();
            RefreshStatus();
            _saveFeedback.ForeColor = Color.ForestGreen;
            _saveFeedback.Text = "✓ 已保存";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            _saveFeedback.ForeColor = Color.Firebrick;
            _saveFeedback.Text = "保存失败，请重试";
            return false;
        }
    }

    private void Delete()
    {
        if (MessageBox.Show(this, "删除当前服务商保存的 API Key？", "确认删除", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { DeleteSavedKey(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            MessageBox.Show(this, "删除失败，请检查用户目录权限。", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal void DeleteSavedKey()
    {
        _credentials.DeleteCredential(SelectedProvider);
        UpdateCredentialDisplay();
        RefreshStatus();
        _saveFeedback.Text = string.Empty;
    }

    private void RefreshStatus()
    {
        string Label(ImageEditProviderKind kind) => kind switch
        {
            ImageEditProviderKind.Volcano => "火山方舟",
            ImageEditProviderKind.Qwen => "千问 Qwen",
            _ => "OpenAI"
        };
        string State(ImageEditProviderKind kind)
        {
            try { return _credentials.HasCredential(kind) ? "已配置" : "未配置"; }
            catch (Exception ex) when (ex is IOException or CryptographicException) { return "不可读取"; }
        }
        _status.Text = string.Join("    ", Enum.GetValues<ImageEditProviderKind>().Select(kind => $"{Label(kind)}：{State(kind)}"));
    }

    private sealed record ProviderChoice(ImageEditProviderKind Kind)
    {
        public override string ToString() => Kind switch
        {
            ImageEditProviderKind.Volcano => "火山方舟",
            ImageEditProviderKind.Qwen => "阿里千问 Qwen",
            _ => "OpenAI"
        };
    }
    private sealed record RegionChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }
}
