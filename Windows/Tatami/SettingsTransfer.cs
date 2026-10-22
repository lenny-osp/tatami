using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Tatami.Core;

namespace Tatami;

/// <summary>設定畫面「匯出設定…」「匯入設定…」按鈕的流程：選檔案、確認、顯示結果。</summary>
public static class SettingsTransfer
{
    private const string Filter = "JSON (*.json)|*.json";

    public static void Export(AppSettings settings, Window? owner)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Filter,
            DefaultExt = ".json",
            FileName = Loc.L("backup.defaultFileName") + ".json",
        };
        if (dialog.ShowDialog(owner) != true) return;

        try
        {
            // 先寫暫存檔再取代，失敗時不會留下寫了一半的檔案
            var temporary = dialog.FileName + ".tmp";
            File.WriteAllText(temporary, settings.MakeBackup().ToJson());
            File.Move(temporary, dialog.FileName, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Alert.Show(owner, Loc.L("backup.exportError.title"), exception.Message);
        }
    }

    public static void Import(AppSettings settings, Window? owner)
    {
        var dialog = new OpenFileDialog { Filter = Filter, Multiselect = false };
        if (dialog.ShowDialog(owner) != true) return;
        var fileName = Path.GetFileName(dialog.FileName);

        SettingsBackup backup;
        try
        {
            backup = SettingsBackup.FromJson(File.ReadAllText(dialog.FileName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Alert.Show(owner, Loc.L("backup.error.title"), Loc.L("backup.error.message", fileName));
            return;
        }

        // 匯入會蓋掉目前的設定，先確認
        if (!Alert.Show(owner, Loc.L("backup.import.confirm.title"),
                Loc.L("backup.import.confirm.message", fileName), Loc.L("backup.import.confirm.button")))
        {
            return;
        }

        settings.Apply(backup);
        // 語言可能已經換了，成功訊息用新的語言顯示
        Alert.Show(owner, Loc.L("backup.import.success"), "");
    }
}
