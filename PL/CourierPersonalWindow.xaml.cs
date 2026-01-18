using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BlApi;
using BO;   
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
    private int _bossId;
    private readonly Action? _courierObserver;

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
        }
    }

    private Visibility _isNoOrderInProgress;
    public Visibility IsNoOrderInProgress =>
        IsAnOrderInProgress == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private int _courierId_Display;
    public int CourierId
    {
        get => _courierId_Display;
        set
        {
            _courierId_Display = value;
            OnPropertyChanged();
        }
    }

    private BO.Courier? _courier;
    public BO.Courier? Courier
    {
        get => _courier;
        set
        {
            _courier = value;
            OnPropertyChanged();
            // computed properties depend on courier
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
            // computed properties depend on order in progress
            OnPropertyChanged(nameof(CanChooseOrder));
            OnPropertyChanged(nameof(DisplayDistanceText));
            OnPropertyChanged(nameof(DeliveryStatusText));
            OnPropertyChanged(nameof(IsDeliveryEndTypeVisible));
        }
    }

    private ObservableCollection<DeliveryTransport>? _deliveryTypes;
    public ObservableCollection<DeliveryTransport> DeliveryTypes
    {
        get => _deliveryTypes!;
        set
        {
            _deliveryTypes = value;
            OnPropertyChanged();
        }
    }

    private ObservableCollection<DeliveredStatus>? _deliveryFinishTypes;
    public ObservableCollection<DeliveredStatus> DeliveryFinishTypes
    {
        get => _deliveryFinishTypes!;
        set
        {
            _deliveryFinishTypes = value;
            OnPropertyChanged();
        }
    }

    private DeliveredStatus _selectedFinishType;
    public DeliveredStatus SelectedFinishType
    {
        get => _selectedFinishType;
        set
        {
            _selectedFinishType = value;
            OnPropertyChanged();
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

    #region History properties (no new files)

    private Visibility _historyVisibility = Visibility.Collapsed;
    public Visibility HistoryVisibility
    {
        get => _historyVisibility;
        set { _historyVisibility = value; OnPropertyChanged(); }
    }

    private ObservableCollection<BO.ClosedDeliveryInList> _deliveryHistory = new();
    public ObservableCollection<BO.ClosedDeliveryInList> DeliveryHistory
    {
        get => _deliveryHistory;
        set { _deliveryHistory = value; OnPropertyChanged(); }
    }

    private string _historyStatusMessage = "Ready";
    public string HistoryStatusMessage
    {
        get => _historyStatusMessage;
        set { _historyStatusMessage = value; OnPropertyChanged(); }
    }

    #endregion

    #region Computed / Helper Properties

    // Whether the courier may choose an order: must be active AND must have no current order.
    public bool CanChooseOrder => Courier != null && Courier.IsActive && OrderInProgress == null;

    // Display distance: smaller between courier max distance and order distance when available.
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

            // one of them available
            if (orderDist.HasValue)
                return $"{orderDist.Value:F2} km";
            if (max.HasValue)
                return $"{max.Value:F2} km (max)";

            return "Unknown";
        }
    }

    // Text describing whether the delivery is in-progress or arrival recorded (used as "type of delivery")
    public string DeliveryStatusText
    {
        get
        {
            if (OrderInProgress == null)
                return "No delivery";

            return OrderInProgress.ArrivalTime.HasValue ? "Arrived / Ending" : "In Progress";
        }
    }

    // Visibility for Delivery End Type (should be shown only when arrival is recorded)
    public Visibility IsDeliveryEndTypeVisible => OrderInProgress != null && OrderInProgress.ArrivalTime.HasValue ? Visibility.Visible : Visibility.Collapsed;

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
        CourierId = courierId;
        _bossId = s_bl.Admin.GetConfig().BossId;
        // FIX: Register the observer properly - it should be a method reference that matches Action signature
        _courierObserver = RefreshCourierData;
        
        // Register the observer with the business logic
        s_bl.Courier.AddObserver(_courierId, _courierObserver);

        DeliveryTypes = new ObservableCollection<DeliveryTransport>
        {
            DeliveryTransport.Car,
            DeliveryTransport.Motorcycle,
            DeliveryTransport.Bike,
            DeliveryTransport.Foot
        };

        DeliveryFinishTypes = new ObservableCollection<DeliveredStatus>
        {
            DeliveredStatus.Delivered,
            DeliveredStatus.Rejected,
            DeliveredStatus.Canceled,
            DeliveredStatus.Absent,
            DeliveredStatus.Failed
        };

        SelectedFinishType = DeliveredStatus.Delivered;

        // init history collection
        DeliveryHistory = new ObservableCollection<BO.ClosedDeliveryInList>();
    }

    #endregion

    #region Event Handlers

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            StatusMessage = "Loading courier data...";

            LoadCourierData();
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
        try
        {
            if (_courierObserver != null)
                s_bl.Courier.RemoveObserver(_courierId, _courierObserver);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
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

            s_bl.Courier.UpdateCourier(CourierId, Courier);

            StatusMessage = "Profile updated successfully!";
            MessageBox.Show("Your profile has been updated successfully.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadCourierData();
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
            // Open the history window (if present)
            var historyWindow = new PL.Courier.CourierDeliveryHistoryWindow(_courierId)
            {
                Owner = this
            };
            historyWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open history:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnChooseOrder_Click(object sender, RoutedEventArgs e)
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

            // Open the order selection window
            var choose = new PL.Courier.CourierOrderSelectionWindow(_courierId)
            {
                Owner = this
            };
            choose.Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to open order selection:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
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

            // Confirm with selected finish type for user's clarity
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

            // BL expects requesterId (who calls), courierId, deliveryId
            int deliveryId = OrderInProgress.DeliveryId;
            if (deliveryId == 0)
            {
                StatusMessage = "Cannot complete delivery: delivery id missing.";
                MessageBox.Show("Delivery id is missing. Please try again after refreshing.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            s_bl.Order.FinishOrder(_courierId, _courierId, deliveryId);

            StatusMessage = $"✅ Delivery marked as {SelectedFinishType}";
            MessageBox.Show($"Delivery has been completed successfully.\n\nStatus: {SelectedFinishType}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            // Clear the order immediately - this will hide the "Current Delivery" section
            // and show the "No Active Delivery" message with the "Choose Order" button enabled
            OrderInProgress = null;
            StatusMessage = "Ready to choose a new order";
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

    private async void RefreshCourierDataAndContinue()
    {
        await Task.Run(async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var courierExists = await Task.Run(() =>
                    {
                        try
                        {
                            s_bl.Courier.GetCourierDetails(_bossId, _courierId);
                            return true;
                        }
                        catch (BO.BLNotFoundException)
                        {
                            return false;
                        }
                    });

                    if (!courierExists)
                    {
                        MessageBox.Show("Your courier profile has been removed by an administrator.",
                            "Profile Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                        Close();
                        return;
                    }

                    var updatedCourier = await Task.Run(() =>
                        s_bl.Courier.GetCourierDetails(_bossId, _courierId));

                    Courier = updatedCourier;
                    OrderInProgress = updatedCourier.CurrentOrder;
                    StatusMessage = "Ready to choose a new order";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Refresh failed: {ex.Message}";
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        });
    }

    #endregion

    #region Private Methods

    private void LoadCourierData()
    {
        try
        {
            var courierData = s_bl.Courier.GetCourierDetails(_bossId, _courierId);
            Courier = courierData;
            OrderInProgress = courierData.CurrentOrder;
            StatusMessage = $"Data loaded - {courierData.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load data: {ex.Message}";
            throw;
        }
    }

    /// <summary>
    /// Called by the observer pattern when courier data changes.
    /// Refreshes the current courier information on the UI thread.
    /// </summary>
    private void RefreshCourierData()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                var courierData = s_bl.Courier.GetCourierDetails(_bossId, _courierId);
                Courier = courierData;
                OrderInProgress = courierData.CurrentOrder;
                StatusMessage = "Data refreshed";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to refresh data: {ex.Message}";
        }
    }

    /// <summary>
    /// Called by observer / child windows to refresh data on the courier screen asynchronously.
    /// Returns a Task that completes when the refresh is done.
    /// </summary>
    public Task RefreshDataFromChildAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        
        Dispatcher.Invoke(async () =>
        {
            try
            {
                var courierExists = await Task.Run(() =>
                {
                    try
                    {
                        s_bl.Courier.GetCourierDetails(_bossId, _courierId);
                        return true;
                    }
                    catch (BO.BLNotFoundException)
                    {
                        return false;
                    }
                });

                if (!courierExists)
                {
                    MessageBox.Show("Your courier profile has been removed by an administrator.",
                        "Profile Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                    tcs.SetResult(false);
                    return;
                }

                var updatedCourier = await Task.Run(() =>
                    s_bl.Courier.GetCourierDetails(_bossId, _courierId));

                Courier = updatedCourier;
                OrderInProgress = updatedCourier.CurrentOrder;
                StatusMessage = "Order assigned successfully!";
                
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private bool ValidateProfileFields()
    {
        if (Courier == null)
            return false;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Courier.Name))
            errors.Add("CourierName is required");

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

    #endregion
}
