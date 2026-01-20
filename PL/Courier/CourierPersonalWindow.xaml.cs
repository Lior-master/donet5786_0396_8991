using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BlApi;
using BO;
using Helpers;
using PL.Courier;

namespace PL;

/// <summary>
/// Interaction logic for CourierPersonalWindow.xaml
/// This window displays a courier's personal dashboard when they log in
/// Shows their profile information, current delivery status, and allows them to manage their orders
/// Implements INotifyPropertyChanged for two-way data binding
/// </summary>
public partial class CourierPersonalWindow : Window, INotifyPropertyChanged
{
    #region Private Fields

    private static readonly IBl s_bl = Factory.Get();
    private readonly int _courierId;
    private readonly int _bossId;
    private readonly Action _courierObserver;

    // Stage 7: prevents concurrent re-entrant refreshes from observer callbacks
    private readonly ObserverMutex _courierItemMutex = new(); // stage 7

    private bool _observerRegistered = false;

    public BO.DeliveredStatus SelectedFinishType { get; set; } = BO.DeliveredStatus.Delivered;

    #endregion

    #region Properties for Data Binding

    private Visibility _isAnOrderInProgress;
    public Visibility IsAnOrderInProgress
    {
        get => _isAnOrderInProgress;
        set
        {
            _isAnOrderInProgress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNoOrderInProgress));
        }
    }

    public Visibility IsNoOrderInProgress =>
        IsAnOrderInProgress == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public int CourierId => _courierId;

    private BO.Courier? _courier;
    public BO.Courier? Courier
    {
        get => _courier;
        set
        {
            _courier = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanChooseOrder));
            OnPropertyChanged(nameof(DisplayDistanceText));
        }
    }

    private OrderInProgress? _orderInProgress;
    public OrderInProgress? OrderInProgress
    {
        get => _orderInProgress;
        set
        {
            _orderInProgress = value;
            IsAnOrderInProgress = value == null ? Visibility.Collapsed : Visibility.Visible;
            OnPropertyChanged();

            OnPropertyChanged(nameof(CanChooseOrder));
            OnPropertyChanged(nameof(DisplayDistanceText));
            OnPropertyChanged(nameof(DeliveryStatusText));
            OnPropertyChanged(nameof(IsDeliveryEndTypeVisible));
        }
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Computed / Helper Properties

    public bool CanChooseOrder => Courier != null && Courier.IsActive && OrderInProgress == null;

    public string DisplayDistanceText
    {
        get
        {
            if (OrderInProgress == null)
                return "N/A";

            double? orderDist = OrderInProgress.Distance;
            double? max = Courier?.MaxDistance;

            if (!orderDist.HasValue && !max.HasValue)
                return "Unknown";

            if (orderDist.HasValue && max.HasValue)
            {
                double used = Math.Min(orderDist.Value, max.Value);
                return $"{used:F2} km (display)";
            }

            if (orderDist.HasValue)
                return $"{orderDist.Value:F2} km";
            if (max.HasValue)
                return $"{max.Value:F2} km (max)";

            return "Unknown";
        }
    }

    public string DeliveryStatusText
    {
        get
        {
            if (OrderInProgress == null)
                return "No delivery";

            return OrderInProgress.ArrivalTime.HasValue ? "Arrived / Ending" : "In Progress";
        }
    }

    public Visibility IsDeliveryEndTypeVisible =>
        OrderInProgress != null && OrderInProgress.ArrivalTime.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Constructors

    public CourierPersonalWindow(int courierId)
    {
        InitializeComponent();

        _courierId = courierId;
        _bossId = s_bl.Admin.GetConfig().BossId;

        _courierObserver = RefreshCourierData;

        // Default UI state
        IsAnOrderInProgress = Visibility.Collapsed;
    }

    #endregion

    #region Event Handlers

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            StatusMessage = "Loading courier data...";

            // Register observer once per window instance
            if (!_observerRegistered)
            {
                s_bl.Courier.AddObserver(_courierId, _courierObserver);
                _observerRegistered = true;
            }

            await LoadCourierDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
            MessageBox.Show($"Failed to load courier data:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (!_observerRegistered)
            return;

        try
        {
            s_bl.Courier.RemoveObserver(_courierId, _courierObserver);
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _observerRegistered = false;
        }
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Courier == null)
            {
                StatusMessage = "No courier data available";
                return;
            }

            if (!ValidateProfileFields())
                return;

            Mouse.OverrideCursor = Cursors.Wait;
            StatusMessage = "Updating profile...";

            // requesterId should be the courier himself (per your BL authorization)
            s_bl.Courier.UpdateCourier(_courierId, Courier);

            StatusMessage = "Profile updated successfully!";
            MessageBox.Show("Your profile has been updated successfully.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadCourierDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
            MessageBox.Show($"Failed to update profile:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var historyWindow = new PL.Courier.CourierDeliveryHistoryWindow(_courierId)
            {
                Owner = this
            };
            historyWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open history:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnChooseOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Courier == null)
            {
                StatusMessage = "Courier data unavailable";
                return;
            }

            if (!Courier.IsActive)
            {
                StatusMessage = "You must be marked as active";
                MessageBox.Show("You must be marked as active by a manager to choose orders.",
                    "Not Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (OrderInProgress != null)
            {
                StatusMessage = "You already have an active delivery";
                MessageBox.Show("You must complete your current delivery before choosing a new one.",
                    "Active Delivery", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var choose = new PL.Courier.CourierOrderSelectionWindow(_courierId)
            {
                Owner = this
            };

            var result = choose.ShowDialog();

            if (result == true && choose.AssignedOrderId.HasValue)
            {
                var expectedOrderId = choose.AssignedOrderId.Value;

                await RefreshDataFromChildAsync();

                if (OrderInProgress?.OrderId == expectedOrderId)
                    StatusMessage = $"✅ Order #{expectedOrderId} assigned successfully!";
                else
                    await CreateOrderInProgressFromAssignedOrder(expectedOrderId);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to open order selection:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OrderInProgress == null)
            {
                StatusMessage = "No active delivery to complete";
                MessageBox.Show("There is no active delivery to complete.",
                    "No Active Delivery", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Mark delivery of Order #{OrderInProgress.OrderId} as: {SelectedFinishType}?\n\n" +
                $"Customer: {OrderInProgress.CustomerName}\n" +
                $"Address: {OrderInProgress.CustomerAddress}\n" +
                $"Status: {SelectedFinishType}",
                "Confirm Delivery Completion",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                StatusMessage = "Delivery completion cancelled";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            StatusMessage = "Completing delivery...";

            int deliveryId = OrderInProgress.DeliveryId;
            if (deliveryId == 0)
            {
                StatusMessage = "Cannot complete delivery: delivery id missing.";
                MessageBox.Show("Delivery id is missing. Please try again after refreshing.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await s_bl.Order.FinishOrderAsync(_courierId, _courierId, deliveryId, SelectedFinishType);

            StatusMessage = $"✅ Delivery marked as {SelectedFinishType}";
            MessageBox.Show($"Delivery has been completed successfully.\n\nStatus: {SelectedFinishType}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            // Immediate UI feedback; observer will later refresh actual data
            OrderInProgress = null;

            OnPropertyChanged(nameof(CanChooseOrder));
            OnPropertyChanged(nameof(IsAnOrderInProgress));
            OnPropertyChanged(nameof(IsNoOrderInProgress));

            StatusMessage = "✅ Delivery completed - Ready to choose a new order";
        }
        catch (BO.BLBadAddressException ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Address Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Completion failed: {ex.Message}";
            MessageBox.Show($"Failed to complete delivery:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadCourierDataAsync()
    {
        try
        {
            var courierData = await s_bl.Courier.GetCourierDetailsAsync(_bossId, _courierId);

            Courier = courierData;
            OrderInProgress = courierData.CurrentOrder;

            StatusMessage = $"Data loaded - {courierData.Name}";
        }
        catch (BO.BLNotFoundException)
        {
            MessageBox.Show("Your courier profile has been removed by an administrator.",
                "Profile Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }

    /// <summary>
    /// Called by the observer pattern when courier data changes.
    /// Stage 7-safe: observer can be invoked from background threads.
    /// Uses Dispatcher + ObserverMutex to prevent overlapping refreshes.
    /// </summary>
    private void RefreshCourierData()
    {
        if (_courierItemMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await LoadCourierDataAsync();
                StatusMessage = "Data refreshed";
            }
            catch
            {
                // keep observer resilient
            }
            finally
            {
                if (await _courierItemMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    RefreshCourierData();
            }
        }));
    }

    /// <summary>
    /// Called by observer / child windows to refresh data on the courier screen asynchronously.
    /// Returns a Task that completes when the refresh is done.
    /// </summary>
    public Task RefreshDataFromChildAsync()
    {
        var tcs = new TaskCompletionSource<object?>();

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await LoadCourierDataAsync();

                StatusMessage = OrderInProgress != null
                    ? $"✅ Order #{OrderInProgress.OrderId} assigned successfully!"
                    : "Ready to choose a new order";

                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
                tcs.TrySetResult(null);
            }
        }));

        return tcs.Task;
    }

    private bool ValidateProfileFields()
    {
        if (Courier == null)
            return false;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Courier.Name))
            errors.Add("Courier name is required");

        if (string.IsNullOrWhiteSpace(Courier.Phone))
            errors.Add("Phone number is required");

        if (string.IsNullOrWhiteSpace(Courier.Email))
            errors.Add("Email address is required");

        if (!Courier.Email.Contains("@"))
            errors.Add("Email address format is invalid");

        if (!Courier.MaxDistance.HasValue || Courier.MaxDistance <= 0)
            errors.Add("Max distance must be a positive number");

        if (errors.Count > 0)
        {
            MessageBox.Show(
                "Please fix the following issues:\n\n" + string.Join("\n", errors),
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private async Task CreateOrderInProgressFromAssignedOrder(int orderId)
    {
        try
        {
            OrderInProgress = await s_bl.Order.GetOrderInProgressSnapshotAsync(_courierId, _courierId, orderId);
            StatusMessage = $"Order #{orderId} assigned (manual fallback).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating OrderInProgress: {ex.Message}";
        }
    }

    #endregion
}
