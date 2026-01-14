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
/// Interaction logic for OrderListWindow.xaml.
/// 
/// This window displays a comprehensive list of orders with advanced filtering capabilities.
/// It supports filtering by order status, order type, and schedule status.
/// The window implements the observer pattern to automatically refresh the order list 
/// when data changes in the business logic layer.
/// 
/// Key Features:
/// - Dynamic filter visibility based on selected filter type
/// - Real-time order list updates via observer pattern
/// - DataGrid display with double-click to view order details
/// - Cancel button for orders that can be cancelled (Pending or Processing status)
/// - Email notification sent to courier when cancelling orders in delivery
/// - Window state tracking to prevent duplicate initialization
/// </summary>
public partial class OrderListWindow : Window
{
    /// <summary>
    /// Flag to track whether the window has been fully loaded.
    /// Used to prevent duplicate observer registration and multiple initializations.
    /// </summary>
    private bool _isOpen = false;

    /// <summary>
    /// Static reference to the Business Logic API singleton instance.
    /// Used to access order, courier, and admin services throughout the window's lifetime.
    /// </summary>
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();
    
    /// <summary>
    /// Initializes a new instance of the OrderListWindow.
    /// Calls InitializeComponent to load XAML resources and initialize WPF controls.
    /// </summary>
    public OrderListWindow()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Gets or sets the collection of orders to be displayed in the DataGrid.
    /// This is a dependency property that binds to the DataGrid view in the XAML.
    /// </summary>
    /// <value>
    /// An enumerable collection of <see cref="BO.OrderInList"/> objects.
    /// Setting this value triggers automatic UI updates through data binding.
    /// </value>
    public IEnumerable<BO.OrderInList> OrderList
    {
        get { return (IEnumerable<BO.OrderInList>)GetValue(OrderListProperty); }
        set { SetValue(OrderListProperty, value); }
    }

