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
    private bool _isOpen = false;

    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();

    // Stage 7: Mutex for order list observer
    private readonly ObserverMutex _orderListMutex = new(); // stage 7
    
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

    public bool IsLoading
    {
        get { return (bool)GetValue(IsLoadingProperty); }
        set { SetValue(IsLoadingProperty, value); }
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register("IsLoading", typeof(bool), typeof(OrderListWindow), new PropertyMetadata(false));

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
            OrderList = null;
            OrderList = await FetchOrderListAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            OrderList = new List<BO.OrderInList>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void orderListObserver()
    {
        #region Stage 7 (for multithreading)
        if (_orderListMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await RefreshOrderListAsync();
            }
            finally
            {
                if (await _orderListMutex.UnsetLoadInProgressAndCheckRestartRequested())
                {
                    orderListObserver();
                }
            }
        });
        #endregion Stage 7 (for multithreading)
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isOpen) return;
        
        await RefreshOrderListAsync();
        try
        {
            s_bl.Order.AddObserver(orderListObserver);
            _isOpen = true;
        }
        catch (Exception ex)
        {
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
        catch { }
        finally
        {
            _isOpen = false;
        }
    }

    /// <summary>
    /// Handles changes to the main filter type combo box selection.
    /// This method controls which specific filter controls are visible and applies the appropriate filter.
    /// 
    /// Behavior by filter type:
    /// - <see cref="PL.FilterTypeOrder.All"/>: Hides all specific filter controls and shows all orders
    /// - <see cref="PL.FilterTypeOrder.ByOrderStatus"/>: Shows order status filter combo box
    /// - <see cref="PL.FilterTypeOrder.ByOrderType"/>: Shows order type filter combo box
    /// - <see cref="PL.FilterTypeOrder.BySheduleStatus"/>: Shows schedule status filter combo box
    /// - <see cref="PL.FilterTypeOrder.ByOrderAndSchedulStatus"/>: Shows both order status and schedule status filter combo boxes
    /// </summary>
    /// <param name="sender">The ComboBox control that changed selection.</param>
    /// <param name="e">The event arguments containing the old and new selected items.</param>
    /// <remarks>
    /// This method skips execution if the window is not fully loaded (IsLoaded = false)
    /// to avoid issues during XAML initialization.
    /// Filter controls are dynamically hidden and shown based on the selected filter type.
    /// All filter values are reset to "All" when the filter type changes.
    /// </remarks>
    private void FilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Skip if window is not fully loaded to avoid executing during initialization
        if (!IsLoaded) return;
        
        // Verify sender is a ComboBox and extract the selected filter type
        if (sender is ComboBox comboBox && comboBox.SelectedItem is PL.FilterTypeOrder selectedFilter)
        {
            FilterTypeOrder = selectedFilter;
            
            // Hide all filter-specific UI elements initially
            lblSpecificFilter.Visibility = Visibility.Collapsed;
            lblSecondaryFilter.Visibility = Visibility.Collapsed;
            cmbOrderStatusFilter.Visibility = Visibility.Collapsed;
            cmbOrderTypeFilter.Visibility = Visibility.Collapsed;
            cmbScheduleStatusFilter.Visibility = Visibility.Collapsed;
            cmbScheduleStatusFilterSecondary.Visibility = Visibility.Collapsed;
            
            // Show the appropriate filter controls based on the selected filter type
            switch (selectedFilter)
            {
                case PL.FilterTypeOrder.ByOrderStatus:
                    // Display order status filter
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Order Status:";
                    cmbOrderStatusFilter.Visibility = Visibility.Visible;
                    // Reset to show all orders until a specific status is selected
                    OrderStatus = BO.OrderStatus.All;
                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    break;
                
                case PL.FilterTypeOrder.ByOrderType:
                    // Display order type filter
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Order Type:";
                    cmbOrderTypeFilter.Visibility = Visibility.Visible;
                    // Reset to show all orders until a specific type is selected
                    OrderType = BO.OrderType.All;
                    if (cmbOrderTypeFilter != null)
                        cmbOrderTypeFilter.SelectedValue = BO.OrderType.All;
                    break;
                                                
                case PL.FilterTypeOrder.BySheduleStatus:
                    // Display schedule status filter
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Schedule Status:";
                    cmbScheduleStatusFilter.Visibility = Visibility.Visible;
                    // Reset to show all orders until a specific schedule status is selected
                    ScheduleStatus = BO.ScheduleStatus.All;
                    if (cmbScheduleStatusFilter != null)
                        cmbScheduleStatusFilter.SelectedValue = BO.ScheduleStatus.All;
                    break;

                case PL.FilterTypeOrder.ByOrderAndSchedulStatus:
                    // Display both order status and schedule status filters
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Order Status:";
                    lblSecondaryFilter.Visibility = Visibility.Visible;
                    cmbOrderStatusFilter.Visibility = Visibility.Visible;
                    cmbScheduleStatusFilterSecondary.Visibility = Visibility.Visible;
                    // Reset to show all orders until specific statuses are selected
                    OrderStatus = BO.OrderStatus.All;
                    ScheduleStatus = BO.ScheduleStatus.All;
                    if (cmbOrderStatusFilter != null)
                        cmbOrderStatusFilter.SelectedValue = BO.OrderStatus.All;
                    if (cmbScheduleStatusFilterSecondary != null)
                        cmbScheduleStatusFilterSecondary.SelectedValue = BO.ScheduleStatus.All;
                    break;
                
                case PL.FilterTypeOrder.All:
                default:
                    // Reset all filter properties to their default "All" values
                    OrderStatus = BO.OrderStatus.All;
                    OrderType = BO.OrderType.All;
                    FragilityLevel = BO.FragilityLevel.All;
                    ScheduleStatus = BO.ScheduleStatus.All;
                    
                    // Reset all ComboBox selections to their default "All" values
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
            
            // Refresh the order list when the filter type changes
            _ = RefreshOrderListAsync();
        }
    }

    /// <summary>
    /// Handles changes to the order status filter combo box selection.
    /// Updates the <see cref="OrderStatus"/> property and refreshes the order list.
    /// </summary>
    /// <param name="sender">The ComboBox control showing order status options.</param>
    /// <param name="e">The event arguments containing the selection change information.</param>
    /// <remarks>
    /// This handler is only active when <see cref="FilterTypeOrder"/> is set to 
    /// <see cref="PL.FilterTypeOrder.ByOrderStatus"/>.
    /// </remarks>
    private void OrderStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Verify sender is a ComboBox and extract the selected order status
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderStatus selectedStatus)
        {
            OrderStatus = selectedStatus;
            // Reload the order list based on the new filter selection
            _ = RefreshOrderListAsync();
        }
    }

    /// <summary>
    /// Handles changes to the order type filter combo box selection.
    /// Updates the <see cref="OrderType"/> property and refreshes the order list.
    /// </summary>
    /// <param name="sender">The ComboBox control showing order type options.</param>
    /// <param name="e">The event arguments containing the selection change information.</param>
    /// <remarks>
    /// This handler is only active when <see cref="FilterTypeOrder"/> is set to 
    /// <see cref="PL.FilterTypeOrder.ByOrderType"/>.
    /// </remarks>
    private void OrderTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Verify sender is a ComboBox and extract the selected order type
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.OrderType selectedType)
        {
            OrderType = selectedType;
            // Reload the order list based on the new filter selection
            _ = RefreshOrderListAsync();
        }
    }

    /// <summary>
    /// Handles changes to the fragility level filter combo box selection.
    /// Updates the <see cref="FragilityLevel"/> property and refreshes the order list.
    /// </summary>
    /// <param name="sender">The ComboBox control showing fragility level options.</param>
    /// <param name="e">The event arguments containing the selection change information.</param>
    /// <remarks>
    /// While this filter is defined and its handler is implemented,
    /// it may not be actively used in the current filtering logic depending
    /// on the business logic implementation.
    /// </remarks>
    private void FragilityLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Verify sender is a ComboBox and extract the selected fragility level
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.FragilityLevel selectedFragility)
        {
            FragilityLevel = selectedFragility;
            // Reload the order list based on the new filter selection
            _ = RefreshOrderListAsync();
        }
    }

    /// <summary>
    /// Handles changes to the schedule status filter combo box selection.
    /// Updates the <see cref="ScheduleStatus"/> property and refreshes the order list.
    /// </summary>
    /// <param name="sender">The ComboBox control showing schedule status options.</param>
    /// <param name="e">The event arguments containing the selection change information.</param>
    /// <remarks>
    /// This handler is only active when <see cref="FilterTypeOrder"/> is set to 
    /// <see cref="PL.FilterTypeOrder.BySheduleStatus"/>.
    /// </remarks>
    private void ScheduleStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Verify sender is a ComboBox and extract the selected schedule status
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.ScheduleStatus selectedSchedule)
        {
            ScheduleStatus = selectedSchedule;
            // Reload the order list based on the new filter selection
            _ = RefreshOrderListAsync();
        }
    }

    /// <summary>
    /// Handles double-click events on the order DataGrid.
    /// When a user double-clicks on an order row, this method opens a detailed view window for that order.
    /// </summary>
    /// <param name="sender">The DataGrid control that received the double-click event.</param>
    /// <param name="e">The event arguments containing mouse button information.</param>
    /// <remarks>
    /// A new <see cref="OrderWindow"/> instance is created with the selected order's ID and displayed.
    /// If the sender is not a DataGrid or no valid order is selected, the method silently returns without action.
    /// </remarks>
    private void dgOrderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Verify sender is a DataGrid
        if (sender is not DataGrid dataGrid)
            return;

        // Verify a valid order is selected in the grid
        if (dataGrid.SelectedItem is not BO.OrderInList selectedOrder)
            return;

        // Open a new window displaying the details of the selected order
        new OrderWindow(selectedOrder.OrderId).Show();
    }

    /// <summary>
    /// Handles the click event of the "Cancel Order" button in the DataGrid.
    /// Cancels the selected order if it is not yet completed (i.e., status is Pending or Processing).
    /// If the order is in delivery (Processing status), sends an email notification to the assigned courier.
    /// </summary>
    /// <param name="sender">The Button control that triggered the click event.</param>
    /// <param name="e">The event arguments containing routed event information.</param>
    /// <remarks>
    /// The button is only displayed for orders with Pending or Processing status.
    /// Orders with Delivered, Canceled, or Returned status cannot be cancelled.
    /// If the order is in delivery, the courier's email is retrieved and an email notification is sent.
    /// </remarks>
    private void btnCancelOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Extract the order ID from the button's Tag property
            if (sender is not Button button)
                return;

            if (button.Tag is not int orderId)
                return;

            // Retrieve the order details to check status and delivery info
            var bossId = s_bl.Admin.GetConfig().BossId;


            // Confirm cancellation with the user
            var result = MessageBox.Show(
                $"Are you sure you want to cancel order #{orderId}?\n",
                "Confirm Order Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Cancel the order through the business logic layer
            s_bl.Order.CancelOrder(bossId, orderId);

            // Show success message
            MessageBox.Show(
                $"Order #{orderId} has been successfully cancelled.",
                "Order Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // Show error message if cancellation fails
            MessageBox.Show(
                $"Error cancelling order: {ex.Message}",
                "Cancellation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles the click event of the "Add Order" button.
    /// Creates and displays a new <see cref="OrderWindow"/> for creating a new order.
    /// </summary>
    /// <param name="sender">The Button control that triggered the click event.</param>
    /// <param name="e">The event arguments containing routed event information.</param>
    /// <remarks>
    /// The new OrderWindow is instantiated without an order ID parameter, 
    /// indicating it operates in "create new order" mode rather than "edit existing" mode.
    /// </remarks>
    private void btnNewOrder_Click(object sender, RoutedEventArgs e)
    {
        // Create and display a new OrderWindow for creating a new order
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

        // Call the correct method to refresh the order list
        _ = RefreshOrderListAsync();
    }
}
