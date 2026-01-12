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
    private bool _isOpen = false;

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

    public PL.FilterTypeOrder FilterTypeOrder { get; set; } = PL.FilterTypeOrder.All;

    public BO.OrderStatus OrderStatus { get; set; } = BO.OrderStatus.All;

    // Additional filter properties for the new filtering options
    public BO.OrderType OrderType { get; set; } = BO.OrderType.All;
    public BO.FragilityLevel FragilityLevel { get; set; } = BO.FragilityLevel.All;
    public BO.ScheduleStatus ScheduleStatus { get; set; } = BO.ScheduleStatus.All;

    private void queryOrderList()
    {
        try
        {
            var bossId = s_bl.Admin.GetConfig().BossId;

            switch (FilterTypeOrder)
            {
                case PL.FilterTypeOrder.All:
                    OrderList = s_bl.Order.orderInLists(bossId, null, null, null);
                    break;

                case PL.FilterTypeOrder.ByOrderStatus:
                    OrderList = (OrderStatus == BO.OrderStatus.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.ByOrderStatus, OrderStatus, null);
                    break;

                case PL.FilterTypeOrder.ByOrderType:
                    OrderList = (OrderType == BO.OrderType.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.ByOrderType, OrderType, null);
                    break;

                case PL.FilterTypeOrder.BySheduleStatus:
                    OrderList = (ScheduleStatus == BO.ScheduleStatus.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.BySheduleStatus, ScheduleStatus, null);
                    break;

                default:
                    OrderList = s_bl.Order.orderInLists(bossId, null, null, null);
                    break;
            }
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
        if (_isOpen) return;
        queryOrderList();
        try
        {
            s_bl.Order.AddObserver(orderListObserver);
            _isOpen = true;
        }
        catch (Exception ex)
        {
            // Some BL implementations might not support observers
            MessageBox.Show($"Observer registration failed: {ex.Message}", 
                           "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isOpen = true;
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
        finally
        {
            _isOpen = false;
        }
    }

    private void FilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Skip if window is not fully loaded
        if (!IsLoaded) return;
        
        if (sender is ComboBox comboBox && comboBox.SelectedItem is PL.FilterTypeOrder selectedFilter)
        {
            FilterTypeOrder = selectedFilter;
            
            // Hide all filter controls first
            lblSpecificFilter.Visibility = Visibility.Collapsed;
            cmbOrderStatusFilter.Visibility = Visibility.Collapsed;
            cmbOrderTypeFilter.Visibility = Visibility.Collapsed;
            cmbScheduleStatusFilter.Visibility = Visibility.Collapsed;
            
            // Show the appropriate filter control based on selection
            switch (selectedFilter)
            {
                case PL.FilterTypeOrder.ByOrderStatus:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Order Status:";
                    cmbOrderStatusFilter.Visibility = Visibility.Visible;
                    // Reset to show all orders until a specific status is selected
                    OrderStatus = BO.OrderStatus.All;
                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    break;
                
                case PL.FilterTypeOrder.ByOrderType:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Order Type:";
                    cmbOrderTypeFilter.Visibility = Visibility.Visible;
                    OrderType = BO.OrderType.All;
                    if (cmbOrderTypeFilter != null)
                        cmbOrderTypeFilter.SelectedValue = BO.OrderType.All;
                    break;
                                                
                case PL.FilterTypeOrder.BySheduleStatus:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Schedule Status:";
                    cmbScheduleStatusFilter.Visibility = Visibility.Visible;
                    ScheduleStatus = BO.ScheduleStatus.All;
                    if (cmbScheduleStatusFilter != null)
                        cmbScheduleStatusFilter.SelectedValue = BO.ScheduleStatus.All;
                    break;
                
                case PL.FilterTypeOrder.All:
                default:
                    // Reset all filter properties to their default values
                    OrderStatus = BO.OrderStatus.All;
                    OrderType = BO.OrderType.All;
                    FragilityLevel = BO.FragilityLevel.All;
                    ScheduleStatus = BO.ScheduleStatus.All;
                    
                    // Reset ComboBox selections to default values
                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    if (cmbOrderTypeFilter != null)
                        cmbOrderTypeFilter.SelectedValue = BO.OrderType.All;
                    if (cmbScheduleStatusFilter != null)
                        cmbScheduleStatusFilter.SelectedValue = BO.ScheduleStatus.All;
                    break;
            }
            
            // Always refresh the order list when filter type changes
            queryOrderList();
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

    private void OrderTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderType selectedType)
        {
            OrderType = selectedType;
            queryOrderList();
        }
    }

    private void FragilityLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.FragilityLevel selectedFragility)
        {
            FragilityLevel = selectedFragility;
            queryOrderList();
        }
    }

    private void ScheduleStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.ScheduleStatus selectedSchedule)
        {
            ScheduleStatus = selectedSchedule;
            queryOrderList();
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

    private void btnNewOrder_Click(object sender, RoutedEventArgs e)
    {
        new OrderWindow().Show();
    }

    /// <summary>
    /// Public method to apply filters and refresh the order list from external calls
    /// </summary>
    /// <param name="filterType">The filter type to apply</param>
    /// <param name="scheduleStatus">Schedule status filter</param>
    /// <param name="orderStatus">Order status filter</param>
    public void ApplyFiltersAndRefresh(PL.FilterTypeOrder filterType, BO.ScheduleStatus? scheduleStatus = null, BO.OrderStatus? orderStatus = null)
    {
        FilterTypeOrder = filterType;
        
        if (scheduleStatus.HasValue)
            ScheduleStatus = scheduleStatus.Value;
        
        if (orderStatus.HasValue)
            OrderStatus = orderStatus.Value;
        
        queryOrderList();
    }
}