    /// <summary>
    /// Defines the OrderList dependency property for WPF data binding.
    /// Enables the OrderList property to be bound to XAML controls like DataGrid.
    /// </summary>
    public static readonly DependencyProperty OrderListProperty =
        DependencyProperty.Register("OrderList", typeof(IEnumerable<BO.OrderInList>), typeof(OrderListWindow), new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the current filter type applied to the order list.
    /// Determines which specific filter control is visible and how the data is filtered.
    /// </summary>
    /// <value>
    /// A <see cref="PL.FilterTypeOrder"/> enum value.
    /// Default is <see cref="PL.FilterTypeOrder.All"/> (no filtering applied).
    /// </value>
    public PL.FilterTypeOrder FilterTypeOrder { get; set; } = PL.FilterTypeOrder.All;

    /// <summary>
    /// Gets or sets the order status filter value.
    /// Used when <see cref="FilterTypeOrder"/> is set to <see cref="PL.FilterTypeOrder.ByOrderStatus"/>.
    /// </summary>
    /// <value>
    /// A <see cref="BO.OrderStatus"/> enum value.
    /// Default is <see cref="BO.OrderStatus.All"/> (displays all statuses).
    /// </value>
    public BO.OrderStatus OrderStatus { get; set; } = BO.OrderStatus.All;

    /// <summary>
    /// Gets or sets the order type filter value.
    /// Used when <see cref="FilterTypeOrder"/> is set to <see cref="PL.FilterTypeOrder.ByOrderType"/>.
    /// </summary>
    /// <value>
    /// A <see cref="BO.OrderType"/> enum value representing the food/service category.
    /// Default is <see cref="BO.OrderType.All"/> (displays all types).
    /// </value>
    public BO.OrderType OrderType { get; set; } = BO.OrderType.All;

    /// <summary>
    /// Gets or sets the fragility level filter value.
    /// Currently defined for potential future use in filtering logic.
    /// </summary>
    /// <value>
    /// A <see cref="BO.FragilityLevel"/> enum value.
    /// Default is <see cref="BO.FragilityLevel.All"/>.
    /// </value>
    public BO.FragilityLevel FragilityLevel { get; set; } = BO.FragilityLevel.All;

    /// <summary>
    /// Gets or sets the schedule status filter value.
    /// Used when <see cref="FilterTypeOrder"/> is set to <see cref="PL.FilterTypeOrder.BySheduleStatus"/>.
    /// </summary>
    /// <value>
    /// A <see cref="BO.ScheduleStatus"/> enum value indicating delivery timing status.
    /// Default is <see cref="BO.ScheduleStatus.All"/> (displays all schedule statuses).
    /// </value>
    public BO.ScheduleStatus ScheduleStatus { get; set; } = BO.ScheduleStatus.All;

    /// <summary>
    /// Queries the business logic layer to retrieve the order list based on current filter settings.
    /// Updates the <see cref="OrderList"/> property with the retrieved results.
    /// 
    /// This method:
    /// 1. Retrieves the boss ID from the admin configuration
    /// 2. Applies the appropriate filter based on <see cref="FilterTypeOrder"/>
    /// 3. Calls the business logic to fetch filtered orders
    /// 4. Updates the DataGrid through data binding
    /// 5. Displays error messages if the operation fails
    /// </summary>
    /// <remarks>
    /// The method supports the following filter scenarios:
    /// - <see cref="PL.FilterTypeOrder.All"/>: Retrieves all orders without filtering
    /// - <see cref="PL.FilterTypeOrder.ByOrderStatus"/>: Filters by order status (if not "All")
    /// - <see cref="PL.FilterTypeOrder.ByOrderType"/>: Filters by order type (if not "All")
    /// - <see cref="PL.FilterTypeOrder.BySheduleStatus"/>: Filters by schedule status (if not "All")
    /// 
    /// If a filter property is set to "All", it bypasses filtering for that category.
    /// On exception, displays error message and sets OrderList to empty collection.
    /// </remarks>
    private void queryOrderList()
    {
        try
        {
            // Retrieve the boss (administrator) ID from system configuration
            var bossId = s_bl.Admin.GetConfig().BossId;

            // Apply filters based on the selected filter type
            switch (FilterTypeOrder)
            {
                case PL.FilterTypeOrder.All:
                    // No filtering: retrieve all orders
                    OrderList = s_bl.Order.orderInLists(bossId, null, null, null);
                    break;

                case PL.FilterTypeOrder.ByOrderStatus:
                    // Filter by order status, or retrieve all if "All" is selected
                    OrderList = (OrderStatus == BO.OrderStatus.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.ByOrderStatus, OrderStatus, null);
                    break;

                case PL.FilterTypeOrder.ByOrderType:
                    // Filter by order type, or retrieve all if "All" is selected
                    OrderList = (OrderType == BO.OrderType.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.ByOrderType, OrderType, null);
                    break;

                case PL.FilterTypeOrder.BySheduleStatus:
                    // Filter by schedule status, or retrieve all if "All" is selected
                    OrderList = (ScheduleStatus == BO.ScheduleStatus.All)
                        ? s_bl.Order.orderInLists(bossId, null, null, null)
                        : s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.BySheduleStatus, ScheduleStatus, null);
                    break;

                case PL.FilterTypeOrder.ByOrderAndSchedulStatus:
                    // Filter by schedule status and by Order status
                    if (ScheduleStatus == BO.ScheduleStatus.All && OrderStatus == BO.OrderStatus.All)
                    {
                        OrderList = s_bl.Order.orderInLists(bossId, null, null, null);
                        break;
                    }
                    else if(ScheduleStatus == BO.ScheduleStatus.All)
                    {
                        OrderList = s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.ByOrderStatus, OrderStatus, null);
                        break;
                    }                    
                    else if(OrderStatus == BO.OrderStatus.All)
                    {
                        OrderList = s_bl.Order.orderInLists(bossId, PL.FilterTypeOrder.BySheduleStatus, ScheduleStatus, null);
                        break;
                    }
                    else
                    {
                        OrderList = s_bl.Order.orderInListsDoubleFilter(bossId, ScheduleStatus, OrderStatus);
                        break;
                    }
                default:
                    // Fallback: retrieve all orders for unknown filter types
                    OrderList = s_bl.Order.orderInLists(bossId, null, null, null);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Display error message to the user
            MessageBox.Show($"Error loading orders: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Set order list to empty collection on failure
            OrderList = new List<BO.OrderInList>();
        }
    }

    /// <summary>
    /// Observer callback method invoked when order data changes in the business logic layer.
    /// This method refreshes the order list to reflect the latest data.
    /// </summary>
    /// <remarks>
    /// This method is registered as an observer with the <see cref="IBl.Order"/> service.
    /// When orders are added, modified, or removed, this callback is automatically invoked.
    /// </remarks>
    private void orderListObserver()
        => queryOrderList();

    /// <summary>
    /// Handles the Window_Loaded event, which fires when the window is fully initialized and ready for display.
    /// 
    /// This method performs the following initialization steps:
    /// 1. Checks if the window is already open to prevent duplicate initialization
    /// 2. Loads the initial order list based on current filter settings
    /// 3. Registers the observer callback with the business logic layer
    /// 4. Sets the <see cref="_isOpen"/> flag to track initialization state
    /// 5. Gracefully handles observer registration failures
    /// </summary>
    /// <param name="sender">The event sender (the Window).</param>
    /// <param name="e">The event arguments containing routed event information.</param>
    /// <remarks>
    /// If the observer is already registered or if the business logic doesn't support observers,
    /// a warning is displayed but execution continues without throwing an exception.
    /// The _isOpen flag is always set to true in the finally block to ensure the window
    /// is marked as initialized even if observer registration fails.
    /// </remarks>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Check if window is already initialized to prevent duplicate loading
        if (_isOpen) return;
        
        // Load the initial order list with current filter settings
        queryOrderList();
        try
        {
            // Register the observer to receive notifications of order data changes
            s_bl.Order.AddObserver(orderListObserver);
            _isOpen = true;
        }
        catch (Exception ex)
        {
            // Some BL implementations might not support observers, so this is not fatal
            MessageBox.Show($"Observer registration failed: {ex.Message}", 
                           "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            // Always mark the window as opened to prevent re-initialization
            _isOpen = true;
        }
    }

    /// <summary>
    /// Handles the Window_Closed event, which fires when the window is being closed.
    /// Cleans up resources by unregistering the observer from the business logic layer.
    /// </summary>
    /// <param name="sender">The event sender (the Window).</param>
    /// <param name="e">The event arguments containing window close information.</param>
    /// <remarks>
    /// Errors during observer removal are silently ignored to prevent exceptions
    /// from preventing the window from closing. The _isOpen flag is set to false
    /// in the finally block to mark the window as closed.
    /// </remarks>
    private void Window_Closed(object sender, EventArgs e)
    {
        try
        {
            // Unregister the observer callback to prevent memory leaks
            s_bl.Order.RemoveObserver(orderListObserver);
        }
        catch
        {
            // Ignore errors during cleanup to allow window closure to proceed
        }
        finally
        {
            // Mark the window as closed
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
            queryOrderList();
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
            queryOrderList();
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
            queryOrderList();
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
            queryOrderList();
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
            queryOrderList();
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
        
        queryOrderList();
    }
}
