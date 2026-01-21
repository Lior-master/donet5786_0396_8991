using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using BlApi;
using BO;
using Helpers;

/// <summary>
/// Implements the presentation layer UI and related view models.
/// </summary>
namespace PL.Courier;

/// <summary>
/// Represents the courier delivery history window component in this layer.
/// Uses ListBox + full data binding.
/// </summary>
public partial class CourierDeliveryHistoryWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    /// <summary>
    /// Stores the courier id value.
    /// </summary>
    private readonly int _courierId;
    /// <summary>
    /// Stores the order list observer value.
    /// </summary>
    private readonly Action _orderListObserver;

    // Stage 7: prevents concurrent re-entrant refreshes from observer callbacks
    private readonly ObserverMutex _historyMutex = new(); // stage 7

    /// <summary>
    /// Stores the observer registered value.
    /// </summary>
    private bool _observerRegistered = false;

    // =========================
    // Bindable collections
    // =========================

    /// <summary>
    /// Performs the operation.
    /// </summary>
    public ObservableCollection<ClosedDeliveryInList> Deliveries { get; } = new();

    /// <summary>
    /// Performs the operation.
    /// </summary>
    public ObservableCollection<OrderTypeFilterItem> OrderTypeFilters { get; } = new();

    // =========================
    // Bindable selected items
    // =========================

    /// <summary>
    /// Stores the selected delivery value.
    /// </summary>
    private ClosedDeliveryInList? _selectedDelivery;
    /// <summary>
    /// Gets or sets the selected delivery value.
    /// </summary>
    public ClosedDeliveryInList? SelectedDelivery
    {
        get => _selectedDelivery;
        set
        {
            _selectedDelivery = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Stores the selected order type filter value.
    /// </summary>
    private OrderTypeFilterItem? _selectedOrderTypeFilter;
    /// <summary>
    /// Gets or sets the selected order type filter value.
    /// </summary>
    public OrderTypeFilterItem? SelectedOrderTypeFilter
    {
        get => _selectedOrderTypeFilter;
        set
        {
            _selectedOrderTypeFilter = value;
            OnPropertyChanged();

            // UI-triggered refresh
            LoadHistory(value?.OrderType);
        }
    }

    // =========================
    // ctor
    // =========================

    /// <summary>
    /// Initializes a new instance of the CourierDeliveryHistoryWindow class.
    /// </summary>
    /// <param name="courierId">The courier id value.</param>
    public CourierDeliveryHistoryWindow(int courierId)
    {
        InitializeComponent();
        _courierId = courierId;

        _orderListObserver = RefreshFromBl;

        // Prefer explicit hooks (works even if not wired in XAML)
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    // =========================
    // Window lifecycle
    // =========================

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        OrderTypeFilters.Clear();
        OrderTypeFilters.Add(new OrderTypeFilterItem("All", null));

        foreach (var ot in Enum.GetValues(typeof(OrderType)).Cast<OrderType>())
            OrderTypeFilters.Add(new OrderTypeFilterItem(ot.ToString(), ot));

        SelectedOrderTypeFilter = OrderTypeFilters.First();

        if (_observerRegistered)
            return;

        try
        {
            s_bl.Order.AddObserver(_orderListObserver);
            _observerRegistered = true;
        }
        catch
        {
            // ignore if BL doesn't support / throws
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (!_observerRegistered)
            return;

        try
        {
            s_bl.Order.RemoveObserver(_orderListObserver);
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

    // =========================
    // Data loading
    // =========================

    private void LoadHistory(OrderType? filter)
    {
        try
        {
            var list = s_bl.Order
                .GetClosedDeliveriesForCourier(_courierId, _courierId, filter, null)
                .OrderByDescending(d => d.DeliveryId)
                .ToList();

            Deliveries.Clear();
            foreach (var d in list)
                Deliveries.Add(d);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load delivery history:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================
    // Stage 7-safe observer callback
    // =========================

    /// <summary>
    /// Refreshes courier history in response to order observer notifications.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Dispatcher"/> to update the UI thread and <see cref="ObserverMutex"/>
    /// to prevent overlapping refreshes. If a notification arrives mid-refresh, the mutex
    /// requests a restart so the latest history is displayed (stage 7 observer pattern).
    /// </remarks>
    private void RefreshFromBl()
    {
        if (_historyMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                LoadHistory(SelectedOrderTypeFilter?.OrderType);
            }
            catch
            {
                // keep observer resilient
            }
            finally
            {
                if (await _historyMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    RefreshFromBl();
            }
        }));
    }

    // =========================
    // Buttons
    // =========================

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        => LoadHistory(SelectedOrderTypeFilter?.OrderType);

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    // =========================
    // INotifyPropertyChanged
    // =========================

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Represents the order type filter item component in this layer.
/// </summary>
public sealed class OrderTypeFilterItem
{
    /// <summary>
    /// Gets or sets the display value.
    /// </summary>
    public string Display { get; }
    /// <summary>
    /// Gets or sets the order type value.
    /// </summary>
    public OrderType? OrderType { get; }

    /// <summary>
    /// Initializes a new instance of the OrderTypeFilterItem class.
    /// </summary>
    /// <param name="display">The display value.</param>
    /// <param name="orderType">The order type value.</param>
    public OrderTypeFilterItem(string display, OrderType? orderType)
    {
        Display = display;
        OrderType = orderType;
    }
}
