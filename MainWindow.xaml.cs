using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using OpenCvSharp;

// 解决Window类命名冲突
using WpfWindow = System.Windows.Window;

// 解决命名冲突
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace ThreeKingdomsHeroes;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : WpfWindow
{
    private List<Hero> heroes;
    private bool isF12Active = false;
    private bool isAutoHammerEnabled = false;
    private bool isAutoUltEnabled = false;
    private bool isAutoBaodaboEnabled = false;
    private System.Threading.Timer hammerTimer;
    private System.Threading.Timer ultTimer;
    private System.Threading.Timer processCheckTimer;
    private System.Threading.Timer baodaboTimer;
    private Process gameProcess = null;
    private const int HOTKEY_ID = 9000;
    private const uint MOD_NONE = 0x0000;
    private const uint VK_F12 = 0x7B;
    private const uint VK_F1 = 0x70;
    private const uint VK_W = 0x57;
    private const uint VK_E = 0x45;

    // 全局键盘钩子相关
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private static LowLevelKeyboardProc _proc;
    private static IntPtr _hookID = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;
    private const uint KEYEVENTF_KEYDOWN = 0x00;
    private const uint KEYEVENTF_KEYUP = 0x02;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public MainWindow()
    {
        InitializeComponent();
        InitializeHeroes();
        // 从配置文件读取自动释放功能的状态
        LoadAutoReleaseSettings();
        // 设置全局键盘钩子
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    private void LoadAutoReleaseSettings()
    {
        // 从app.config读取自动释放锤子的状态
        string autoHammerEnabled = ConfigurationManager.AppSettings["AutoHammerEnabled"];
        if (!string.IsNullOrEmpty(autoHammerEnabled))
        {
            bool enabled = bool.TryParse(autoHammerEnabled, out bool result) && result;
            cbAutoHammer.IsChecked = enabled;
            isAutoHammerEnabled = enabled;
        }

        // 从app.config读取自动释放大招的状态
        string autoUltEnabled = ConfigurationManager.AppSettings["AutoUltEnabled"];
        if (!string.IsNullOrEmpty(autoUltEnabled))
        {
            bool enabled = bool.TryParse(autoUltEnabled, out bool result) && result;
            cbAutoUlt.IsChecked = enabled;
            isAutoUltEnabled = enabled;
        }

        // 从app.config读取自动升级包大伯的状态，默认启用
        string autoBaodaboEnabled = ConfigurationManager.AppSettings["AutoBaodaboEnabled"];
        if (!string.IsNullOrEmpty(autoBaodaboEnabled))
        {
            bool enabled = bool.TryParse(autoBaodaboEnabled, out bool result) && result;
            cbAutoBaodabo.IsChecked = enabled;
            isAutoBaodaboEnabled = enabled;
        }
        else
        {
            // 默认启用
            cbAutoBaodabo.IsChecked = true;
            isAutoBaodaboEnabled = true;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
        if (source != null)
        {
            // 设置窗口样式，确保即使在后台也能接收消息
            source.AddHook(HwndHook);
            // 使用当前窗口句柄注册热键
            bool success = RegisterHotKey(source.Handle, HOTKEY_ID, MOD_NONE, VK_F12);
            // 调试信息
            System.Diagnostics.Debug.WriteLine($"热键注册成功: {success}");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // 在UI线程上执行操作
            this.Dispatcher.Invoke(() =>
            {
                ToggleF12Status();
            });
            handled = true;
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        // 移除全局键盘钩子
        UnhookWindowsHookEx(_hookID);
        
        var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
        if (source != null)
        {
            bool success = UnregisterHotKey(source.Handle, HOTKEY_ID);
            System.Diagnostics.Debug.WriteLine($"热键注销成功: {success}");
        }
        // 停止所有定时器
        StopAllTimers();
        base.OnClosed(e);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == VK_F12)
            {
                // 在UI线程上执行操作
                this.Dispatcher.Invoke(() =>
                {
                    ToggleF12Status();
                });
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private void InitializeHeroes()
    {
        heroes = new List<Hero>
        {
            // 魏国
            new Hero { Name = "于禁", Country = "魏" },
            new Hero { Name = "曹仁", Country = "魏" },
            new Hero { Name = "张辽", Country = "魏" },
            new Hero { Name = "徐晃", Country = "魏" },
            new Hero { Name = "司马懿", Country = "魏" },
            new Hero { Name = "典韦", Country = "魏" },
            new Hero { Name = "夏侯惇", Country = "魏" },
            new Hero { Name = "荀彧", Country = "魏" },
            new Hero { Name = "张郃", Country = "魏" },
            new Hero { Name = "曹丕", Country = "魏" },
            new Hero { Name = "郭嘉", Country = "魏" },
            new Hero { Name = "蔡文姬", Country = "魏" },

            // 蜀国
            new Hero { Name = "张飞", Country = "蜀" },
            new Hero { Name = "赵云", Country = "蜀" },
            new Hero { Name = "庞统", Country = "蜀" },
            new Hero { Name = "徐庶", Country = "蜀" },
            new Hero { Name = "姜维", Country = "蜀" },
            new Hero { Name = "诸葛亮", Country = "蜀" },
            new Hero { Name = "关羽", Country = "蜀" },
            new Hero { Name = "孟获", Country = "蜀" },
            new Hero { Name = "黄月英", Country = "蜀" },
            new Hero { Name = "黄忠", Country = "蜀" },
            new Hero { Name = "马超", Country = "蜀" },
            new Hero { Name = "法正", Country = "蜀" },

            // 吴国
            new Hero { Name = "鲁肃", Country = "吴" },
            new Hero { Name = "小乔", Country = "吴" },
            new Hero { Name = "黄盖", Country = "吴" },
            new Hero { Name = "大乔", Country = "吴" },
            new Hero { Name = "周瑜", Country = "吴" },
            new Hero { Name = "周泰", Country = "吴" },
            new Hero { Name = "孙尚香", Country = "吴" },
            new Hero { Name = "太史慈", Country = "吴" },
            new Hero { Name = "陆逊", Country = "吴" },
            new Hero { Name = "吕蒙", Country = "吴" },
            new Hero { Name = "孙策", Country = "吴" },
            new Hero { Name = "甘宁", Country = "吴" },
            new Hero { Name = "孙坚", Country = "吴" },
            new Hero { Name = "孙玲珑", Country = "吴" },

            // 中立
            new Hero { Name = "公孙瓒", Country = "中立" },
            new Hero { Name = "祝融", Country = "中立" },
            new Hero { Name = "貂蝉", Country = "中立" },
            new Hero { Name = "吕布", Country = "中立" },
            new Hero { Name = "蒲元", Country = "中立" },
            new Hero { Name = "董卓", Country = "中立" },
            new Hero { Name = "高顺", Country = "中立" },
            new Hero { Name = "袁绍", Country = "中立" },
            new Hero { Name = "张角", Country = "中立" }
        };
    }

    private void btnWei_Click(object sender, RoutedEventArgs e)
    {
        var weiHeroes = heroes.Where(h => h.Country == "魏").ToList();
        DisplayHeroes(weiHeroes);
    }

    private void btnShu_Click(object sender, RoutedEventArgs e)
    {
        var shuHeroes = heroes.Where(h => h.Country == "蜀").ToList();
        DisplayHeroes(shuHeroes);
    }

    private void btnWu_Click(object sender, RoutedEventArgs e)
    {
        var wuHeroes = heroes.Where(h => h.Country == "吴").ToList();
        DisplayHeroes(wuHeroes);
    }

    private void btnNeutral_Click(object sender, RoutedEventArgs e)
    {
        var neutralHeroes = heroes.Where(h => h.Country == "中立").ToList();
        DisplayHeroes(neutralHeroes);
    }

    private void DisplayHeroes(List<Hero> heroList)
    {
        wpHeroes.Children.Clear();
        
        if (heroList.Count > 0)
        {
            tbNoData.Visibility = Visibility.Collapsed;
            
            foreach (var hero in heroList)
            {
                Border heroBorder = new Border
                {
                    Background = WpfBrushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#E0E0E0")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(20, 15, 20, 15),
                    Margin = new Thickness(10, 10, 10, 10),
                    Width = 100,
                    Height = 60,
                    Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Opacity = 0.2 }
                };
                
                TextBlock heroText = new TextBlock
                {
                    Text = hero.Name,
                    FontSize = 14,
                    Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#333333")),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                
                heroBorder.Child = heroText;
                wpHeroes.Children.Add(heroBorder);
            }
        }
        else
        {
            tbNoData.Visibility = Visibility.Visible;
        }
    }

    private void btnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

    private void ToggleF12Status()
    {
        isF12Active = !isF12Active;
        UpdateF12StatusText();
        
        if (isF12Active)
        {
            // 启动进程监控
            StartProcessMonitoring();
        }
        else
        {
            // 停止所有定时器
            StopAllTimers();
        }
    }

    private void UpdateF12StatusText()
    {
        if (isF12Active)
        {
            tbF12Status.Text = "F12状态: 已启动";
            tbF12Status.Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4CAF50"));
        }
        else
        {
            tbF12Status.Text = "F12状态: 未启动";
            tbF12Status.Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#666666"));
        }
    }

    private void StartHammerTimer()
    {
        if (hammerTimer == null)
        {
            int interval = GetIntervalFromConfig("HammerInterval", 2000);
            hammerTimer = new System.Threading.Timer(CheckProcessAndClick, null, 0, interval);
        }
    }

    private void StopHammerTimer()
    {
        if (hammerTimer != null)
        {
            hammerTimer.Dispose();
            hammerTimer = null;
        }
    }

    private void StartUltTimer()
    {
        if (ultTimer == null)
        {
            int interval = GetIntervalFromConfig("UltInterval", 500);
            ultTimer = new System.Threading.Timer(CheckProcessAndPressF1, null, 0, interval);
        }
    }

    private void StopUltTimer()
    {
        if (ultTimer != null)
        {
            ultTimer.Dispose();
            ultTimer = null;
        }
    }

    private void StartBaodaboTimer()
    {
        if (baodaboTimer == null)
        {
            int interval = GetIntervalFromConfig("BaodaboCheckInterval", 5000);
            baodaboTimer = new System.Threading.Timer(CheckBaodaboIcons, null, 0, interval);
        }
    }

    private void StopBaodaboTimer()
    {
        if (baodaboTimer != null)
        {
            baodaboTimer.Dispose();
            baodaboTimer = null;
        }
    }

    private int GetIntervalFromConfig(string key, int defaultValue)
    {
        string intervalStr = ConfigurationManager.AppSettings[key];
        if (int.TryParse(intervalStr, out int interval) && interval > 0)
        {
            return interval;
        }
        return defaultValue;
    }

    private void StartProcessMonitoring()
    {
        // 启动时检查进程
        if (CheckAndSubscribeProcess())
        {
            // 进程存在，开始执行操作
            StartActiveTimers();
        }
        else
        {
            // 进程不存在，启动较低频率的轮询检查
            StartProcessCheckTimer();
        }
    }

    private bool CheckAndSubscribeProcess()
    {
        // 检查TDClient或TDClient.bin进程
        Process[] processes = Process.GetProcessesByName("TDClient");
        if (processes.Length == 0)
        {
            processes = Process.GetProcessesByName("TDClient.bin");
        }

        if (processes.Length > 0)
        {
            // 找到进程，订阅Exited事件
            gameProcess = processes[0];
            gameProcess.EnableRaisingEvents = true;
            gameProcess.Exited += GameProcess_Exited;
            return true;
        }
        return false;
    }

    private void GameProcess_Exited(object sender, EventArgs e)
    {
        // 进程退出，停止所有定时器
        StopAllTimers();
        // 开始轮询检查进程
        StartProcessCheckTimer();
    }

    private void StartProcessCheckTimer()
    {
        // 启动较低频率的轮询检查（每5秒）
        if (processCheckTimer == null)
        {
            processCheckTimer = new System.Threading.Timer(CheckProcessPeriodically, null, 0, 5000);
        }
    }

    private void CheckProcessPeriodically(object state)
    {
        if (CheckAndSubscribeProcess())
        {
            // 进程存在，停止轮询检查
            StopProcessCheckTimer();
            // 开始执行操作
            StartActiveTimers();
        }
    }

    private void StopProcessCheckTimer()
    {
        if (processCheckTimer != null)
        {
            processCheckTimer.Dispose();
            processCheckTimer = null;
        }
    }

    private void StartActiveTimers()
    {
        // 开始所有激活的定时器
        if (isAutoHammerEnabled && isF12Active)
        {
            StartHammerTimer();
        }
        if (isAutoUltEnabled && isF12Active)
        {
            StartUltTimer();
        }
        if (isAutoBaodaboEnabled && isF12Active)
        {
            StartBaodaboTimer();
        }
    }

    private void StopAllTimers()
    {
        // 停止所有定时器
        StopHammerTimer();
        StopUltTimer();
        StopBaodaboTimer();
        StopProcessCheckTimer();
    }

    private void CheckProcessAndClick(object state)
    {
        // 直接执行操作，因为我们已经通过其他方式确保进程存在
        // 模拟右键点击
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
    }

    private void CheckProcessAndPressF1(object state)
    {
        // 直接执行操作，因为我们已经通过其他方式确保进程存在
        // 模拟F1键按下
        keybd_event((byte)VK_F1, 0, KEYEVENTF_KEYDOWN, 0);
        Thread.Sleep(50);
        keybd_event((byte)VK_F1, 0, KEYEVENTF_KEYUP, 0);
    }

    private void cbAutoHammer_Checked(object sender, RoutedEventArgs e)
    {
        isAutoHammerEnabled = true;
        // 保存到配置文件
        SaveAutoSetting("AutoHammerEnabled", true);
        if (isF12Active)
        {
            // 重新启动进程监控，确保使用新的设置
            StopAllTimers();
            StartProcessMonitoring();
        }
    }

    private void cbAutoHammer_Unchecked(object sender, RoutedEventArgs e)
    {
        isAutoHammerEnabled = false;
        // 保存到配置文件
        SaveAutoSetting("AutoHammerEnabled", false);
        StopHammerTimer();
    }

    private void cbAutoUlt_Checked(object sender, RoutedEventArgs e)
    {
        isAutoUltEnabled = true;
        // 保存到配置文件
        SaveAutoSetting("AutoUltEnabled", true);
        if (isF12Active)
        {
            // 重新启动进程监控，确保使用新的设置
            StopAllTimers();
            StartProcessMonitoring();
        }
    }

    private void cbAutoUlt_Unchecked(object sender, RoutedEventArgs e)
    {
        isAutoUltEnabled = false;
        // 保存到配置文件
        SaveAutoSetting("AutoUltEnabled", false);
        StopUltTimer();
    }

    private void SaveAutoSetting(string key, bool enabled)
    {
        try
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = enabled.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存自动设置失败 [{key}]: {ex.Message}");
        }
    }



    private void cbAutoBaodabo_Checked(object sender, RoutedEventArgs e)
    {
        isAutoBaodaboEnabled = true;
        // 保存到配置文件
        SaveAutoSetting("AutoBaodaboEnabled", true);
        if (isF12Active)
        {
            // 重新启动进程监控，确保使用新的设置
            StopAllTimers();
            StartProcessMonitoring();
        }
    }

    private void cbAutoBaodabo_Unchecked(object sender, RoutedEventArgs e)
    {
        isAutoBaodaboEnabled = false;
        // 保存到配置文件
        SaveAutoSetting("AutoBaodaboEnabled", false);
        StopBaodaboTimer();
    }



    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            ToggleF12Status();
            e.Handled = true;
        }
    }

    private void CheckBaodaboIcons(object state)
    {
        try
        {
            // 优先检测包大伯建造图标
            bool foundBuild = CheckImageOnScreen(@"d:\AutoBakNoDelete\Desktop\塔防江山谱DH_JSP\ThreeKingdomsHeroes\photos\包大伯.png");
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 检测到包大伯建造图标: {foundBuild}");
            
            if (foundBuild)
            {
                // 模拟按下W键
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 按下W键建造包大伯");
                keybd_event((byte)VK_W, 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(50);
                keybd_event((byte)VK_W, 0, KEYEVENTF_KEYUP, 0);
                return;
            }
            
            // 检测包大伯升级图标
            bool foundUpgrade = CheckImageOnScreen(@"d:\AutoBakNoDelete\Desktop\塔防江山谱DH_JSP\ThreeKingdomsHeroes\photos\包大伯升级.png");
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 检测到包大伯升级图标: {foundUpgrade}");
            
            if (foundUpgrade)
            {
                // 模拟按下E键
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 按下E键升级包大伯");
                keybd_event((byte)VK_E, 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(50);
                keybd_event((byte)VK_E, 0, KEYEVENTF_KEYUP, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 检测包大伯图标失败: {ex.Message}");
        }
    }

    private bool CheckImageOnScreen(string imagePath)
    {
        try
        {
            // 确保图像文件存在
            if (!System.IO.File.Exists(imagePath))
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 图像文件不存在: {imagePath}");
                return false;
            }
            
            // 加载模板图像
            using (var template = Cv2.ImRead(imagePath, ImreadModes.Color))
            {
                if (template.Empty())
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 无法加载模板图像: {imagePath}");
                    return false;
                }
                
                // 捕获屏幕
                using (var screenBmp = new Drawing.Bitmap(Forms.Screen.PrimaryScreen.Bounds.Width, Forms.Screen.PrimaryScreen.Bounds.Height))
                {
                    using (var g = Drawing.Graphics.FromImage(screenBmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);
                    }
                    
                    // 将Bitmap保存到临时文件
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"screen_{DateTime.Now.ToString("HHmmssfff")}.bmp");
                    screenBmp.Save(tempPath, Drawing.Imaging.ImageFormat.Bmp);
                    
                    // 加载屏幕图像
                    using (var screenMat = Cv2.ImRead(tempPath, ImreadModes.Color))
                    {
                        // 删除临时文件
                        System.IO.File.Delete(tempPath);
                        
                        if (screenMat.Empty())
                        {
                            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 无法加载屏幕图像");
                            return false;
                        }
                        
                        // 转换为灰度图像
                        using (var grayScreen = new Mat())
                        using (var grayTemplate = new Mat())
                        {
                            Cv2.CvtColor(screenMat, grayScreen, ColorConversionCodes.BGR2GRAY);
                            Cv2.CvtColor(template, grayTemplate, ColorConversionCodes.BGR2GRAY);
                            
                            // 直方图均衡化，提高对比度
                            using (var equalizedScreen = new Mat())
                            {
                                Cv2.EqualizeHist(grayScreen, equalizedScreen);
                                
                                // 多尺度匹配
                                double minVal = double.MaxValue;
                                OpenCvSharp.Point minLoc = new OpenCvSharp.Point();
                                double maxVal = double.MinValue;
                                OpenCvSharp.Point maxLoc = new OpenCvSharp.Point();
                                
                                // 尝试不同的缩放比例
                                for (double scale = 0.8; scale <= 1.2; scale += 0.1)
                                {
                                    using (var resizedTemplate = new Mat())
                                    {
                                        Cv2.Resize(grayTemplate, resizedTemplate, new OpenCvSharp.Size(), scale, scale);
                                        
                                        // 确保模板尺寸小于屏幕尺寸
                                        if (resizedTemplate.Rows > equalizedScreen.Rows || resizedTemplate.Cols > equalizedScreen.Cols)
                                        {
                                            continue;
                                        }
                                        
                                        using (var result = new Mat())
                                        {
                                            // 使用多种匹配方法
                                            OpenCvSharp.TemplateMatchModes[] methods = { 
                                                OpenCvSharp.TemplateMatchModes.SqDiffNormed, 
                                                OpenCvSharp.TemplateMatchModes.CCorrNormed, 
                                                OpenCvSharp.TemplateMatchModes.CCoeffNormed 
                                            };
                                            
                                            foreach (var method in methods)
                                            {
                                                Cv2.MatchTemplate(equalizedScreen, resizedTemplate, result, method);
                                                Cv2.MinMaxLoc(result, out double methodMinVal, out double methodMaxVal, out OpenCvSharp.Point methodMinLoc, out OpenCvSharp.Point methodMaxLoc);
                                                
                                                // 记录最佳匹配
                                                if (method == OpenCvSharp.TemplateMatchModes.SqDiffNormed)
                                                {
                                                    if (methodMinVal < minVal)
                                                    {
                                                        minVal = methodMinVal;
                                                        minLoc = methodMinLoc;
                                                    }
                                                }
                                                else
                                                {
                                                    if (methodMaxVal > maxVal)
                                                    {
                                                        maxVal = methodMaxVal;
                                                        maxLoc = methodMaxLoc;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                // 检查是否找到匹配
                                bool foundMatch = false;
                                
                                // 对于SqDiffNormed，值越小越好
                                if (minVal < 0.05)
                                {
                                    foundMatch = true;
                                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 使用SqDiffNormed找到匹配，值: {minVal:F4}");
                                }
                                // 对于其他方法，值越大越好
                                else if (maxVal > 0.95)
                                {
                                    foundMatch = true;
                                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 使用其他方法找到匹配，值: {maxVal:F4}");
                                }
                                
                                if (foundMatch)
                                {
                                    // 保存检测到的彩色图像，以便调试
                                    /* 调试开始
                                    try
                                    {
                                        // 创建保存目录
                                        string saveDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(imagePath), "detected");
                                        if (!System.IO.Directory.Exists(saveDir))
                                        {
                                            System.IO.Directory.CreateDirectory(saveDir);
                                        }
                                        
                                        // 提取匹配区域（从彩色屏幕图像中提取）
                                        int templateWidth = grayTemplate.Cols;
                                        int templateHeight = grayTemplate.Rows;
                                        var matchRegion = new OpenCvSharp.Rect(maxLoc.X, maxLoc.Y, templateWidth, templateHeight);
                                        var regionMat = screenMat[matchRegion];
                                        
                                        // 保存彩色图像
                                        string fileName = System.IO.Path.GetFileNameWithoutExtension(imagePath);
                                        string savePath = System.IO.Path.Combine(saveDir, $"{fileName}_{DateTime.Now.ToString("HHmmssfff")}.png");
                                        Cv2.ImWrite(savePath, regionMat);
                                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 检测到的彩色图像已保存到: {savePath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"保存检测图像失败: {ex.Message}");
                                    }
                                    调试结束 */
                                    
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] 检测图像失败: {ex.Message}");
        }
        
        return false;
    }
}

public class Hero
{
    public string Name { get; set; }
    public string Country { get; set; }
}