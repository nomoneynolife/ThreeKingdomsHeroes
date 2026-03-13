using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace ThreeKingdomsHeroes;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<Hero> heroes;

    public MainWindow()
    {
        InitializeComponent();
        InitializeHeroes();
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
}

public class Hero
{
    public string Name { get; set; }
    public string Country { get; set; }
}