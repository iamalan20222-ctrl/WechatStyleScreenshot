using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class PromptSettingsForm : Form
{
    private readonly OutfitSettingsStore _store;
    private readonly OutfitAppSettings _settings;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<OutfitStylePresetType, (TextBox Title, TextBox Prompt)> _editors = new();

    public PromptSettingsForm(OutfitSettingsStore store)
    {
        _store = store;
        _settings = store.Load();
        Text = "AI 穿搭提示词设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 600);
        Size = new Size(680, 680);
        Font = new Font("Microsoft YaHei UI", 9f);

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(layout);
        layout.Controls.Add(_tabs, 0, 0);

        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All) AddStyleTab(preset);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        Button save = new() { Text = "保存", Width = 90, Height = 32 };
        save.Click += (_, _) => SaveSettings();
        Button resetAll = new() { Text = "全部恢复默认", Width = 120, Height = 32 };
        resetAll.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "恢复所有款式的默认标题和风格提示词？", "确认恢复", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (OutfitStylePreset preset in OutfitStyleCatalog.All) ResetEditor(preset.Type);
            SaveSettings();
        };
        footer.Controls.Add(save);
        footer.Controls.Add(resetAll);
        layout.Controls.Add(footer, 0, 1);
    }

    private void AddStyleTab(OutfitStylePreset preset)
    {
        TabPage page = new(preset.Label);
        _tabs.TabPages.Add(page);
        TableLayoutPanel panel = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        page.Controls.Add(panel);
        panel.Controls.Add(new Label { Text = "风格名称", Dock = DockStyle.Fill }, 0, 0);
        StyleSetting current = _settings.GetStyle(preset.Type);
        TextBox title = new() { Text = current.Title, MaxLength = 24, Dock = DockStyle.Fill };
        panel.Controls.Add(title, 0, 1);
        panel.Controls.Add(new Label { Text = "风格提示词", Dock = DockStyle.Fill }, 0, 2);
        TextBox prompt = new() { Text = current.Prompt, MaxLength = 6000, Multiline = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        panel.Controls.Add(prompt, 0, 3);
        _editors[preset.Type] = (title, prompt);

        FlowLayoutPanel commands = new() { Dock = DockStyle.Fill };
        Button preview = new() { Text = "查看最终提示词", Width = 130, Height = 30 };
        preview.Click += (_, _) => ShowPreview(preset.Type);
        Button reset = new() { Text = "恢复此款默认", Width = 120, Height = 30 };
        reset.Click += (_, _) => { ResetEditor(preset.Type); SaveSettings(); };
        commands.Controls.Add(preview);
        commands.Controls.Add(reset);
        panel.Controls.Add(commands, 0, 4);

        CheckBox expand = new() { Text = "显示系统保护规则（只读）", Dock = DockStyle.Fill };
        panel.Controls.Add(expand, 0, 5);
        TextBox locked = new() { Text = "只能修改衣物；保持人物身份、脸部、身材比例、动作、背景与光影；不修改截图范围外内容。",
            Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Visible = false };
        expand.CheckedChanged += (_, _) => locked.Visible = expand.Checked;
        panel.Controls.Add(locked, 0, 6);
    }

    private void SaveSettings()
    {
        OutfitAppSettings latest = _store.Load();
        foreach ((OutfitStylePresetType type, (TextBox title, TextBox prompt)) in _editors)
        {
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(prompt.Text))
            {
                MessageBox.Show(this, "风格名称和提示词不能为空。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            latest.SetStyle(type, new StyleSetting { Title = title.Text.Trim(), Prompt = prompt.Text.Trim() });
        }
        try { _store.Save(latest); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "保存设置失败，请检查用户目录权限。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetEditor(OutfitStylePresetType type)
    {
        StyleSetting defaults = OutfitStyleCatalog.GetDefaultSetting(type);
        (TextBox title, TextBox prompt) = _editors[type];
        title.Text = defaults.Title;
        prompt.Text = defaults.Prompt;
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
