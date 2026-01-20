using BlApi;
using BO;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PL.Courier;

/// <summary>
/// Window that lists open orders available to the courier and allows assignment.
/// Relies on BL observers for refresh (AssignOrderToCourier triggers notifications).
/// Keeps UI logic minimal: mostly delegates to small helper methods.
/// </summary>
public partial class CourierOrderSelectionWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private readonly int _courierId;
    private readonly int _bossId;
    private readonly Action _ordersObserver;
    private bool _isLoading;

    // Property to track the assigned order ID
    public int? AssignedOrderId { get; private set; }

    public ObservableCollection<OpenOrderInList> OpenOrders { get; } = new();

    private string _filterStatusMessage = string.Empty;
    public string FilterStatusMessage
    {
        get => _filterStatusMessage;
        set { _filterStatusMessage = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public CourierOrderSelectionWindow(int courierId)
    {
        InitializeComponent();

        _courierId = courierId;
        _bossId = s_bl.Admin.GetConfig().BossId;

        DataContext = this;
        lstOpenOrders.ItemsSource = OpenOrders;

        // Observer must marshal back to UI thread
        _ordersObserver = () => _ = Dispatcher.InvokeAsync(RefreshOpenOrdersAsync);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            InitializeFilterCombo();

            // Register observer so BL notifications update this window automatically
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
        try { s_bl.Order.AddObserver(_ordersObserver); } catch { }
    }

    private void TryUnregisterObserver()
    {
        try { s_bl.Order.RemoveObserver(_ordersObserver); } catch { }
    }

    private BO.OrderType? GetSelectedFilter()
    {
        return cmbFilterOrderType.SelectedItem is BO.OrderType ot ? ot : null;
    }

    private async Task RefreshOpenOrdersAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        var filter = GetSelectedFilter();
        try
        {
            txtStatus.Text = "Loading open orders...";
            var list = await Task.Run(() => GetOpenOrdersFromBl(filter));
            UpdateOpenOrdersCollection(list);
            UpdateStatusMessages(filter);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private System.Collections.Generic.List<OpenOrderInList> GetOpenOrdersFromBl(BO.OrderType? filter)
    {
        // Minimal local logic: let BL do filtering; PL only does basic display ordering
        return s_bl.Order.GetOpenOrdersForCourier(_bossId, _courierId, filter, null).ToList();
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
        if (filter == null)
            FilterStatusMessage = "Showing all available orders";
        else
            FilterStatusMessage = $"Filtered by: {filter}";
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

            // Assigner la commande au coursier
            await Task.Run(() =>
                s_bl.Order.AssignOrderToCourier(_bossId, selected.OrderId, _courierId));

            // IMPORTANT: Stocker l'ID de l'ordre assigné
            AssignedOrderId = selected.OrderId;

            // Set DialogResult to true to indicate successful assignment
            DialogResult = true;

            // Montrer le succès
            ShowAssignmentSuccess(selected);
            
            // Fermer cette fenêtre - le parent récupérera AssignedOrderId
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
