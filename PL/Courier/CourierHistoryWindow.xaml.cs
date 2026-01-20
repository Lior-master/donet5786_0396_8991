using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using BlApi;
using BO;

namespace PL.Courier;

/// <summary>
/// Delivery history window for a courier.
/// Uses ListBox + full data binding.
/// </summary>
public partial class CourierDeliveryHistoryWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private readonly int _courierId;
    private Action? _orderListObserver;

    // =========================
    // Bindable collections
    // =========================

    public ObservableCollection<ClosedDeliveryInList> Deliveries { get; } = new();

    public ObservableCollection<OrderTypeFilterItem> OrderTypeFilters { get; } = new();

    // =========================
    // Bindable selected items
    // =========================

    private ClosedDeliveryInList? _selectedDelivery;
    public ClosedDeliveryInList? SelectedDelivery
    {
        get => _selectedDelivery;
        set
        {
            _selectedDelivery = value;
            OnPropertyChanged();
        }
    }

    private OrderTypeFilterItem? _selectedOrderTypeFilter;
    public OrderTypeFilterItem? SelectedOrderTypeFilter
    {
        get => _selectedOrderTypeFilter;
        set
        {
            _selectedOrderTypeFilter = value;
            OnPropertyChanged();
            LoadHistory(value?.OrderType);
        }
    }

    // =========================
    // ctor
    // =========================

    public CourierDeliveryHistoryWindow(int courierId)
    {
        InitializeComponent();
        _courierId = courierId;
        _orderListObserver = RefreshFromBl;
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

        try { s_bl.Order.AddObserver(_orderListObserver!); } catch { }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        try { if (_orderListObserver != null) s_bl.Order.RemoveObserver(_orderListObserver); } catch { }
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

    private void RefreshFromBl()
    {
        Dispatcher.Invoke(() =>
        {
            LoadHistory(SelectedOrderTypeFilter?.OrderType);
        });
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
/// Helper class for OrderType filtering in ComboBox.
/// </summary>
public sealed class OrderTypeFilterItem
{
    public string Display { get; }
    public OrderType? OrderType { get; }

    public OrderTypeFilterItem(string display, OrderType? orderType)
    {
        Display = display;
        OrderType = orderType;
    }
}
