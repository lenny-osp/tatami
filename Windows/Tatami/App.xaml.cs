using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tatami.Win32;

namespace Tatami;

/// <summary>
/// App 進入點：只顯示在通知區域，不出現在工作列。
/// 啟動時讀取設定、註冊快捷鍵、開始監聽拖曳，並依設定檢查更新。
/// </summary>
public partial class App : Application
{
    /// <summary>單一執行個體用的 mutex 名稱（安裝程式也可以用它判斷 Tatami 是否在執行）</summary>
    public const string MutexName = "Tatami.SingleInstance";

    private Mutex? instanceMutex;
    private SnapController? snapController;
    private GridController? gridController;
    private ShortcutController? shortcutController;
    private ForegroundTracker? foregroundTracker;
    private Updater? updater;
    private TrayIcon? trayIcon;
    private IntPtr trayIconHandle;

    // 選單在 OnStartup 才建立：要等 Fluent 主題的資源載入後，才會套用新的樣式
    private ContextMenu menu = null!;
    private MenuItem showGridItem = null!;
    private MenuItem settingsItem = null!;
    private MenuItem helpItem = null!;
    private MenuItem aboutItem = null!;
    private MenuItem checkForUpdatesItem = null!;
    private MenuItem quitItem = null!;
    /// <summary>打開選單前的前景視窗（選單本身會讓 Tatami 變成前景）</summary>
    private IntPtr menuTarget;

    private SettingsWindow? settingsWindow;
    private HelpWindow? helpWindow;
    private AboutWindow? aboutWindow;

    /// <summary>所有設定。啟動後才有值。</summary>
    public static AppSettings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 已經在執行時，第二次啟動直接結束
        instanceMutex = new Mutex(false, MutexName, out bool createdNew);
        if (!createdNew)
        {
            instanceMutex.Dispose();
            instanceMutex = null;
            Shutdown();
            return;
        }

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Tatami;component/Styles.xaml"),
        });

        // 讀取設定並套用（啟動 Snap、註冊快捷鍵）
        snapController = new SnapController();
        Settings = new AppSettings(snapController, AppSettings.DefaultPath);
        Loc.Instance.Initialize(Path.Combine(AppContext.BaseDirectory, "Locales"), Settings.Language);
        gridController = new GridController(Settings);
        foregroundTracker = new ForegroundTracker();
        shortcutController = new ShortcutController(Settings, gridController);
        updater = new Updater();

        BuildMenu();
        trayIconHandle = TatamiIcon.CreateTrayIcon();
        trayIcon = new TrayIcon("Tatami", trayIconHandle);
        trayIcon.Invoked += OpenMenu;
        trayIcon.AppearanceChanged += RefreshTrayIcon;

        if (Settings.IsAutoUpdateEnabled) updater.CheckInBackground();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
        shortcutController?.Dispose();
        gridController?.Close();
        snapController?.Dispose();
        foregroundTracker?.Dispose();
        updater?.Dispose();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    // MARK: - 通知區域選單

    private void BuildMenu()
    {
        menu = new ContextMenu();
        showGridItem = new MenuItem();
        settingsItem = new MenuItem();
        helpItem = new MenuItem();
        aboutItem = new MenuItem();
        checkForUpdatesItem = new MenuItem();
        quitItem = new MenuItem();

        showGridItem.Click += (_, _) => gridController?.Show(menuTarget);
        settingsItem.Click += (_, _) => ShowSettings();
        helpItem.Click += (_, _) => ShowWindow(ref helpWindow, () => new HelpWindow());
        aboutItem.Click += (_, _) => ShowWindow(ref aboutWindow, () => new AboutWindow());
        checkForUpdatesItem.Click += (_, _) => updater?.CheckForUpdates();
        quitItem.Click += (_, _) => Shutdown();

        menu.Items.Add(showGridItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(helpItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(checkForUpdatesItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);
        menu.Placement = PlacementMode.MousePoint;
    }

    /// <summary>每次打開選單時更新文字，套用目前的語言。</summary>
    private void OpenMenu()
    {
        // 先記住要搬的視窗：點通知區域時工作列已經變成前景，所以用最後一個一般視窗
        menuTarget = foregroundTracker?.TargetWindow() ?? IntPtr.Zero;

        showGridItem.Header = Loc.L("menu.showGrid");
        settingsItem.Header = Loc.L("menu.settings");
        helpItem.Header = Loc.L("menu.help");
        aboutItem.Header = Loc.L("menu.about");
        checkForUpdatesItem.Header = Loc.L("menu.checkForUpdates");
        quitItem.Header = Loc.L("menu.quit");
        menu.FlowDirection = Loc.Instance.FlowDirection;
        menu.Language = Loc.Instance.XmlLanguage;

        // Tatami 要先成為前景程式，點選單外面時選單才會關閉
        trayIcon?.BringToForeground();
        menu.IsOpen = true;
    }

    /// <summary>工作列的淺色／深色或縮放比例變了：重畫圖示。</summary>
    private void RefreshTrayIcon()
    {
        if (trayIcon is null) return;
        var newIcon = TatamiIcon.CreateTrayIcon();
        if (newIcon == IntPtr.Zero) return;
        trayIcon.SetIcon(newIcon);
        if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
        trayIconHandle = newIcon;
    }

    // MARK: - 視窗

    private void ShowSettings()
    {
        // 使用者可能在工作管理員裡改過「啟動應用程式」，重新讀取
        Settings.Refresh();
        ShowWindow(ref settingsWindow, () => new SettingsWindow(Settings));
    }

    /// <summary>打開（或帶到最前面）一個視窗。關閉後下次重新建立。</summary>
    private static void ShowWindow<T>(ref T? window, Func<T> create) where T : Window
    {
        if (window is null)
        {
            var created = create();
            window = created;
            created.Closed += (_, _) => ClearWindow(created);
        }
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();
    }

    private static void ClearWindow(Window window)
    {
        if (Current is not App app) return;
        if (app.settingsWindow == window) app.settingsWindow = null;
        if (app.helpWindow == window) app.helpWindow = null;
        if (app.aboutWindow == window) app.aboutWindow = null;
    }
}
