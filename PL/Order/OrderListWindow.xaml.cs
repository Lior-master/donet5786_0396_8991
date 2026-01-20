using PL.Courier;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PL.Order;

public partial class OrderListWindow : Window
{
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();

    // Stage 7: Mutex for order list observer
    private readonly ObserverMutex _orderListMutex = new(); // stage 7

    private bool _observerRegistered = false;

    public OrderListWindow()
    {
        InitializeComponent();
    }

    public IEnumerable<BO.OrderInList> OrderList
    {
        get => (IEnumerable<BO.OrderInList>)GetValue(OrderListProperty);
        set => SetValue(OrderListProperty, value);
    }

    public static readonly DependencyProperty OrderListProperty =
        DependencyProperty.Register(
            nameof(OrderList),
            typeof(IEnumerable<BO.OrderInList>),
            typeof(OrderListWindow),
            new PropertyMetadata(null));

    public PL.FilterTypeOrder FilterTypeOrder { get; set; } = PL.FilterTypeOrder.All;

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(OrderListWindow),
            new PropertyMetadata(false));

    public BO.OrderStatus OrderStatus { get; set; } = BO.OrderStatus.All;
    public BO.OrderType OrderType { get; set; } = BO.OrderType.All;
    public BO.FragilityLevel FragilityLevel { get; set; } = BO.FragilityLevel.All;
    public BO.ScheduleStatus ScheduleStatus { get; set; } = BO.ScheduleStatus.All;

    private async Task<List<BO.OrderInList>> FetchOrderListAsync()
    {
        var bossId = s_bl.Admin.GetConfig().BossId;

        return FilterTypeOrder switch
        {
            PL.FilterTypeOrder.All =>
                (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList(),

            PL.FilterTypeOrder.ByOrderStatus => (OrderStatus == BO.OrderStatus.All)
                ? (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList()
                : (await s_bl.Order.orderInListsAsync(bossId, PL.FilterTypeOrder.ByOrderStatus, OrderStatus, null)).ToList(),

            PL.FilterTypeOrder.ByOrderType => (OrderType == BO.OrderType.All)
                ? (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList()
                : (await s_bl.Order.orderInListsAsync(bossId, PL.FilterTypeOrder.ByOrderType, OrderType, null)).ToList(),

            PL.FilterTypeOrder.BySheduleStatus => (ScheduleStatus == BO.ScheduleStatus.All)
                ? (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList()
                : (await s_bl.Order.orderInListsAsync(bossId, PL.FilterTypeOrder.BySheduleStatus, ScheduleStatus, null)).ToList(),

            PL.FilterTypeOrder.ByOrderAndSchedulStatus => (ScheduleStatus == BO.ScheduleStatus.All && OrderStatus == BO.OrderStatus.All)
                ? (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList()
                : (ScheduleStatus == BO.ScheduleStatus.All)
                    ? (await s_bl.Order.orderInListsAsync(bossId, PL.FilterTypeOrder.ByOrderStatus, OrderStatus, null)).ToList()
                    : (OrderStatus == BO.OrderStatus.All)
                        ? (await s_bl.Order.orderInListsAsync(bossId, PL.FilterTypeOrder.BySheduleStatus, ScheduleStatus, null)).ToList()
                        : (await s_bl.Order.orderInListsDoubleFilterAsync(bossId, ScheduleStatus, OrderStatus)).ToList(),

            _ => (await s_bl.Order.orderInListsAsync(bossId, null, null, null)).ToList()
        };
    }

    private async Task RefreshOrderListAsync()
    {
        try
        {
            IsLoading = true;

            // safer for WPF DataGrid than null (avoids some binding edge cases)
            OrderList = Array.Empty<BO.OrderInList>();

            OrderList = await FetchOrderListAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            OrderList = Array.Empty<BO.OrderInList>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ================================
    //   STAGE 7 OBSERVER (THREAD SAFE)
    // ================================
    private void orderListObserver()
    {
        if (_orderListMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await RefreshOrderListAsync();
            }
            catch
            {
                // keep observer resilient
            }
            finally
            {
                if (await _orderListMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    orderListObserver();
            }
        }));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load once
        await RefreshOrderListAsync();

        // Register observer once per window instance
        if (_observerRegistered)
            return;

        try
        {
            s_bl.Order.AddObserver(orderListObserver);
            _observerRegistered = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Observer registration failed: {ex.Message}",
                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (!_observerRegistered)
            return;

        try
        {
            s_bl.Order.RemoveObserver(orderListObserver);
        }
        catch
        {
            // ignore on shutdown
        }
        finally
        {
            _observerRegistered = false;
        }
    }

    // ================================
    // FILTER UI HANDLERS (unchanged)
    // ================================
    private void FilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (sender is ComboBox comboBox && comboBox.SelectedItem is PL.FilterTypeOrder selectedFilter)
        {
            FilterTypeOrder = selectedFilter;

            lblSpecificFilter.Visibility = Visibility.Collapsed;
            lblSecondaryFilter.Visibility = Visibility.Collapsed;
            cmbOrderStatusFilter.Visibility = Visibility.Collapsed;
            cmbOrderTypeFilter.Visibility = Visibility.Collapsed;
            cmbScheduleStatusFilter.Visibility = Visibility.Collapsed;
            cmbScheduleStatusFilterSecondary.Visibility = Visibility.Collapsed;

            switch (selectedFilter)
            {
                case PL.FilterTypeOrder.ByOrderStatus:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Order Status:";
                    cmbOrderStatusFilter.Visibility = Visibility.Visible;
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

                case PL.FilterTypeOrder.ByOrderAndSchedulStatus:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Order Status:";
                    lblSecondaryFilter.Visibility = Visibility.Visible;
                    cmbOrderStatusFilter.Visibility = Visibility.Visible;
                    cmbScheduleStatusFilterSecondary.Visibility = Visibility.Visible;

                    OrderStatus = BO.OrderStatus.All;
                    ScheduleStatus = BO.ScheduleStatus.All;

                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    if (cmbScheduleStatusFilterSecondary != null)
                        cmbScheduleStatusFilterSecondary.SelectedValue = BO.ScheduleStatus.All;
                    break;

                case PL.FilterTypeOrder.All:
                default:
                    OrderStatus = BO.OrderStatus.All;
                    OrderType = BO.OrderType.All;
                    FragilityLevel = BO.FragilityLevel.All;
                    ScheduleStatus = BO.ScheduleStatus.All;

                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    if (cmbOrderTypeFilter != null)
                        cmbOrderTypeFilter.SelectedValue = BO.OrderType.All;
                    if (cmbScheduleStatusFilter != null)
                        cmbScheduleStatusFilter.SelectedValue = BO.ScheduleStatus.All;
                    if (cmbScheduleStatusFilterSecondary != null)
                        cmbScheduleStatusFilterSecondary.SelectedValue = BO.ScheduleStatus.All;
                    break;
            }

            _ = RefreshOrderListAsync();
        }
    }

    private void OrderStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderStatus selectedStatus)
        {
            OrderStatus = selectedStatus;
            _ = RefreshOrderListAsync();
        }
    }

    private void OrderTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderType selectedType)
        {
            OrderType = selectedType;
            _ = RefreshOrderListAsync();
        }
    }

    private void FragilityLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.FragilityLevel selectedFragility)
        {
            FragilityLevel = selectedFragility;
            _ = RefreshOrderListAsync();
        }
    }

    private void ScheduleStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.ScheduleStatus selectedSchedule)
        {
            ScheduleStatus = selectedSchedule;
            _ = RefreshOrderListAsync();
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

    private void btnCancelOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not int orderId)
                return;

            var bossId = s_bl.Admin.GetConfig().BossId;

            var result = MessageBox.Show(
                $"Are you sure you want to cancel order #{orderId}?\n",
                "Confirm Order Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            s_bl.Order.CancelOrder(bossId, orderId);

            MessageBox.Show(
                $"Order #{orderId} has been successfully cancelled.",
                "Order Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error cancelling order: {ex.Message}",
                "Cancellation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void btnNewOrder_Click(object sender, RoutedEventArgs e)
    {
        new OrderWindow().Show();
    }

    public void ApplyFiltersAndRefresh(PL.FilterTypeOrder filterType, BO.ScheduleStatus? scheduleStatus = null, BO.OrderStatus? orderStatus = null)
    {
        FilterTypeOrder = filterType;

        if (scheduleStatus.HasValue)
            ScheduleStatus = scheduleStatus.Value;

        if (orderStatus.HasValue)
            OrderStatus = orderStatus.Value;

        _ = RefreshOrderListAsync();
    }
}
