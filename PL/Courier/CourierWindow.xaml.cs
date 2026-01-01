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
/// Interaction logic for CourierWindow.xaml
/// This window is used as a "Single Item Window" (Stage 5 requirement),
/// in Create or Update mode depending on the constructor used.
/// </summary>
public partial class CourierWindow : Window, INotifyPropertyChanged
{
    // Access to the BL layer
    private static readonly IBl s_bl = Factory.Get();

    private int bossId = s_bl.Admin.GetConfig().BossId;

    private readonly bool _isCreateMode;

    /// <summary>
    /// Property to determine if the ID field should be read-only (true in update mode, false in create mode)
    /// </summary>
    private bool _isIdReadOnly;
    public bool isReadOnly 
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
        isReadOnly = false; // Editable in create mode

        // Initialize a new courier with editable fields - FIX INITIALIZATION
        CourierCurrent = new BO.Courier
        {
            Id = 0, // Will be assigned by BL layer
            Name = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Password = string.Empty,
            IsActive = true,
            Transport = DeliveryTransport.Bike,
            MaxDistance = null,
            StartDate = DateTime.Now, // Set current date
            Administrator = BO.Administrator.Courier // Set default role
        };
    }

    /// <summary>
    /// Update mode constructor (existing courier).
    /// </summary>
    public CourierWindow(int courierId)
    {
        InitializeComponent();
        _isCreateMode = false;
        isReadOnly = true; // Read-only in update mode

        // Load data asynchronously to avoid freezing the UI thread
        Loaded += async (_, __) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                CourierCurrent = await Task.Run(() => s_bl.Courier.GetCourierDetails(bossId, courierId));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading courier", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        };
    }

    /// <summary>
    /// Close the window without saving.
    /// </summary>
    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Save changes (Create or Update depending on mode).
    /// </summary>
    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate all fields
            if (!ValidateFields())
            {
                return; // Don't save if validation fails
            }

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

    /// <summary>
    /// Validates all courier fields and shows error messages.
    /// </summary>
    /// <returns>True if all validation passes, false otherwise.</returns>
    private bool ValidateFields()
    {
        var errors = new List<string>();

        // Validate name
        if (string.IsNullOrWhiteSpace(CourierCurrent.Name))
        {
            errors.Add("Courier name is required.");
        }

        // Validate phone
        if (string.IsNullOrWhiteSpace(CourierCurrent.Phone))
        {
            errors.Add("Courier phone is required.");
        }

        // Validate email
        if (string.IsNullOrWhiteSpace(CourierCurrent.Email))
        {
            errors.Add("Courier email is required.");
        }

        // Validate password
        if (string.IsNullOrWhiteSpace(CourierCurrent.Password))
        {
            errors.Add("Courier password is required.");
        }

        // Validate max distance - FIX NULL CHECK
        if (!CourierCurrent.MaxDistance.HasValue || CourierCurrent.MaxDistance <= 0)
        {
            errors.Add("Courier max distance must be a positive number.");
        }

        // Validate start date - MAKE MORE LENIENT
        var currentTime = s_bl.Admin.GetClock();
        if (CourierCurrent.StartDate > currentTime.AddDays(1))
        {
            errors.Add("Courier start date cannot be in the future.");
        }

        // Validate transport
        if (CourierCurrent.Transport == DeliveryTransport.All)
        {
            errors.Add("Courier transport type cannot be 'All'.");
        }

        // Show validation errors if any
        if (errors.Count > 0)
        {
            string errorMessage = "Please fix the following issues:\n\n" + string.Join("\n", errors);
            MessageBox.Show(errorMessage, "Validation Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }
}
