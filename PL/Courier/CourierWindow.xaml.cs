using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BlApi;
using BO;
using Helpers;

namespace PL.Courier;

/// <summary>
/// Interaction logic for CourierWindow.xaml - handles courier creation and editing
/// Supports both create mode (new courier) and update mode (existing courier)
/// Implements INotifyPropertyChanged for two-way data binding with XAML
/// </summary>
public partial class CourierWindow : Window, INotifyPropertyChanged
{
    #region Private Fields

    /// <summary>
    /// Business logic layer interface for data operations
    /// </summary>
    private static readonly IBl s_bl = Factory.Get();

    /// <summary>
    /// Current boss/admin ID for authorization of operations
    /// </summary>
    private readonly int _bossId;

    /// <summary>
    /// Indicates if window is in create mode (true) or update mode (false)
    /// </summary>
    private readonly bool _isCreateMode;

    /// <summary>
    /// ID of courier being edited (null in create mode)
    /// </summary>
    private readonly int? _courierId;

    /// <summary>
    /// Observer action for real-time courier updates from business layer
    /// </summary>
    private readonly Action? _courierObserver;

    /// <summary>
    /// Stage 7: prevents concurrent re-entrant refreshes from background threads
    /// </summary>
    private readonly ObserverMutex _courierItemMutex = new(); // stage 7

    #endregion

    #region Properties for Data Binding

