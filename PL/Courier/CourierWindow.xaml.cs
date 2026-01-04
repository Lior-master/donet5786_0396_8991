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
            Transport = DeliveryTransport.All,
            MaxDistance = null,
            StartDate = s_bl.Admin.GetClock(),
            Administrator = BO.Administrator.Courier
        };
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

            CourierCurrent = await Task.Run(() =>
                s_bl.Courier.GetCourierDetails(bossId, _courierId.Value));
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

        if (CourierCurrent.Transport == DeliveryTransport.All)
            errors.Add("Courier transport type cannot be 'All'.");

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
