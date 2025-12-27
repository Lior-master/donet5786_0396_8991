using PL.Courier;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace PL.Order;

/// <summary>
/// Interaction logic for OrderListWindow.xaml
/// </summary>
public partial class OrderListWindow : Window
{
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();
    public OrderListWindow()
    {
        InitializeComponent();
    }
    public IEnumerable<BO.OrderInList> OrderList
    {
        get { return (IEnumerable<BO.OrderInList>)GetValue(OrderListProperty); }
        set { SetValue(OrderListProperty, value); }
    }


    public static readonly DependencyProperty OrderListProperty =
        DependencyProperty.Register("OrderList", typeof(IEnumerable<BO.OrderInList>), typeof(OrderListWindow), new PropertyMetadata(null));

    public BO.OrderStatus OrderStatus { get; set; } = BO.OrderStatus.All;

    private void queryOrderList()
    {
        OrderList = (OrderStatus == BO.OrderStatus.All) ?
            s_bl?.Order.orderInLists(347657991,null, null, null)! : s_bl?.Order.orderInLists(347657991, OrderStatus, null, null)!;
    }

    private void courseListObserver()
        => queryOrderList();
 
    private void Window_Loaded(object sender, RoutedEventArgs e)
    => s_bl.Order.AddObserver(courseListObserver);

    private void Window_Closed(object sender, EventArgs e)
        => s_bl.Order.RemoveObserver(courseListObserver);

}
