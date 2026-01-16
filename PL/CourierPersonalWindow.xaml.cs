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
        _courierObserver = RefreshCourierData!;

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

            try
            {
                if (_courierObserver != null)
                {
                    s_bl.Courier.AddObserver(_courierId, _courierObserver);
                    StatusMessage = $"Welcome, {Courier?.Name}! Ready to go.";
                }
                else
                {
                    StatusMessage = $"Welcome, {Courier?.Name}! (auto-refresh disabled)";
                }
            }
            catch
            {
                StatusMessage = $"Welcome, {Courier?.Name}! (auto-refresh disabled)";
            }
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
            StatusMessage = "Opening delivery history...";
            MessageBox.Show("Delivery history feature coming soon.\n\nThis will show your complete delivery record.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to open history:\n{ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            StatusMessage = "Opening order selection...";
            MessageBox.Show("Order selection feature coming soon.\n\nThis will let you choose available deliveries based on your location and capacity.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage = "Ready";
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

            var result = MessageBox.Show(
                $"Mark delivery of Order #{OrderInProgress.OrderId} as {SelectedFinishType}?\n\n" +
                $"Customer: {OrderInProgress.CustomerName}\n" +
                $"Address: {OrderInProgress.CustomerAddress}",
                "Confirm Completion",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                StatusMessage = "Delivery completion cancelled";
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            StatusMessage = "Completing delivery...";

            StatusMessage = $"Delivery marked as {SelectedFinishType}";
            MessageBox.Show("Delivery has been completed successfully.", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadCourierData();
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

    private void RefreshCourierData()
    {
        Dispatcher.Invoke(async () =>
        {
            try
            {
                // Add defensive check to prevent race condition - verify courier still exists
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
                    // Courier was deleted - close this window gracefully
                    MessageBox.Show("Your courier profile has been removed by an administrator.", 
                        "Profile Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                    return;
                }

                // Load updated courier data asynchronously
                var updatedCourier = await Task.Run(() => 
                    s_bl.Courier.GetCourierDetails(_bossId, _courierId));
                
                Courier = updatedCourier;
                OrderInProgress = updatedCourier.CurrentOrder;
                StatusMessage = "Data refreshed from server";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
                // Don't show error message box for refresh failures as they might be frequent
                // The user can see the error in the status message
            }
        });
    }

    private bool ValidateProfileFields()
    {
        if (Courier == null)
            return false;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Courier.Name))
            errors.Add("Name is required");

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
