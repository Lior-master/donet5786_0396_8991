using BlApi;
using BO;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PL.Courier;

/// <summary>
/// Window that lists open orders available to the courier and allows assignment.
/// </summary>
public partial class CourierOrderSelectionWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();
    private readonly int _courierId;
    private Action? _orderListObserver;

    public ObservableCollection<OpenOrderInList> OpenOrders { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public CourierOrderSelectionWindow(int courierId)
    {
        InitializeComponent();
        _courierId = courierId;
        dgOpenOrders.ItemsSource = OpenOrders;
        _orderListObserver = RefreshFromBl;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Populate filter combo
            cmbFilterOrderType.Items.Clear();
            cmbFilterOrderType.Items.Add("All");
            foreach (var t in Enum.GetValues(typeof(BO.OrderType)).Cast<BO.OrderType>())
                cmbFilterOrderType.Items.Add(t);
            cmbFilterOrderType.SelectedIndex = 0;

            // register observer (if supported) so list auto-refreshes on changes
            try { s_bl.Order.AddObserver(_orderListObserver!); } catch { }

            LoadOpenOrders(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load open orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        try { if (_orderListObserver != null) s_bl.Order.RemoveObserver(_orderListObserver); } catch { }
    }

    private void LoadOpenOrders(BO.OrderType? filter)
    {
        try
        {
            txtStatus.Text = "Loading open orders...";
            var list = s_bl.Order.GetOpenOrdersForCourier(_courierId, _courierId, filter, null)
                .OrderBy(o => o.OrderId)
                .ToList();

            OpenOrders.Clear();
            foreach (var o in list) OpenOrders.Add(o);

            txtStatus.Text = $"Loaded {OpenOrders.Count} open orders";
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void RefreshFromBl()
    {
        Dispatcher.Invoke(() =>
        {
            BO.OrderType? filter = null;
            if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
            LoadOpenOrders(filter);
        });
    }

    private void cmbFilterOrderType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BO.OrderType? filter = null;
        if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
        LoadOpenOrders(filter);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BO.OrderType? filter = null;
        if (cmbFilterOrderType.SelectedItem is BO.OrderType ot) filter = ot;
        LoadOpenOrders(filter);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async void BtnAssign_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (dgOpenOrders.SelectedItem is not OpenOrderInList selected)
            {
                MessageBox.Show("Please select an order to assign.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show($"Assign order #{selected.OrderId} to you?", "Confirm Assignment", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (res != MessageBoxResult.OK)
                return;

            txtStatus.Text = "Assigning order...";
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            // requesterId = courier themselves
            s_bl.Order.AssignOrderToCourier(_courierId, selected.OrderId, _courierId);

            MessageBox.Show($"Order #{selected.OrderId} assigned to you.", "Assigned", MessageBoxButton.OK, MessageBoxImage.Information);

            // attempt to refresh the owner courier window if available
            if (Owner is PL.CourierPersonalWindow cpw)
            {
                try 
                { 
                    // Wait for the data to be refreshed
                    await cpw.RefreshDataFromChildAsync(); 
                } 
                catch { /* ignore */ }
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to assign order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "Error assigning order";
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void BtnChooseOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not OpenOrderInList selected)
            {
                MessageBox.Show("Error retrieving order information.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var res = MessageBox.Show($"Assign order #{selected.OrderId} to you?", "Confirm Assignment", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (res != MessageBoxResult.OK)
                return;

            txtStatus.Text = "Assigning order...";
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            // requesterId = courier themselves
            s_bl.Order.AssignOrderToCourier(_courierId, selected.OrderId, _courierId);

            MessageBox.Show($"Order #{selected.OrderId} assigned to you.", "Assigned", MessageBoxButton.OK, MessageBoxImage.Information);

            // attempt to refresh the owner courier window if available
            if (Owner is PL.CourierPersonalWindow cpw)
            {
                try 
                { 
                    // Wait for the data to be refreshed
                    await cpw.RefreshDataFromChildAsync(); 
                } 
                catch { /* ignore */ }
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to assign order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "Error assigning order";
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

}