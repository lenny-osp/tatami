import Foundation

/// App 支援的介面語言。rawValue 對應 Resources/<rawValue>.lproj 資料夾。
enum AppLanguage: String, CaseIterable, Identifiable {
    case english = "en"
    case traditionalChinese = "zh-Hant"
    case simplifiedChinese = "zh-Hans"
    case french = "fr"
    case german = "de"
    case spanish = "es"
    case hindi = "hi"
    case japanese = "ja"
    case korean = "ko"
    case arabic = "ar"

    var id: String { rawValue }

    /// 在語言選單中用該語言本身的寫法顯示，看不懂目前介面的人也找得到自己的語言
    var nativeName: String {
        switch self {
        case .english: "English"
        case .traditionalChinese: "繁體中文"
        case .simplifiedChinese: "简体中文"
        case .french: "Français"
        case .german: "Deutsch"
        case .spanish: "Español"
        case .hindi: "हिन्दी"
        case .japanese: "日本語"
        case .korean: "한국어"
        case .arabic: "العربية"
        }
    }

    var isRightToLeft: Bool { self == .arabic }
}

/// 依照使用者在 App 裡選的語言取出翻譯（不跟隨系統語言，切換後立即生效）。
@MainActor
enum Localizer {
    static var language: AppLanguage = .english {
        didSet { bundle = bundle(for: language) }
    }

    private static var bundle = bundle(for: .english)
    private static let englishBundle = bundle(for: .english)

    static func string(_ key: String) -> String {
        // 找不到翻譯時退回英文，英文也沒有就顯示 key，方便發現漏翻
        let english = englishBundle.localizedString(forKey: key, value: key, table: nil)
        return bundle.localizedString(forKey: key, value: english, table: nil)
    }

    private static func bundle(for language: AppLanguage) -> Bundle {
        Bundle.main.path(forResource: language.rawValue, ofType: "lproj").flatMap(Bundle.init(path:)) ?? .main
    }
}

/// 取得目前語言的翻譯，例如 `L("menu.quit")`。
@MainActor
func L(_ key: String) -> String {
    Localizer.string(key)
}

/// 含有 %d 等參數的翻譯，例如 `L("grid.footer", 6, 6)`。
@MainActor
func L(_ key: String, _ arguments: CVarArg...) -> String {
    String(format: Localizer.string(key), arguments: arguments)
}