    private bool _haveAssignedOrder;
    public bool HaveAssignedOrder
    {
        get => _haveAssignedOrder;
        set
        {
            _haveAssignedOrder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HaveAssignedOrderInverted));
        }
    }

    public bool HaveAssignedOrderInverted => !_haveAssignedOrder;

    private bool _isReadOnly;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set
        {
            _isReadOnly = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReadOnlyInverted));
        }
    }

    public bool IsReadOnlyInverted => !IsReadOnly;

    private BO.Courier? _courierCurrent;
    public BO.Courier CourierCurrent
    {
        get => _courierCurrent!;
        set
        {
            _courierCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRemoveCourier));
            OnPropertyChanged(nameof(CurrentOrderDisplay));
        }
    }

    public string CurrentOrderDisplay
    {
        get
        {
            if (CourierCurrent?.CurrentOrder == null)
                return "No current order assigned.";

            var order = CourierCurrent.CurrentOrder;
            return $"Order ID: {order.OrderId}\n" +
                   $"Customer: {order.CustomerName}\n" +
                   $"Address: {order.CustomerAddress}\n" +
                   $"Phone: {order.CustomerPhone}\n" +
                   $"Status: {order.OrderStatus}\n" +
                   $"Pickup Time: {order.PickupTime:dd/MM/yyyy HH:mm}\n" +
                   $"Distance: {(order.Distance?.ToString("F1") ?? "Unknown")} km";
        }
    }

    public string SaveButtonText => _isCreateMode ? "➕ Add" : "💾 Save";

    public bool CanRemoveCourier
    {
        get
        {
            if (_isCreateMode || CourierCurrent == null)
                return false;

            return CourierCurrent.NumberOfOnTimeDeliveries == 0
                   && CourierCurrent.NumberOfLateDeliveries == 0
                   && CourierCurrent.CurrentOrder == null;
        }
    }

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Constructors

    /// <summary>
    /// Create mode constructor (new courier).
    /// Initializes window for adding a new courier with default values
    /// </summary>
    public CourierWindow()
    {
        InitializeComponent();

        _bossId = s_bl.Admin.GetConfig().BossId;
        _isCreateMode = true;
        IsReadOnly = false;

        CourierCurrent = new BO.Courier
        {
            Id = 0,
            Name = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Password = string.Empty,
            IsActive = true,
            Transport = DeliveryTransport.Car,
            MaxDistance = null,
            StartDate = s_bl.Admin.GetClock(),
            Administrator = BO.Administrator.Courier
        };

        OnPropertyChanged(nameof(SaveButtonText));
    }

    /// <summary>
    /// Update mode constructor (existing courier).
    /// Loads existing courier data and sets up real-time updates via observer pattern
    /// </summary>
    public CourierWindow(int courierId)
    {
        InitializeComponent();

        _bossId = s_bl.Admin.GetConfig().BossId;
        _isCreateMode = false;
        IsReadOnly = true;

        _courierId = courierId;
        _courierObserver = RefreshCourierFromBl;

        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(SaveButtonText));

        Loaded += CourierWindow_Loaded;
        Closed += CourierWindow_Closed;
    }

    #endregion

    #region Private Methods

    private async void CourierWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isCreateMode || _courierId is null || _courierObserver is null)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // Register BEFORE fetching so we don't miss updates
            s_bl.Courier.AddObserver(_courierId.Value, _courierObserver);

            CourierCurrent = await s_bl.Courier.GetCourierDetailsAsync(_bossId, _courierId.Value);

            HaveAssignedOrder = CourierCurrent.CurrentOrder is not null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error loading courier",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void CourierWindow_Closed(object? sender, EventArgs e)
    {
        if (_isCreateMode || _courierId is null || _courierObserver is null)
            return;

        try
        {
            s_bl.Courier.RemoveObserver(_courierId.Value, _courierObserver);
        }
        catch
        {
            // ignore shutdown issues
        }
    }

    /// <summary>
    /// Refreshes courier data from business layer when notified of changes.
    /// Stage 7-safe: observer can be invoked from background threads.
    /// </summary>
    private void RefreshCourierFromBl()
    {
        if (_courierItemMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                if (_isCreateMode || _courierId is null)
                    return;

                try
                {
                    // Single BL call:
                    // If courier was deleted -> BLNotFoundException -> close window gracefully
                    CourierCurrent = await s_bl.Courier.GetCourierDetailsAsync(_bossId, _courierId.Value);
                    HaveAssignedOrder = CourierCurrent.CurrentOrder is not null;
                }
                catch (BO.BLNotFoundException)
                {
                    MessageBox.Show("This courier has been deleted.", "Courier Removed",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
            catch
            {
                // keep observer resilient (avoid crashing UI due to background updates)
            }
            finally
            {
                if (await _courierItemMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    RefreshCourierFromBl();
            }
        }));
    }

    private bool ValidateFields()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CourierCurrent.Name))
            errors.Add("Courier name is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Phone))
            errors.Add("Courier phone is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Email))
            errors.Add("Courier email is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Password))
            errors.Add("Courier password is required.");

        if (!CourierCurrent.MaxDistance.HasValue || CourierCurrent.MaxDistance <= 0)
            errors.Add("Courier max distance must be a positive number.");

        if (CourierCurrent.StartDate > s_bl.Admin.GetClock().AddDays(1))
            errors.Add("Courier start date cannot be in the future.");

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

    #region Event Handlers

    private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ValidateFields())
                return;

            if (_isCreateMode)
            {
                s_bl.Courier.addCourier(_bossId, CourierCurrent);
                MessageBox.Show("Courier created successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                s_bl.Courier.UpdateCourier(_bossId, CourierCurrent);
                MessageBox.Show("Courier updated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreateMode || _courierId == null)
            return;

        try
        {
            var result = MessageBox.Show(
                $"Are you sure you want to remove courier '{CourierCurrent.Name}' (ID: {CourierCurrent.Id})?\n\nThis action cannot be undone.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int courierId = CourierCurrent.Id;
                string courierName = CourierCurrent.Name;

                s_bl.Courier.removeCourier(_bossId, courierId);
                Close();

                MessageBox.Show(
                    $"Courier '{courierName}' has been successfully removed.",
                    "Removal Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (BO.BLInvalidOperationException ex)
        {
            MessageBox.Show(
                $"Cannot remove courier '{CourierCurrent.Name}':\n{ex.Message}",
                "Removal Not Allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error removing courier '{CourierCurrent.Name}':\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion
}
