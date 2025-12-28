using BO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PL.Courier;

/// <summary>
/// Interaction logic for CourierListWindow.xaml
/// </summary>
public partial class CourierListWindow : Window
{
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();
    public CourierListWindow()
    {
        InitializeComponent();
    }

    public IEnumerable<BO.CourierInList> CourierList
    {
        get { return (IEnumerable<BO.CourierInList>)GetValue(CourierListProperty); }
        set { SetValue(CourierListProperty, value); }
    }
    
    public static readonly DependencyProperty CourierListProperty =
        DependencyProperty.Register("CourierList", typeof(IEnumerable<BO.CourierInList>), typeof(CourierListWindow), new PropertyMetadata(null));

    public BO.DeliveryTransport CourierDelivery { get; set; } = BO.DeliveryTransport.All;

    private void queryCourierList()
    {
        try
        {
            int bossId = s_bl.Admin.GetConfig().BossId;
            CourierList = (CourierDelivery == BO.DeliveryTransport.All) ?
                s_bl?.Courier.GetCouriersList(bossId, null, null)! : s_bl?.Courier.GetCouriersList(bossId, null, CourierDelivery)!;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}",
               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CourierList = new List<BO.CourierInList>();
        }
    }

    private void courierListObserver()
        => queryCourierList();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        queryCourierList();
        s_bl.Courier.AddObserver(courierListObserver);
    }

    private void Window_Closed(object sender, EventArgs e)
        => s_bl.Courier.RemoveObserver(courierListObserver);

}
