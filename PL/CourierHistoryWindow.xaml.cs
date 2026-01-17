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
/// Delivery history window showing closed deliveries for the logged courier.
/// </summary>
public partial class CourierDeliveryHistoryWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private readonly int _courierId;
    private Action? _orderListObserver;

    public ObservableCollection<ClosedDeliveryInList> Deliveries { get; private set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public CourierDeliveryHistoryWindow(int courierId)
    {
        InitializeComponent();
        _courierId = courierId;
        dgHistory.ItemsSource = Deliveries;
        _orderListObserver = RefreshFromBl;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Populate filter combo: "All" + OrderType enum values
            cmbFilterOrderType.Items.Clear();
            cmbFilterOrderType.Items.Add("All");
            foreach (var v in Enum.GetValues(typeof(BO.OrderType)).Cast<BO.OrderType>())
                cmbFilterOrderType.Items.Add(v);
            cmbFilterOrderType.SelectedIndex = 0;

            // Register observer to auto-refresh when orders/deliveries change
            try { s_bl.Order.AddObserver(_orderListObserver!); } catch { /* ignore if BL doesn't support observers */ }

            LoadHistory(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        try { if (_orderListObserver != null) s_bl.Order.RemoveObserver(_orderListObserver); } catch { }
    }

    private void LoadHistory(BO.OrderType? filter)
    {
        try
        {
            txtStatus.Text = "Loading...";
            // requesterId is the courier themselves (allowed by BL)
            var list = s_bl.Order.GetClosedDeliveriesForCourier(_courierId, _courierId, filter, null)
                .OrderByDescending(d => d.DeliveryId)
                .ToList();

            Deliveries.Clear();
            foreach (var d in list) Deliveries.Add(d);

            if (Deliveries.Count == 0)
            {
                // Attempt to fetch as boss to detect authorization/filter issues
                try
                {
                    var bossId = s_bl.Admin.GetConfig().BossId;
                    var bossList = s_bl.Order.GetClosedDeliveriesForCourier(bossId, _courierId, filter, null).ToList();
                    if (bossList.Count > 0)
                    {
                        txtStatus.Text = $"0 deliveries for courier requester. {bossList.Count} found when queried by boss.";
                        MessageBox.Show(
                            "No closed deliveries were returned when requesting as the courier.\n" +
                            "However the boss account returned closed deliveries for this courier. This indicates an authorization/filtering issue in the BL for courier requester.\n\n" +
                            "I recommend checking the BL method GetClosedDeliveriesForCourier requester/authorization logic.",
                            "History: Authorization Puzzle", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch
                {
                    // ignore boss check errors
                }

                txtStatus.Text = "No closed deliveries found for this courier.";
                MessageBox.Show("No closed deliveries found for this courier.", "History", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            txtStatus.Text = $"Loaded {Deliveries.Count} closed deliveries";
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to load history:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshFromBl()
    {
        Dispatcher.Invoke(() =>
        {
            BO.OrderType? filter = null;
            if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
            LoadHistory(filter);
        });
    }

    private void cmbFilterOrderType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BO.OrderType? filter = null;
        if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
        LoadHistory(filter);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BO.OrderType? filter = null;
        if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
        LoadHistory(filter);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}