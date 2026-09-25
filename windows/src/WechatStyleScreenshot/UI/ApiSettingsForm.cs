using System.Security.Cryptography;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class ApiSettingsForm : Form
{
    private readonly OutfitSettingsStore _store;
    private readonly CredentialStore _credentials;
    private readonly OutfitAppSettings _settings;
    private readonly ComboBox _defaultProvider = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _editingProvider = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly TextBox _key = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox _model = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _region = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _workspace = new() { Dock = DockStyle.Fill };
    private readonly TextBox _baseUrl = new() { Dock = DockStyle.Fill };
    private readonly Label _regionLabel = new() { Text = "Region", AutoSize = true };
    private readonly Label _workspaceLabel = new() { Text = "Workspace ID", AutoSize = true };
    private readonly Label _baseUrlLabel = new() { Text = "Base URL", AutoSize = true };

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
        Controls.Add(layout);
        AddRow(layout, 0, "默认 AI 服务", _defaultProvider);
        AddRow(layout, 1, "编辑服务商", _editingProvider);
        layout.Controls.Add(_status, 0, 2);
        layout.SetColumnSpan(_status, 2);
        TableLayoutPanel keyRow = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        Button reveal = new() { Text = "显示", Width = 56, Height = 27 };
        reveal.Click += (_, _) => { _key.UseSystemPasswordChar = !_key.UseSystemPasswordChar; reveal.Text = _key.UseSystemPasswordChar ? "显示" : "隐藏"; };
        keyRow.Controls.Add(_key, 0, 0);
        keyRow.Controls.Add(reveal, 1, 0);
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
        save.Click += (_, _) => Save();
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

    private ImageEditProviderKind SelectedProvider => (ImageEditProviderKind)_editingProvider.SelectedIndex;

    private void LoadEditor()
    {
        _key.Clear();
        _key.UseSystemPasswordChar = true;
        _model.DropDownStyle = ComboBoxStyle.DropDown;
        _model.Items.Clear();
        switch (SelectedProvider)
        {
            case ImageEditProviderKind.Volcano:
                _model.Text = _settings.VolcanoModel;
                break;
            case ImageEditProviderKind.Qwen:
                _model.Items.AddRange(["qwen-image-3.0-pro", "qwen-image-3.0"]);
                _model.DropDownStyle = ComboBoxStyle.DropDownList;
                _model.SelectedItem = _model.Items.Contains(_settings.QwenModel) ? _settings.QwenModel : "qwen-image-3.0-pro";
                _workspace.Text = _settings.QwenWorkspaceId;
                _baseUrl.Text = _settings.QwenCustomBaseUrl;
                _region.SelectedIndex = Math.Max(0, _region.Items.Cast<RegionChoice>().ToList().FindIndex(x => x.Id == _settings.QwenRegion));
                break;
            case ImageEditProviderKind.OpenAI:
                _model.Items.AddRange(["gpt-image-2.5-sunburst", "gpt-image-2.5-flare", "gpt-image-2"]);
                _model.DropDownStyle = ComboBoxStyle.DropDownList;
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

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_model.Text))
        {
            MessageBox.Show(this, "Model 不能为空。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            OutfitAppSettings latest = _store.Load();
            latest.DefaultProvider = (ImageEditProviderKind)_defaultProvider.SelectedIndex;
            switch (SelectedProvider)
            {
                case ImageEditProviderKind.Volcano: latest.VolcanoModel = _model.Text.Trim(); break;
                case ImageEditProviderKind.Qwen:
                    latest.QwenModel = _model.Text.Trim();
                    latest.QwenRegion = (_region.SelectedItem as RegionChoice)?.Id ?? "Beijing";
                    latest.QwenWorkspaceId = _workspace.Text.Trim();
                    latest.QwenCustomBaseUrl = _baseUrl.Text.Trim();
                    QwenEndpointBuilder.Build(latest);
                    break;
                case ImageEditProviderKind.OpenAI: latest.OpenAiModel = _model.Text.Trim(); break;
            }
            if (!string.IsNullOrWhiteSpace(_key.Text)) _credentials.SaveCredential(SelectedProvider, _key.Text);
            _store.Save(latest);
            _settings.DefaultProvider = latest.DefaultProvider;
            _key.Clear();
            RefreshStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            MessageBox.Show(this, "保存失败，请检查 API 配置和用户目录权限。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Delete()
    {
        if (MessageBox.Show(this, "删除当前服务商保存的 API Key？", "确认删除", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { _credentials.DeleteCredential(SelectedProvider); _key.Clear(); RefreshStatus(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            MessageBox.Show(this, "删除失败，请检查用户目录权限。", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
