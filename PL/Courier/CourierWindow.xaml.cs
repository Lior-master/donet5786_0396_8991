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

public partial class CourierWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private int bossId = s_bl.Admin.GetConfig().BossId;
    private readonly bool _isCreateMode;

    private readonly int? _courierId;
    private readonly Action? _courierObserver;

    private bool _isIdReadOnly;
    public bool IsReadOnly
    {
        get => _isIdReadOnly;
        private set
        {
            _isIdReadOnly = value;
            OnPropertyChanged();
        }
    }

    private BO.Courier? _courierCurrent;
    public BO.Courier CourierCurrent
    {
        get => _courierCurrent!;
        set
        {
            _courierCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRemoveCourier));
        }
    }

    /// <summary>
    /// Gets the text to display on the save/add button based on the mode.
    /// </summary>
    public string SaveButtonText => _isCreateMode ? "➕ Add" : "💾 Save";

    /// <summary>
    /// Determines if the current courier can be removed.
    /// A courier can only be removed if they have no delivery history and no current order.
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Create mode constructor (new courier).
    /// </summary>
    public CourierWindow()
    {
        InitializeComponent();
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

        // Notify the UI that SaveButtonText should be refreshed
        OnPropertyChanged(nameof(SaveButtonText));
    }

    /// <summary>
    /// Update mode constructor (existing courier).
    /// </summary>
    public CourierWindow(int courierId)
    {
        InitializeComponent();
        _isCreateMode = false;
        IsReadOnly = true;

        _courierId = courierId;
        _courierObserver = RefreshCourierFromBl;

        // Notify the UI that SaveButtonText should be refreshed
        OnPropertyChanged(nameof(SaveButtonText));

        Loaded += async (_, __) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                s_bl.Courier.AddObserver(courierId, _courierObserver);
                CourierCurrent = await Task.Run(() =>
                    s_bl.Courier.GetCourierDetails(bossId, courierId));
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
        };

        Closed += (_, __) =>
        {
            if (_courierObserver is not null)
                s_bl.Courier.RemoveObserver(courierId, _courierObserver);
        };
    }

    private void RefreshCourierFromBl()
    {
        Dispatcher.Invoke(async () =>
        {
            if (_isCreateMode || _courierId is null)
                return;

            try 
            {
                // Add defensive check to prevent race condition
                var courierExists = await Task.Run(() =>
                {
                    try
                    {
                        s_bl.Courier.GetCourierDetails(bossId, _courierId.Value);
                        return true;
                    }
                    catch (BO.BLNotFoundException)
                    {
                        return false;
                    }
                });

                if (!courierExists)
                {
                    // Courier was deleted - close this window
                    Close();
                    return;
                }

                CourierCurrent = await Task.Run(() =>
                    s_bl.Courier.GetCourierDetails(bossId, _courierId.Value));
            }
            catch (BO.BLNotFoundException)
            {
                // Courier was deleted - close this window
                MessageBox.Show("This courier has been deleted.", "Courier Removed", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
        });
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ValidateFields())
                return;

            if (_isCreateMode)
            {
                s_bl.Courier.addCourier(bossId, CourierCurrent);
                MessageBox.Show("Courier created successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                s_bl.Courier.UpdateCourier(bossId, CourierCurrent);
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
            // Confirm removal with user
            var result = MessageBox.Show(
                $"Are you sure you want to remove courier '{CourierCurrent.Name}' (ID: {CourierCurrent.Id})?\n\nThis action cannot be undone.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int bossId = s_bl.Admin.GetConfig().BossId;
                int courierId = CourierCurrent.Id;
                string courierName = CourierCurrent.Name;
                s_bl.Courier.removeCourier(bossId, courierId);
                Close();

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
}
