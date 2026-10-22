using System.Globalization;
using System.Text;

namespace Tatami.Core;

/// <summary>App 支援的介面語言。Code 對應 Resources/&lt;Code&gt;.lproj 資料夾。</summary>
public sealed record AppLanguage(string Code, string NativeName)
{
    public static AppLanguage English { get; } = new("en", "English");

    /// <summary>在語言選單中用該語言本身的寫法顯示，看不懂目前介面的人也找得到自己的語言</summary>
    public static IReadOnlyList<AppLanguage> All { get; } =
    [
        English,
        new("zh-Hant", "繁體中文"),
        new("zh-Hans", "简体中文"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new("hi", "हिन्दी"),
        new("ja", "日本語"),
        new("ko", "한국어"),
        new("ar", "العربية"),
    ];

    public bool IsRightToLeft => Code == "ar";

    /// <summary>依代碼找語言，找不到時是 null。</summary>
    public static AppLanguage? FromCode(string? code) => All.FirstOrDefault(l => l.Code == code);

    public override string ToString() => NativeName;
}

/// <summary>
/// 依照使用者在 App 裡選的語言取出翻譯（不跟隨系統語言，切換後立即生效）。
///
/// 翻譯檔和 macOS 版共用。Windows 版會先找 "&lt;key&gt;.windows"（macOS 的說法不適用時的
/// Windows 版本），找不到才用原本的 key；都找不到時退回英文，英文也沒有就顯示 key，方便發現漏翻。
/// </summary>
public sealed class Localizer
{
    public const string PlatformSuffix = ".windows";

    private readonly Func<string, IReadOnlyDictionary<string, string>> loadTable;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> tables = new();
    private AppLanguage language = AppLanguage.English;

    /// <summary>從資料夾讀取翻譯：&lt;directory&gt;/&lt;code&gt;.lproj/Localizable.strings</summary>
    public Localizer(string localesDirectory)
        : this(code => LoadFile(Path.Combine(localesDirectory, code + ".lproj", "Localizable.strings")))
    {
    }

    /// <summary>自訂讀取方式（測試用）。</summary>
    public Localizer(Func<string, IReadOnlyDictionary<string, string>> loadTable)
    {
        this.loadTable = loadTable;
    }

    /// <summary>目前的介面語言。改變時觸發 LanguageChanged。</summary>
    public AppLanguage Language
    {
        get => language;
        set
        {
            if (language == value) return;
            language = value;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? LanguageChanged;

    /// <summary>取得目前語言的翻譯。</summary>
    public string Get(string key)
    {
        var current = Table(language.Code);
        var english = Table(AppLanguage.English.Code);
        string platformKey = key + PlatformSuffix;
        // 平台專用的說法優先（即使只有英文版），因為 macOS 的操作說明在 Windows 上是錯的
        if (current.TryGetValue(platformKey, out var value)) return value;
        if (english.TryGetValue(platformKey, out value)) return value;
        if (current.TryGetValue(key, out value)) return value;
        if (english.TryGetValue(key, out value)) return value;
        return key;
    }

    /// <summary>含有 %d、%@ 等參數的翻譯，例如 Get("grid.footer", 6, 6)。</summary>
    public string Get(string key, params object?[] arguments) => Format(Get(key), arguments);

    /// <summary>
    /// 處理 .strings 裡 printf 風格的格式：%d %i %u %ld %lld（整數）、%@ %s（字串）、
    /// %f、%%，以及指定位置的 %1$@。數字一律用不分地區的寫法，和 macOS 的 String(format:) 相同。
    /// </summary>
    public static string Format(string format, params object?[] arguments)
    {
        var result = new StringBuilder(format.Length + 16);
        int nextArgument = 0;
        int i = 0;
        while (i < format.Length)
        {
            char ch = format[i];
            if (ch != '%' || i + 1 >= format.Length)
            {
                result.Append(ch);
                i++;
                continue;
            }
            if (format[i + 1] == '%')
            {
                result.Append('%');
                i += 2;
                continue;
            }

            int j = i + 1;
            // 指定位置：%1$@
            int? position = null;
            int digitsStart = j;
            while (j < format.Length && char.IsAsciiDigit(format[j])) j++;
            if (j > digitsStart && j < format.Length && format[j] == '$')
            {
                position = int.Parse(format.AsSpan(digitsStart, j - digitsStart), CultureInfo.InvariantCulture) - 1;
                j++;
            }
            else
            {
                j = digitsStart;
            }
            // 長度修飾字（l、ll、h、q、z、t）不影響輸出
            while (j < format.Length && format[j] is 'l' or 'h' or 'q' or 'z' or 't') j++;
            if (j >= format.Length)
            {
                result.Append(format, i, format.Length - i);
                break;
            }

            char conversion = format[j];
            if (conversion is not ('d' or 'i' or 'u' or '@' or 's' or 'f'))
            {
                // 不認得的格式原樣保留
                result.Append(format, i, j - i + 1);
                i = j + 1;
                continue;
            }

            int index = position ?? nextArgument++;
            object? argument = index >= 0 && index < arguments.Length ? arguments[index] : null;
            result.Append(conversion switch
            {
                'f' => Convert.ToDouble(argument ?? 0, CultureInfo.InvariantCulture).ToString("0.000000", CultureInfo.InvariantCulture),
                _ => Convert.ToString(argument, CultureInfo.InvariantCulture) ?? "",
            });
            i = j + 1;
        }
        return result.ToString();
    }

    private IReadOnlyDictionary<string, string> Table(string code)
    {
        if (!tables.TryGetValue(code, out var table))
        {
            table = loadTable(code);
            tables[code] = table;
        }
        return table;
    }

    /// <summary>讀不到或格式錯誤的翻譯檔當成空的（退回英文），不讓 App 因此無法啟動。</summary>
    private static IReadOnlyDictionary<string, string> LoadFile(string path)
    {
        try
        {
            return StringsFile.Load(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            System.Diagnostics.Debug.WriteLine($"Tatami: cannot load {path}: {exception.Message}");
            return new Dictionary<string, string>();
        }
    }
}
