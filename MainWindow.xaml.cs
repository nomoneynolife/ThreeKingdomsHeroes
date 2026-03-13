using System;
using System.Collections.Generic;
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

namespace ThreeKingdomsHeroes;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<Hero> heroes;
    private bool isF12Active = false;
    private bool isAutoHammerEnabled = false;
    private bool isAutoUltEnabled = false;
    private System.Threading.Timer hammerTimer;
    private System.Threading.Timer ultTimer;
    private const int HOTKEY_ID = 9000;
    private const uint MOD_NONE = 0x0000;
    private const uint VK_F12 = 0x7B;
    private const uint VK_F1 = 0x70;

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
        // 设置全局键盘钩子
        _proc = HookCallback;
        _hookID = SetHook(_proc);
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
        hammerTimer?.Dispose();
        ultTimer?.Dispose();
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
                    Background = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
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
            if (isAutoHammerEnabled)
            {
                StartHammerTimer();
            }
            if (isAutoUltEnabled)
            {
                StartUltTimer();
            }
        }
        else
        {
            StopHammerTimer();
            StopUltTimer();
        }
    }

    private void UpdateF12StatusText()
    {
        if (isF12Active)
        {
            tbF12Status.Text = "F12状态: 已启动";
            tbF12Status.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        }
        else
        {
            tbF12Status.Text = "F12状态: 未启动";
            tbF12Status.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
        }
    }

    private void StartHammerTimer()
    {
        if (hammerTimer == null)
        {
            hammerTimer = new System.Threading.Timer(CheckProcessAndClick, null, 0, 5000);
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
            ultTimer = new System.Threading.Timer(CheckProcessAndPressF1, null, 0, 500);
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

    private void CheckProcessAndClick(object state)
    {
        // 检查TDClient.bin进程
        bool processExists = Process.GetProcessesByName("TDClient").Length > 0 || Process.GetProcessesByName("TDClient.bin").Length > 0;
        
        if (processExists)
        {
            // 模拟右键点击
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }
    }

    private void CheckProcessAndPressF1(object state)
    {
        // 检查TDClient.bin进程
        bool processExists = Process.GetProcessesByName("TDClient").Length > 0 || Process.GetProcessesByName("TDClient.bin").Length > 0;
        
        if (processExists)
        {
            // 模拟F1键按下
            keybd_event((byte)VK_F1, 0, KEYEVENTF_KEYDOWN, 0);
            Thread.Sleep(50);
            keybd_event((byte)VK_F1, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private void cbAutoHammer_Checked(object sender, RoutedEventArgs e)
    {
        isAutoHammerEnabled = true;
        if (isF12Active)
        {
            StartHammerTimer();
        }
    }

    private void cbAutoHammer_Unchecked(object sender, RoutedEventArgs e)
    {
        isAutoHammerEnabled = false;
        StopHammerTimer();
    }

    private void cbAutoUlt_Checked(object sender, RoutedEventArgs e)
    {
        isAutoUltEnabled = true;
        if (isF12Active)
        {
            StartUltTimer();
        }
    }

    private void cbAutoUlt_Unchecked(object sender, RoutedEventArgs e)
    {
        isAutoUltEnabled = false;
        StopUltTimer();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            ToggleF12Status();
            e.Handled = true;
        }
    }
}

public class Hero
{
    public string Name { get; set; }
    public string Country { get; set; }
}