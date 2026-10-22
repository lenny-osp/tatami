#!/bin/bash
# 檢查所有語言的 Localizable.strings：格式正確、key 和英文版完全一致，
# 而且程式碼裡用到的每個 key 都有翻譯。
# 實際的檢查在 check_localizations.py（Windows 上直接執行：python scripts/check_localizations.py）。
exec python3 "$(dirname "$0")/check_localizations.py" "$@"
