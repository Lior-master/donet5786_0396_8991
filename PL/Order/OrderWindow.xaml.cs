using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using BlApi;
using BO;

namespace PL.Order;

public partial class OrderWindow : Window, INotifyPropertyChanged
{
    // BL access (comme dans le reste du projet)
    private static readonly IBl s_bl = Factory.Get();

    private readonly int bossId = s_bl.Admin.GetConfig().BossId;

    private readonly bool _isCreateMode;

    private BO.Order? _orderCurrent;
    public BO.Order OrderCurrent
    {
        get => _orderCurrent!;
        set
        {
            _orderCurrent = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Create mode constructor (New Order).
    /// </summary>
    public OrderWindow()
    {
        InitializeComponent();
        _isCreateMode = true;

        // New order skeleton.
        OrderCurrent = new BO.Order
        {
            Type = default,
            CustomerName = string.Empty,
            CustomerPhone = string.Empty,
            CustomerAddress = string.Empty,
            OrderDate = s_bl.Admin.GetClock(),
            Weight = null,
            Volume = null,
            Fragility = null,
            OrderDescription = null
        };
    }

    /// <summary>
    /// Update mode constructor (Existing Order by Id).
    /// </summary>
    public OrderWindow(int orderId)
    {
        InitializeComponent();
        _isCreateMode = false;

        Loaded += async (_, __) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                OrderCurrent = await Task.Run(() => s_bl.Order.GetOrderDetails(bossId, orderId));
               
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot load order", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        };
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

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
                s_bl.Order.AddOrder(bossId, OrderCurrent);
                MessageBox.Show("Order created successfully.", "Success",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                s_bl.Order.UpdateOrderDetails(bossId, OrderCurrent);
                MessageBox.Show("Order updated successfully.", "Success",
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

        if(string.IsNullOrWhiteSpace(OrderCurrent.CustomerName))
            errors.Add("Customer name is required.");
        if(string.IsNullOrWhiteSpace(OrderCurrent.CustomerPhone))
            errors.Add("Customer phone is required.");
        if(string.IsNullOrWhiteSpace(OrderCurrent.CustomerAddress))
            errors.Add("Customer address is required.");

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
