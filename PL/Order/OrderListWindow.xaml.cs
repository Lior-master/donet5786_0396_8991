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
        try
        {
            // First, try to find any existing courier to use as requester
            var bossId = s_bl.Admin.GetConfig().BossId;

            OrderList = (OrderStatus == BO.OrderStatus.All) ?
                s_bl?.Order.orderInLists(bossId, null, null, null)! : 
                s_bl?.Order.orderInLists(bossId, OrderStatus, null, null)!;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}", 
                           "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            OrderList = new List<BO.OrderInList>();
        }
    }

    private void orderListObserver()
        => queryOrderList();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        queryOrderList();
        try
        {
            s_bl.Order.AddObserver(orderListObserver);
        }
        catch (Exception ex)
        {
            // Some BL implementations might not support observers
            MessageBox.Show($"Observer registration failed: {ex.Message}", 
                           "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        try
        {
            s_bl.Order.RemoveObserver(orderListObserver);
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private void OrderStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderStatus selectedStatus)
        {
            OrderStatus = selectedStatus;
            queryOrderList(); // reload the list based on new filter
        }
    }

    private void dgOrderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        if (dataGrid.SelectedItem is not BO.OrderInList selectedOrder)
            return;

        new OrderWindow(selectedOrder.OrderId).Show();
    }

}
