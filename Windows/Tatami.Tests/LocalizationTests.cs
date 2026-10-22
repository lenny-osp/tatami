using Tatami.Core;

namespace Tatami.Tests;

/// <summary>用真正的翻譯檔（Resources/*.lproj）測試，和 macOS 版共用同一份。</summary>
public class StringsFileTests
{
    /// <summary>從測試組件所在的資料夾往上找，直到找到放翻譯檔的 Resources 資料夾。</summary>
    public static string ResourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Resources");
            if (File.Exists(Path.Combine(candidate, "en.lproj", "Localizable.strings"))) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Resources/en.lproj not found above " + AppContext.BaseDirectory);
    }

    public static TheoryData<string> Languages() => new(AppLanguage.All.Select(l => l.Code));

    private static Dictionary<string, string> LoadLanguage(string code) =>
        StringsFile.Load(Path.Combine(ResourcesDirectory(), code + ".lproj", "Localizable.strings"));

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryLanguageHasExactlyTheEnglishKeys(string code)
    {
        var english = LoadLanguage("en").Keys.ToHashSet();
        var keys = LoadLanguage(code).Keys.ToHashSet();
        Assert.Empty(english.Except(keys).Order());
        Assert.Empty(keys.Except(english).Order());
    }

    [Fact]
    public void EveryLanguageFolderIsSupported()
    {
        // Resources 裡的每個 .lproj 都要出現在語言選單裡，反之亦然
        var folders = Directory.GetDirectories(ResourcesDirectory(), "*.lproj")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order();
        Assert.Equal(AppLanguage.All.Select(l => l.Code).Order(), folders);
    }

    [Theory]
    [InlineData("help.snap.body")]
    [InlineData("help.permission.title")]
    [InlineData("help.permission.body")]
    [InlineData("help.updates.body")]
    [InlineData("help.backup.body")]
    [InlineData("about.credits")]
    public void WindowsVariantsExistForMacOnlyTexts(string key)
    {
        var english = LoadLanguage("en");
        Assert.True(english.ContainsKey(key + Localizer.PlatformSuffix), key + Localizer.PlatformSuffix);
    }

    [Fact]
    public void ParsesEscapesAndComments()
    {
        const string text = """
            // 註解
            /* 多行
               註解 */
            "a" = "say \"hi\"\nnext";
            "b"="back\\slash \U00e9";  // 行尾註解
            """;
        var table = StringsFile.Parse(text);
        Assert.Equal("say \"hi\"\nnext", table["a"]);
        Assert.Equal("back\\slash é", table["b"]);
    }

    [Fact]
    public void ReportsTheLineOfAFormatError()
    {
        var error = Assert.Throws<FormatException>(() => StringsFile.Parse("\"a\" = \"x\";\n\"b\" = \"y\"\n", "test.strings"));
        Assert.Contains("test.strings:", error.Message);
        Assert.Throws<FormatException>(() => StringsFile.Parse("\"a\" = \"x\";\n\"a\" = \"y\";"));
    }
}

public class LocalizerTests
{
    private static Localizer Make() => new(code => code switch
    {
        "en" => new Dictionary<string, string>
        {
            ["menu.quit"] = "Quit Tatami",
            ["help.snap.body"] = "macOS text",
            ["help.snap.body.windows"] = "Windows text",
            ["only.english"] = "English only",
        },
        "ja" => new Dictionary<string, string>
        {
            ["menu.quit"] = "Tatami を終了",
            ["help.snap.body"] = "macOS の説明",
            ["help.snap.body.windows"] = "Windows の説明",
        },
        "fr" => new Dictionary<string, string>
        {
            ["help.snap.body"] = "texte macOS",
        },
        _ => new Dictionary<string, string>(),
    });

    [Fact]
    public void UsesTheSelectedLanguage()
    {
        var localizer = Make();
        Assert.Equal("Quit Tatami", localizer.Get("menu.quit"));
        localizer.Language = AppLanguage.FromCode("ja")!;
        Assert.Equal("Tatami を終了", localizer.Get("menu.quit"));
    }

    [Fact]
    public void PrefersTheWindowsVariant()
    {
        var localizer = Make();
        Assert.Equal("Windows text", localizer.Get("help.snap.body"));
        localizer.Language = AppLanguage.FromCode("ja")!;
        Assert.Equal("Windows の説明", localizer.Get("help.snap.body"));
        // 翻譯缺少 Windows 版本時，用英文的 Windows 版本，而不是錯誤的 macOS 說明
        localizer.Language = AppLanguage.FromCode("fr")!;
        Assert.Equal("Windows text", localizer.Get("help.snap.body"));
    }

    [Fact]
    public void FallsBackToEnglishThenToTheKey()
    {
        var localizer = Make();
        localizer.Language = AppLanguage.FromCode("ja")!;
        Assert.Equal("English only", localizer.Get("only.english"));
        Assert.Equal("missing.key", localizer.Get("missing.key"));
    }

    [Fact]
    public void RaisesLanguageChanged()
    {
        var localizer = Make();
        int count = 0;
        localizer.LanguageChanged += (_, _) => count++;
        localizer.Language = AppLanguage.FromCode("ja")!;
        localizer.Language = AppLanguage.FromCode("ja")!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void FormatHandlesIntegersAndObjects()
    {
        Assert.Equal("6 × 4 grid", Localizer.Format("%d × %d grid", 6, 4));
        Assert.Equal("“a.json” x", Localizer.Format("“%@” x", "a.json"));
        Assert.Equal("12 items", Localizer.Format("%ld items", 12L));
        Assert.Equal("100%", Localizer.Format("100%%"));
        Assert.Equal("b a", Localizer.Format("%2$@ %1$@", "a", "b"));
        // 參數不夠時不要當掉
        Assert.Equal("x  y", Localizer.Format("x %@ y"));
    }

    [Fact]
    public void FormatUsesInvariantDigits()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("1234", Localizer.Format("%d", 1234));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void RealFilesFormatGridFooterInEveryLanguage()
    {
        var localizer = new Localizer(StringsFileTests.ResourcesDirectory());
        foreach (var language in AppLanguage.All)
        {
            localizer.Language = language;
            var text = localizer.Get("grid.footer", 7, 3);
            Assert.Contains("7", text);
            Assert.Contains("3", text);
            Assert.DoesNotContain("%", text);
            Assert.NotEqual("grid.footer", text);
        }
    }

    [Fact]
    public void RealFilesUseTheWindowsCredits()
    {
        var localizer = new Localizer(StringsFileTests.ResourcesDirectory());
        Assert.Contains("NetSparkle", localizer.Get("about.credits"));
    }
}
