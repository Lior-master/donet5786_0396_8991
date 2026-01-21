using BlApi;
using BO;
using Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

/// <summary>
/// Implements the presentation layer UI and related view models.
/// </summary>
namespace PL.Courier;

/// <summary>
/// Refresh.
/// Relies on BL observers for refresh (AssignOrderToCourier triggers notifications).
/// </summary>
public partial class CourierOrderSelectionWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    /// <summary>
    /// Stores the courier id value.
    /// </summary>
    private readonly int _courierId;
    /// <summary>
    /// Stores the boss id value.
    /// </summary>
    private readonly int _bossId;

    /// <summary>
    /// Stores the orders observer value.
    /// </summary>
    private readonly Action _ordersObserver;

    // Stage 7: prevents re-entrant refreshes and ensures we rerun if updates arrive mid-refresh
    private readonly ObserverMutex _openOrdersMutex = new(); // stage 7

    /// <summary>
    /// Stores the observer registered value.
    /// </summary>
    private bool _observerRegistered = false;

    /// <summary>
    /// Stores the is loading value.
    /// </summary>
    private bool _isLoading;
    /// <summary>
    /// Gets or sets the is loading value.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    // Property to track the assigned order ID
    /// <summary>
    /// Gets or sets the assigned order id value.
    /// </summary>
    public int? AssignedOrderId { get; private set; }

    /// <summary>
    /// Performs the operation.
    /// </summary>
    public ObservableCollection<OpenOrderInList> OpenOrders { get; } = new();

    /// <summary>
    /// Stores the filter status message value.
    /// </summary>
    private string _filterStatusMessage = string.Empty;
    /// <summary>
    /// Gets or sets the filter status message value.
    /// </summary>
    public string FilterStatusMessage
    {
        get => _filterStatusMessage;
        set { _filterStatusMessage = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Initializes a new instance of the CourierOrderSelectionWindow class.
    /// </summary>
    /// <param name="courierId">The courier id value.</param>
    public CourierOrderSelectionWindow(int courierId)
    {
        InitializeComponent();

        _courierId = courierId;
        _bossId = s_bl.Admin.GetConfig().BossId;

        DataContext = this;
        lstOpenOrders.ItemsSource = OpenOrders;

        // Observer callback (can come from background threads) -> marshal to UI thread safely
        _ordersObserver = OrdersObserverCallback;

        // Prefer explicit hooks (works even if not wired in XAML)
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            InitializeFilterCombo();

            TryRegisterObserver();

            await RefreshOpenOrdersAsync();
        }
        catch (Exception ex)
        {
            ShowErrorAndClose($"Failed to load open orders: {ex.Message}");
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        TryUnregisterObserver();
    }

    private void InitializeFilterCombo()
    {
        cmbFilterOrderType.Items.Clear();
        cmbFilterOrderType.Items.Add("All");

        foreach (var t in Enum.GetValues(typeof(BO.OrderType)).Cast<BO.OrderType>())
            cmbFilterOrderType.Items.Add(t);

        cmbFilterOrderType.SelectedIndex = 0;
    }

    private void TryRegisterObserver()
    {
        if (_observerRegistered)
            return;

        try
        {
            s_bl.Order.AddObserver(_ordersObserver);
            _observerRegistered = true;
        }
        catch
        {
            // ignore if BL doesn't support / throws
        }
    }

    private void TryUnregisterObserver()
    {
        if (!_observerRegistered)
            return;

        try
        {
            s_bl.Order.RemoveObserver(_ordersObserver);
        }
        catch
        {
            // ignore shutdown issues
        }
        finally
        {
            _observerRegistered = false;
        }
    }

    private BO.OrderType? GetSelectedFilter() =>
        cmbFilterOrderType.SelectedItem is BO.OrderType ot ? ot : null;

    // ================================
    // Stage 7-safe observer callback
    // ================================
    /// <summary>
    /// Handles order observer notifications and refreshes the open-order list.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Dispatcher"/> to marshal updates to the UI thread and
    /// <see cref="ObserverMutex"/> to prevent overlapping refresh runs. If a notification arrives
    /// during an active refresh, the mutex requests a restart to ensure the latest data is shown
    /// (stage 7 observer pattern).
    /// </remarks>
    private void OrdersObserverCallback()
    {
        if (_openOrdersMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await RefreshOpenOrdersCoreAsync();
            }
            catch
            {
                // keep observer resilient
            }
            finally
            {
                if (await _openOrdersMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    OrdersObserverCallback();
            }
        }));
    }

    private async Task RefreshOpenOrdersAsync()
    {
        // User-triggered refresh (filter change / loaded) should reuse the same core logic
        await RefreshOpenOrdersCoreAsync();
    }

    private async Task RefreshOpenOrdersCoreAsync()
    {
        IsLoading = true;
        var filter = GetSelectedFilter();

        try
        {
            txtStatus.Text = "Loading open orders...";
            OpenOrders.Clear();

            var list = (await s_bl.Order.GetOpenOrdersForCourierAsync(_bossId, _courierId, filter, null)).ToList();

            UpdateOpenOrdersCollection(list);
            UpdateStatusMessages(filter);
        }
        catch (Exception ex)
        {
            txtStatus.Text = "❌ Failed to load open orders";
            FilterStatusMessage = "Failed to refresh";
            MessageBox.Show($"Failed to refresh open orders:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateOpenOrdersCollection(System.Collections.Generic.List<OpenOrderInList> list)
    {
        OpenOrders.Clear();
        foreach (var o in list) OpenOrders.Add(o);

        txtStatus.Text = $"Loaded {OpenOrders.Count} available orders";
        lstOpenOrders.Items.Refresh();
    }

    private void UpdateStatusMessages(BO.OrderType? filter)
    {
        FilterStatusMessage = filter == null
            ? "Showing all available orders"
            : $"Filtered by: {filter}";
    }

    private void cmbFilterOrderType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _ = RefreshOpenOrdersAsync();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async void BtnAssign_Click(object sender, RoutedEventArgs e)
    {
        await AssignSelectedOrderAsync(lstOpenOrders.SelectedItem as OpenOrderInList);
    }

    private async void BtnChooseOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not OpenOrderInList selected) return;

        await AssignSelectedOrderAsync(selected);
    }

    private async Task AssignSelectedOrderAsync(OpenOrderInList? selected)
    {
        if (selected == null)
        {
            MessageBox.Show("Please select an order to assign.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmAssignment(selected))
            return;

        try
        {
            BeginBusy("🚚 Assigning order...");

            await s_bl.Order.AssignOrderToCourierAsync(_bossId, selected.OrderId, _courierId);

            AssignedOrderId = selected.OrderId;

            // Signal to parent that an order was assigned
            DialogResult = true;

            ShowAssignmentSuccess(selected);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ Failed to assign order: {ex.Message}",
                "Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "❌ Assignment failed";
        }
        finally
        {
            EndBusy();
        }
    }

    private bool ConfirmAssignment(OpenOrderInList selected)
    {
        var result = MessageBox.Show(
            $"Assign Order #{selected.OrderId}?\n\n" +
            $"Address: {selected.CustomerAddress}\n" +
            $"Type: {selected.OrderType}" +
            (selected.Fragility != null ? $" ({selected.Fragility})" : "") + "\n" +
            $"Bird Distance: {selected.BirdDistance:F1} km\n" +
            $"Status: {selected.ScheduleStatus}\n" +
            $"Est. Delivery: {selected.EstimatedDeliveryTime:hh\\:mm}\n" +
            $"Deadline: {selected.MaxDeliveredTime:HH:mm}\n\n" +
            "This order will be assigned to you immediately.",
            "Confirm Order Assignment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    private void ShowAssignmentSuccess(OpenOrderInList selected)
    {
        MessageBox.Show(
            $"✅ Order #{selected.OrderId} Assigned Successfully!\n\n" +
            $"Delivery Address: {selected.CustomerAddress}\n" +
            $"Bird Distance: {selected.BirdDistance:F1} km\n" +
            $"Status: {selected.ScheduleStatus}\n\n" +
            "You can now view your active delivery in the main dashboard.",
            "Order Assigned",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BeginBusy(string statusText)
    {
        txtStatus.Text = statusText;
        Mouse.OverrideCursor = Cursors.Wait;
    }

    private void EndBusy()
    {
        Mouse.OverrideCursor = null;
    }

    private void ShowErrorAndClose(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
    }
}
