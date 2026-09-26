using System.Drawing;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class CustomStyleSlotTests
{
    [Fact]
    public void CatalogHasExactlyEightFixedSlotsWithOnlyBuiltInsEnabled()
    {
        Assert.Equal("ABCDEFGH", string.Concat(OutfitStyleCatalog.All.Select(p => OutfitStyleCatalog.SlotId(p.Type))));
        OutfitAppSettings settings = new();
        Assert.Equal(new[] { "A", "B", "C" }, settings.GetEnabledStyles().Select(p => OutfitStyleCatalog.SlotId(p.Type)));
        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All.Skip(3))
            Assert.False(settings.GetStyle(preset.Type).IsEnabled);
        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All.Skip(3))
            settings.SetStyle(preset.Type, new StyleSetting { Title = "Custom", Prompt = "Adult garment design", IsEnabled = true });
        Assert.Equal(8, settings.GetEnabledStyles().Count);
        Assert.Throws<InvalidOperationException>(() => OutfitStyleCatalog.Get((OutfitStylePresetType)8));
    }

    [Fact]
    public void SettingsLimitSlotsAndResetCustomStylesWithoutChangingBuiltIns()
    {
        OutfitSettingsStore store = new(TempFile("settings.json"));
        OutfitAppSettings settings = store.Load();
        settings.SetStyle(OutfitStylePresetType.Sport, new StyleSetting { Title = "A custom", Prompt = "A custom prompt" });
        settings.SetStyle(OutfitStylePresetType.D, new StyleSetting { Title = "晚礼服", Prompt = "Replace clothing with an evening gown.", IsEnabled = true });
        settings.Styles["I"] = new StyleSetting { Title = "Forbidden", Prompt = "extra", IsEnabled = true };
        store.Save(settings);
        OutfitAppSettings loaded = store.Load();
        Assert.DoesNotContain("I", loaded.Styles.Keys);
        Assert.Equal(2, loaded.Styles.Count);
        Assert.True(loaded.GetStyle(OutfitStylePresetType.Sport).IsEnabled);
        Assert.Equal("晚礼服", loaded.GetStyle(OutfitStylePresetType.D).Title);
        Assert.Contains(loaded.GetEnabledStyles(), p => p.Type == OutfitStylePresetType.D);

        loaded.SetStyle(OutfitStylePresetType.D, new StyleSetting { Title = "晚礼服", Prompt = "An evening gown", IsEnabled = false });
        store.Save(loaded);
        Assert.DoesNotContain(store.Load().GetEnabledStyles(), p => p.Type == OutfitStylePresetType.D);
        loaded.ResetAllStyles();
        store.Save(loaded);
        OutfitAppSettings reset = store.Load();
        Assert.Equal("运动风", reset.GetStyle(OutfitStylePresetType.Sport).Title);
        Assert.Equal(new[] { "A", "B", "C" }, reset.GetEnabledStyles().Select(p => OutfitStyleCatalog.SlotId(p.Type)));
        Assert.Equal(8, reset.Styles.Count);
    }

    [Theory]
    [InlineData(OutfitStylePresetType.D)]
    [InlineData(OutfitStylePresetType.E)]
    [InlineData(OutfitStylePresetType.F)]
    [InlineData(OutfitStylePresetType.G)]
    [InlineData(OutfitStylePresetType.H)]
    public void CustomPromptsKeepCompiledIdentityAndSafetyRules(OutfitStylePresetType type)
    {
        OutfitAppSettings settings = new();
        settings.SetStyle(type, new StyleSetting { Title = "晚礼服", Prompt = "Replace the clothing with a formal gown.", IsEnabled = true });
        string final = OutfitPromptBuilder.Build(new OutfitPreviewOptions(type), settings.GetStyle(type));
        Assert.StartsWith(OutfitPromptBuilder.LockedCommonPrompt, final);
        Assert.Contains("Replace the clothing with a formal gown.", final);
        Assert.EndsWith(OutfitPromptBuilder.LockedSafetySuffix, final);
        Assert.DoesNotContain("sports bra", final, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PickerTracksEnabledCustomSlotsAndTheirSavedTitles()
    {
        OutfitSettingsStore store = new(TempFile("settings.json"));
        OutfitAppSettings settings = store.Load();
        settings.SetStyle(OutfitStylePresetType.D, new StyleSetting { Title = "晚礼服", Prompt = "A formal gown", IsEnabled = true });
        settings.SetStyle(OutfitStylePresetType.F, new StyleSetting { Title = "汉服风", Prompt = "Traditional fashion", IsEnabled = true });
        store.Save(settings);

        RunOnSta(() =>
        {
            using Bitmap source = new(600, 500);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 600, 500), new Bitmap(source), false,
                type => store.Load().GetStyle(type).Title, () => store.Load().GetEnabledStyles());
            overlay.SetSelectionForTesting(new Rectangle(50, 40, 320, 330));
            Assert.Equal(new[] { "A 运动风", "B 泳衣", "C JK穿搭", "D 晚礼服", "F 汉服风" }, overlay.StyleLabelsForTesting);
            Assert.True(overlay.ClickOutfitButtonForTesting());
            Assert.False(overlay.ClickStyleForTesting(OutfitStylePresetType.E));
            overlay.OutfitPreviewStartRequested += (form, style) => style == OutfitStylePresetType.D && form.TryBeginOutfitPreview();
            Assert.True(overlay.ClickStyleForTesting(OutfitStylePresetType.D));
        });

        settings.SetStyle(OutfitStylePresetType.D, new StyleSetting { Title = "晚礼服", Prompt = "A formal gown", IsEnabled = false });
        store.Save(settings);
        RunOnSta(() =>
        {
            using Bitmap source = new(600, 500);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 600, 500), new Bitmap(source), false,
                type => store.Load().GetStyle(type).Title, () => store.Load().GetEnabledStyles());
            Assert.DoesNotContain("D 晚礼服", overlay.StyleLabelsForTesting);
            Assert.Contains("F 汉服风", overlay.StyleLabelsForTesting);
        });
    }

    [Fact]
    public void PromptSettingsCanEnableDisableAndResetCustomSlots()
    {
        OutfitSettingsStore store = new(TempFile("settings.json"));
        RunOnSta(() =>
        {
            using PromptSettingsForm form = new(store);
            Assert.Equal(8, form.StyleLabelsForTesting.Count);
            Assert.Contains("D [未启用]", form.StyleLabelsForTesting);
            Assert.True(form.SetCustomEnabledForTesting(OutfitStylePresetType.D, true));
            form.SetTitleForTesting(OutfitStylePresetType.D, "晚礼服");
            form.SetPromptForTesting(OutfitStylePresetType.D, "Replace clothing with a tailored gown.");
            Assert.True(form.SaveSettingsForTesting());
            Assert.Contains("D 晚礼服", form.StyleLabelsForTesting);
            Assert.Equal("Replace clothing with a tailored gown.", store.Load().GetStyle(OutfitStylePresetType.D).Prompt);
            Assert.True(form.SetCustomEnabledForTesting(OutfitStylePresetType.D, false));
            Assert.False(store.Load().GetStyle(OutfitStylePresetType.D).IsEnabled);
            Assert.Contains("D [未启用]", form.StyleLabelsForTesting);
            Assert.True(form.SetCustomEnabledForTesting(OutfitStylePresetType.D, true));
            Assert.True(form.ResetAllAndSaveForTesting());
            Assert.Equal(3, store.Load().GetEnabledStyles().Count);
        });
    }

    private static string TempFile(string name) => Path.Combine(Path.GetTempPath(),
        "WechatStyleScreenshot-tests", Guid.NewGuid().ToString("N"), name);

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
