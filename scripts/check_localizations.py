#!/usr/bin/env python3
# 檢查所有語言的 Localizable.strings：格式正確、key 和英文版完全一致，
# 而且程式碼（macOS 的 Sources/、Windows 的 Windows/）裡用到的每個 key 都有翻譯。
#
# 只用 Python 標準函式庫，macOS 和 Windows 都能直接執行：
#   python3 scripts/check_localizations.py      （macOS）
#   python scripts/check_localizations.py       （Windows）
# 不依賴 plutil，所以自己解析 Apple 的 .strings 格式。
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "Resources"
ENGLISH = RESOURCES / "en.lproj" / "Localizable.strings"

# Windows 的 Localizer 會先找 "<key>.windows"，找不到才用原本的 key
WINDOWS_SUFFIX = ".windows"

# 對應的跳脫字元：\" \\ \n 等
ESCAPES = {'"': '"', "\\": "\\", "'": "'", "n": "\n", "t": "\t", "r": "\r", "0": "\0"}


class StringsError(Exception):
    """格式錯誤，附上檔案和行號。"""

    def __init__(self, path, line, message):
        super().__init__(f"{path}:{line}: {message}")


class StringsParser:
    """解析 Apple .strings：每一項是 "key" = "value";，可以有 // 和 /* */ 註解與空行。"""

    def __init__(self, path, text):
        self.path = path
        self.text = text
        self.pos = 0

    def line(self, pos=None):
        return self.text.count("\n", 0, self.pos if pos is None else pos) + 1

    def fail(self, message, pos=None):
        raise StringsError(self.path, self.line(pos), message)

    def skip_space_and_comments(self):
        text = self.text
        while self.pos < len(text):
            ch = text[self.pos]
            if ch.isspace():
                self.pos += 1
            elif text.startswith("//", self.pos):
                end = text.find("\n", self.pos)
                self.pos = len(text) if end < 0 else end + 1
            elif text.startswith("/*", self.pos):
                end = text.find("*/", self.pos + 2)
                if end < 0:
                    self.fail("註解 /* 沒有對應的 */")
                self.pos = end + 2
            else:
                return

    def expect(self, ch, what):
        self.skip_space_and_comments()
        if self.pos >= len(self.text):
            self.fail(f"檔案在預期 {what} 的地方結束了")
        if self.text[self.pos] != ch:
            self.fail(f"預期 {what}，但看到 {self.text[self.pos]!r}")
        self.pos += 1

    def string(self, what):
        self.skip_space_and_comments()
        if self.pos >= len(self.text) or self.text[self.pos] != '"':
            found = repr(self.text[self.pos]) if self.pos < len(self.text) else "檔案結尾"
            self.fail(f"預期用雙引號包起來的{what}，但看到 {found}")
        start = self.pos
        self.pos += 1
        out = []
        text = self.text
        while True:
            if self.pos >= len(text):
                self.fail(f"{what}的雙引號沒有結束", start)
            ch = text[self.pos]
            if ch == '"':
                self.pos += 1
                return "".join(out)
            if ch == "\\":
                if self.pos + 1 >= len(text):
                    self.fail("反斜線後面沒有字元")
                esc = text[self.pos + 1]
                if esc in ESCAPES:
                    out.append(ESCAPES[esc])
                    self.pos += 2
                elif esc in "Uu":
                    digits = text[self.pos + 2 : self.pos + 6]
                    if not re.fullmatch(r"[0-9A-Fa-f]{4}", digits):
                        self.fail(f"\\{esc} 後面要接 4 個十六進位數字")
                    out.append(chr(int(digits, 16)))
                    self.pos += 6
                else:
                    self.fail(f"不認得的跳脫字元 \\{esc}（雙引號寫成 \\\"，換行寫成 \\n）")
            else:
                out.append(ch)
                self.pos += 1

    def parse(self):
        entries = {}
        while True:
            self.skip_space_and_comments()
            if self.pos >= len(self.text):
                return entries
            key_line = self.line()
            key = self.string("key")
            self.expect("=", "=")
            value = self.string("翻譯")
            self.expect(";", "分號 ;")
            if key in entries:
                raise StringsError(self.path, key_line, f"key 重複: {key}")
            entries[key] = value


def read_text(path):
    data = path.read_bytes()
    # .strings 可以是 UTF-8 或 UTF-16（有 BOM）
    if data.startswith((b"\xff\xfe", b"\xfe\xff")):
        return data.decode("utf-16")
    return data.decode("utf-8-sig")


def load(path):
    rel = path.relative_to(ROOT).as_posix()
    try:
        return StringsParser(rel, read_text(path)).parse()
    except UnicodeDecodeError as error:
        print(f"✗ {rel}: 格式錯誤\n    不是 UTF-8 或 UTF-16 編碼（{error}）")
    except StringsError as error:
        print(f"✗ {rel}: 格式錯誤\n    {error}")
    sys.exit(1)


def source_files():
    """macOS 的 Swift 程式碼，以及 Windows 的 C# 和 XAML（略過 bin/、obj/ 編譯產物）。"""
    yield from sorted((ROOT / "Sources").rglob("*.swift"))
    windows = ROOT / "Windows"
    for pattern in ("*.cs", "*.xaml"):
        for path in sorted(windows.rglob(pattern)):
            parts = path.relative_to(windows).parts
            if "bin" in parts or "obj" in parts:
                continue
            yield path


def main():
    # Windows 的 CI 把輸出導到檔案時預設不是 UTF-8，✓ 和中文會讓 print 出錯
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")

    base = load(ENGLISH)
    failed = False
    for path in sorted(RESOURCES.glob("*.lproj/Localizable.strings")):
        rel = path.relative_to(ROOT).as_posix()
        keys = set(load(path))
        missing = set(base) - keys
        extra = keys - set(base)
        if missing or extra:
            failed = True
            print(f"✗ {rel}")
            for k in sorted(missing):
                print(f"    缺少: {k}")
            for k in sorted(extra):
                print(f"    多出: {k}")
        else:
            print(f"✓ {rel} ({len(keys)} keys)")

    # 程式碼裡用到的 key 都必須存在於英文版，兩種寫法：
    #   "menu.quit"     Swift 的 L("menu.quit")、C# 的 L("menu.quit")、說明視窗的 "help.grid.title"
    #   [menu.quit]     XAML 的 {Binding [menu.quit], Source=...}
    prefixes = "|".join(map(re.escape, sorted({k.split(".")[0] for k in base})))
    key = r"((?:%s)\.[A-Za-z0-9_.]+)" % prefixes
    patterns = [re.compile(r'"%s"' % key), re.compile(r"\[%s\]" % key)]
    used = set()
    for source in source_files():
        text = source.read_text(encoding="utf-8-sig")
        for pattern in patterns:
            used.update(pattern.findall(text))
    undefined = sorted(used - set(base))
    if undefined:
        failed = True
        print("✗ 程式碼用到但英文翻譯檔沒有的 key:")
        for k in undefined:
            print(f"    {k}")
    else:
        print(f"✓ 程式碼用到的 {len(used)} 個 key 都有翻譯")

    # "<key>.windows" 由 Windows 的 Localizer 自動取用，原本的 key 有用到就算有用到
    unused = sorted(
        k
        for k in set(base) - used
        if not (k.endswith(WINDOWS_SUFFIX) and k[: -len(WINDOWS_SUFFIX)] in used)
    )
    if unused:
        print("  （提醒）翻譯檔裡有、但程式碼沒用到的 key: " + ", ".join(unused))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
