using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class PromptSettingsForm : Form
{
    private readonly OutfitSettingsStore _store;
    private readonly OutfitAppSettings _settings;
    private readonly ListBox _styleList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Panel _editorHost = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<OutfitStylePresetType, (TextBox Title, TextBox Prompt)> _editors = new();
    private readonly Dictionary<OutfitStylePresetType, (Panel Page, Label Status, Button Enable, Button Disable, Button Clear, Button Preview)> _styleViews = new();
    private readonly Dictionary<OutfitStylePresetType, bool> _enabled = new();
    private readonly Label _saveFeedback = new() { AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
    private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 1800 };

    public PromptSettingsForm(OutfitSettingsStore store)
    {
        _store = store;
        _settings = store.Load();
        Text = "AI 穿搭提示词设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 600);
        Size = new Size(680, 680);
        Font = new Font("Microsoft YaHei UI", 9f);
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); _saveFeedback.Text = string.Empty; };
        Disposed += (_, _) => _feedbackTimer.Dispose();

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(layout);
        layout.Controls.Add(_styleList, 0, 0);
        layout.Controls.Add(_editorHost, 1, 0);

        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All) AddStyleEditor(preset);
        _styleList.SelectedIndexChanged += (_, _) =>
        {
            for (int index = 0; index < OutfitStyleCatalog.All.Count; index++)
                _styleViews[OutfitStyleCatalog.All[index].Type].Page.Visible = index == _styleList.SelectedIndex;
        };
        _styleList.SelectedIndex = 0;

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        Button save = new() { Text = "保存", Width = 90, Height = 32 };
        save.Click += (_, _) => SaveSettings("✓ 已保存");
        Button resetAll = new() { Text = "全部恢复默认", Width = 120, Height = 32 };
        resetAll.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "恢复所有款式的默认标题和风格提示词？", "确认恢复", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
            ResetAllAndSaveForTesting();
        };
        footer.Controls.Add(save);
        footer.Controls.Add(resetAll);
        footer.Controls.Add(_saveFeedback);
        layout.Controls.Add(footer, 0, 1);
        layout.SetColumnSpan(footer, 2);
    }

    private void AddStyleEditor(OutfitStylePreset preset)
    {
        Panel page = new() { Dock = DockStyle.Fill, Visible = false };
        _editorHost.Controls.Add(page);
        TableLayoutPanel panel = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(12) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        page.Controls.Add(panel);
        StyleSetting current = _settings.GetStyle(preset.Type);
        _enabled[preset.Type] = current.IsEnabled;
        Label status = new() { Dock = DockStyle.Fill, ForeColor = Color.DimGray };
        panel.Controls.Add(status, 0, 0);
        panel.Controls.Add(new Label { Text = "风格名称", Dock = DockStyle.Fill }, 0, 1);
        TextBox title = new() { Text = current.Title, MaxLength = 24, Dock = DockStyle.Fill };
        panel.Controls.Add(title, 0, 2);
        panel.Controls.Add(new Label { Text = "风格提示词", Dock = DockStyle.Fill }, 0, 3);
        TextBox prompt = new() { Text = current.Prompt, MaxLength = 6000, Multiline = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        panel.Controls.Add(prompt, 0, 4);
        _editors[preset.Type] = (title, prompt);

        FlowLayoutPanel commands = new() { Dock = DockStyle.Fill, WrapContents = true };
        Button preview = new() { Text = "查看最终提示词", Width = 130, Height = 30 };
        preview.Click += (_, _) => ShowPreview(preset.Type);
        Button reset = new() { Text = "恢复此款默认", Width = 120, Height = 30 };
        reset.Click += (_, _) => ResetStyleAndSaveForTesting(preset.Type);
        Button enable = new() { Text = "启用该款式", Width = 110, Height = 30 };
        enable.Click += (_, _) => SetCustomEnabled(preset.Type, true);
        Button disable = new() { Text = "禁用此款式", Width = 110, Height = 30 };
        disable.Click += (_, _) => SetCustomEnabled(preset.Type, false);
        Button clear = new() { Text = "清空内容", Width = 85, Height = 30 };
        clear.Click += (_, _) => { title.Clear(); prompt.Clear(); };
        commands.Controls.Add(preview);
        commands.Controls.Add(reset);
        if (!OutfitStyleCatalog.IsBuiltIn(preset.Type))
        {
            reset.Visible = false;
            commands.Controls.Add(enable);
            commands.Controls.Add(disable);
            commands.Controls.Add(clear);
        }
        panel.Controls.Add(commands, 0, 5);

        CheckBox expand = new() { Text = "显示系统保护规则（只读）", Dock = DockStyle.Fill };
        panel.Controls.Add(expand, 0, 6);
        TextBox locked = new() { Text = "只能修改衣物；保持人物身份、脸部、身材比例、动作、背景与光影；不修改截图范围外内容。",
            Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Visible = false };
        expand.CheckedChanged += (_, _) => locked.Visible = expand.Checked;
        panel.Controls.Add(locked, 0, 7);
        _styleViews[preset.Type] = (page, status, enable, disable, clear, preview);
        title.TextChanged += (_, _) => UpdateStyleListLabel(preset.Type);
        UpdateEditorState(preset.Type);
    }

    internal string SaveFeedbackForTesting => _saveFeedback.Text;

    internal void SetTitleForTesting(OutfitStylePresetType type, string title) => _editors[type].Title.Text = title;

    internal void SetPromptForTesting(OutfitStylePresetType type, string prompt) => _editors[type].Prompt.Text = prompt;

    internal bool SetCustomEnabledForTesting(OutfitStylePresetType type, bool enabled) => SetCustomEnabled(type, enabled);

    internal IReadOnlyList<string> StyleLabelsForTesting => _styleList.Items.Cast<string>().ToArray();

    internal bool SaveSettingsForTesting() => SaveSettings("✓ 已保存");

    internal bool ResetStyleAndSaveForTesting(OutfitStylePresetType type)
    {
        ResetEditor(type);
        return SaveSettings("✓ 已恢复并保存");
    }

    internal bool ResetAllAndSaveForTesting()
    {
        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All) ResetEditor(preset.Type);
        return SaveSettings("✓ 已全部恢复并保存");
    }

    private bool SaveSettings(string successText)
    {
        OutfitAppSettings latest = _store.Load();
        foreach ((OutfitStylePresetType type, (TextBox title, TextBox prompt)) in _editors)
        {
            if (_enabled[type] && (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(prompt.Text) ||
                title.Text.Length > 24 || prompt.Text.Length > 6000))
            {
                ShowFeedback("保存失败，请重试", false);
                return false;
            }
            latest.SetStyle(type, new StyleSetting
            {
                Title = title.Text.Trim(), Prompt = prompt.Text.Trim(), IsEnabled = _enabled[type]
            });
        }
        try
        {
            _store.Save(latest);
            ShowFeedback(successText, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowFeedback("保存失败，请重试", false);
            return false;
        }
    }

    private void ShowFeedback(string message, bool success)
    {
        _feedbackTimer.Stop();
        _saveFeedback.ForeColor = success ? Color.ForestGreen : Color.Firebrick;
        _saveFeedback.Text = message;
        _feedbackTimer.Start();
    }

    private void ResetEditor(OutfitStylePresetType type)
    {
        StyleSetting defaults = OutfitStyleCatalog.GetDefaultSetting(type);
        (TextBox title, TextBox prompt) = _editors[type];
        title.Text = defaults.Title;
        prompt.Text = defaults.Prompt;
        _enabled[type] = OutfitStyleCatalog.IsBuiltIn(type);
        UpdateEditorState(type);
    }

    private bool SetCustomEnabled(OutfitStylePresetType type, bool enabled)
    {
        if (OutfitStyleCatalog.IsBuiltIn(type)) return false;
        bool previous = _enabled[type];
        _enabled[type] = enabled;
        if (enabled)
        {
            StyleSetting defaults = OutfitStyleCatalog.GetDefaultSetting(type);
            if (string.IsNullOrWhiteSpace(_editors[type].Title.Text)) _editors[type].Title.Text = defaults.Title;
            if (string.IsNullOrWhiteSpace(_editors[type].Prompt.Text)) _editors[type].Prompt.Text = defaults.Prompt;
        }
        UpdateEditorState(type);
        if (SaveSettings("✓ 已保存")) return true;
        _enabled[type] = previous;
        UpdateEditorState(type);
        return false;
    }

    private void UpdateEditorState(OutfitStylePresetType type)
    {
        bool builtIn = OutfitStyleCatalog.IsBuiltIn(type);
        bool enabled = _enabled[type];
        (TextBox title, TextBox prompt) = _editors[type];
        (Panel _, Label status, Button enable, Button disable, Button clear, Button preview) = _styleViews[type];
        status.Text = builtIn ? "内置款式" : enabled ? "自定义款式 · 已启用" : "此款式尚未启用";
        title.Enabled = prompt.Enabled = preview.Enabled = enabled;
        enable.Visible = !builtIn && !enabled;
        disable.Visible = !builtIn && enabled;
        clear.Enabled = enabled;
        UpdateStyleListLabel(type);
    }

    private void UpdateStyleListLabel(OutfitStylePresetType type)
    {
        int index = (int)type;
        string title = _enabled[type] ? _editors[type].Title.Text.Trim() : "[未启用]";
        string label = $"{OutfitStyleCatalog.SlotId(type)} {title}";
        if (index < _styleList.Items.Count) _styleList.Items[index] = label;
        else _styleList.Items.Add(label);
    }

    private void ShowPreview(OutfitStylePresetType type)
    {
        (TextBox title, TextBox prompt) = _editors[type];
        string final = OutfitPromptBuilder.Build(new OutfitPreviewOptions(type),
            new StyleSetting { Title = title.Text, Prompt = prompt.Text });
        using Form preview = new() { Text = "最终提示词（只读）", Size = new Size(680, 600), StartPosition = FormStartPosition.CenterParent };
        preview.Controls.Add(new TextBox { Text = final, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) });
        preview.ShowDialog(this);
    }
}
