using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BlApi;
using BO;

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
    private int bossId = s_bl.Admin.GetConfig().BossId;
    
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

    #endregion

    #region Properties for Data Binding

    /// <summary>
    /// Indicates if courier has an assigned order - used for UI state management
    /// </summary>
    private bool _haveAssignedOrder;
    public bool HaveAssignedOrder
    {
        get => _haveAssignedOrder;
        set
        {
            _haveAssignedOrder = value;
            OnPropertyChanged(nameof(HaveAssignedOrderInverted));
        }
    }

    /// <summary>
    /// Inverted value of HaveAssignedOrder for enabling/disabling UI controls
    /// When courier has assigned order, certain fields should be disabled
    /// </summary>
    private bool _haveAssignedOrderInverted;
    public bool HaveAssignedOrderInverted
    {
        get => !_haveAssignedOrder;
        set
        {
            _haveAssignedOrderInverted = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Indicates if window is in read-only mode
    /// True for update mode initially, false for create mode
    /// </summary>
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

    /// <summary>
    /// Gets the inverted value of IsReadOnly for use with IsEnabled binding.
    /// When IsReadOnly is true, IsEnabled should be false, and vice versa.
    /// </summary>
    public bool IsReadOnlyInverted => !IsReadOnly;

    /// <summary>
    /// The courier object being displayed/edited
    /// Null-forgiving operator used as CourierCurrent is initialized in constructor
    /// </summary>
    private BO.Courier? _courierCurrent;
    public BO.Courier CourierCurrent
    {
        get => _courierCurrent!;
        set
        {
            _courierCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRemoveCourier));
            OnPropertyChanged(nameof(CurrentOrderDisplay)); // Notify UI that CurrentOrderDisplay has changed
        }
    }

    /// <summary>
    /// Gets a formatted string representation of the current order for display in the UI.
    /// Returns "No current order" if the courier is not currently assigned to any delivery.
    /// </summary>
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

    /// <summary>
    /// Gets the text to display on the save/add button based on the mode.
    /// Shows "Add" icon for create mode, "Save" icon for update mode
    /// </summary>
    public string SaveButtonText => _isCreateMode ? "➕ Add" : "💾 Save";

    /// <summary>
    /// Determines if the current courier can be removed.
    /// A courier can only be removed if they have no delivery history and no current order.
    /// This prevents data integrity issues and maintains delivery records
    /// </summary>
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

    /// <summary>
    /// Event raised when a property value changes - required for WPF data binding
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises PropertyChanged event for the specified property
    /// Uses CallerMemberName attribute to automatically get calling property name
    /// </summary>
    /// <param name="propertyName">CourierName of the property that changed</param>
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
        _isCreateMode = true;
        IsReadOnly = false;
        OnPropertyChanged(nameof(IsReadOnly));

        // Initialize new courier with default values
        CourierCurrent = new BO.Courier
        {
            Id = 0, // Will be auto-generated by business layer
            Name = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Password = string.Empty,
            IsActive = true,
            Transport = DeliveryTransport.Car, // Default transport method
            MaxDistance = null,
            StartDate = s_bl.Admin.GetClock(), // Use application clock for consistency
            Administrator = BO.Administrator.Courier // Default role
        };

        // Notify the UI that SaveButtonText should be refreshed
        OnPropertyChanged(nameof(SaveButtonText));
    }

    /// <summary>
    /// Update mode constructor (existing courier).
    /// Loads existing courier data and sets up real-time updates via observer pattern
    /// </summary>
    /// <param name="courierId">ID of the courier to edit</param>
    public CourierWindow(int courierId)
    {
        InitializeComponent();
        _isCreateMode = false;
        IsReadOnly = true; // Start in read-only mode for existing couriers
        

        _courierId = courierId;
        _courierObserver = RefreshCourierFromBl; // Set up observer for real-time updates

        // Notify the UI that SaveButtonText should be refreshed
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(SaveButtonText));

        // Load courier data asynchronously when window loads
        Loaded += async (_, __) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait; // Show loading cursor
                s_bl.Courier.AddObserver(courierId, _courierObserver); // Subscribe to updates
                
                // Load courier details from business layer on background thread
                CourierCurrent = await s_bl.Courier.GetCourierDetailsAsync(bossId, courierId);
                
                // Update assigned order status for UI binding
                if (CourierCurrent.CurrentOrder is not null)
                {
                    HaveAssignedOrder = true;
                    OnPropertyChanged(nameof(HaveAssignedOrder));
                }
                else
                {
                    HaveAssignedOrder = false;
                    OnPropertyChanged(nameof(HaveAssignedOrder));
                }
            }
            catch (Exception ex)
            {
                // Show error and close window if courier cannot be loaded
                MessageBox.Show(ex.Message, "Error loading courier",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                Mouse.OverrideCursor = null; // Reset cursor
            }
        };

        // Clean up observer when window closes
        Closed += (_, __) =>
        {
            if (_courierObserver is not null)
                s_bl.Courier.RemoveObserver(courierId, _courierObserver);
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Refreshes courier data from business layer when notified of changes
    /// Called by observer pattern when courier data changes in another window
    /// Handles race conditions and courier deletion scenarios
    /// </summary>
    private void RefreshCourierFromBl()
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (_isCreateMode || _courierId is null)
                return;

            try 
            {
                // Add defensive check to prevent race condition
                var courierExists = await CheckCourierExistsAsync(_courierId.Value);

                if (!courierExists)
                {
                    // Courier was deleted - close this window gracefully
                    Close();
                    return;
                }

                // Refresh courier data
                CourierCurrent = await s_bl.Courier.GetCourierDetailsAsync(bossId, _courierId.Value);
            }
            catch (BO.BLNotFoundException)
            {
                // Courier was deleted - inform user and close window
                MessageBox.Show("This courier has been deleted.", "Courier Removed", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
        });
    }

    private async Task<bool> CheckCourierExistsAsync(int courierId)
    {
        try
        {
            await s_bl.Courier.GetCourierDetailsAsync(bossId, courierId);
            return true;
        }
        catch (BO.BLNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates all required fields before saving courier data
    /// Checks for empty fields, valid ranges, and business rules
    /// </summary>
    /// <returns>True if all validations pass, false otherwise</returns>
    private bool ValidateFields()
    {
        var errors = new List<string>();

        // Check required text fields
        if (string.IsNullOrWhiteSpace(CourierCurrent.Name))
            errors.Add("Courier name is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Phone))
            errors.Add("Courier phone is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Email))
            errors.Add("Courier email is required.");

        if (string.IsNullOrWhiteSpace(CourierCurrent.Password))
            errors.Add("Courier password is required.");

        // Validate numeric fields
        if (!CourierCurrent.MaxDistance.HasValue || CourierCurrent.MaxDistance <= 0)
            errors.Add("Courier max distance must be a positive number.");

        // Validate date fields against business rules
        if (CourierCurrent.StartDate > s_bl.Admin.GetClock().AddDays(1))
            errors.Add("Courier start date cannot be in the future.");

        // Show validation errors if any exist
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

    /// <summary>
    /// Handles Cancel button click - closes window without saving changes
    /// </summary>
    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Handles Save/Add button click - validates and saves courier data
    /// Performs either create or update operation based on window mode
    /// </summary>
    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate all fields before attempting to save
            if (!ValidateFields())
                return;

            if (_isCreateMode)
            {
                // Create new courier
                s_bl.Courier.addCourier(bossId, CourierCurrent);
                MessageBox.Show("Courier created successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Update existing courier
                s_bl.Courier.UpdateCourier(bossId, CourierCurrent);
                MessageBox.Show("Courier updated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Close(); // Close window after successful save
        }
        catch (Exception ex)
        {
            // Show error message if save operation fails
            MessageBox.Show($"Save failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles Remove button click - deletes courier after confirmation
    /// Only available for couriers with no delivery history
    /// </summary>
    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        // Prevent removal in create mode or if courier ID is null
        if (_isCreateMode || _courierId == null)
            return;

        try
        {
            // Confirm removal with user - this action is irreversible
            var result = MessageBox.Show(
                $"Are you sure you want to remove courier '{CourierCurrent.Name}' (ID: {CourierCurrent.Id})?\n\nThis action cannot be undone.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Perform removal operation
                int bossId = s_bl.Admin.GetConfig().BossId;
                int courierId = CourierCurrent.Id;
                string courierName = CourierCurrent.Name;
                s_bl.Courier.removeCourier(bossId, courierId);
                Close();

                // Show success message
                MessageBox.Show(
                    $"Courier '{courierName}' has been successfully removed.",
                    "Removal Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }
        catch (BO.BLInvalidOperationException ex)
        {
            // Handle business rule violations (courier has deliveries, etc.)
            MessageBox.Show(
                $"Cannot remove courier '{CourierCurrent.Name}':\n{ex.Message}",
                "Removal Not Allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            // Handle unexpected errors during removal
            MessageBox.Show(
                $"Error removing courier '{CourierCurrent.Name}':\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion
}
