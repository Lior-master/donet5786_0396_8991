using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using BlApi;
using BO;

namespace PL.Order;

public partial class OrderWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private readonly int bossId = s_bl.Admin.GetConfig().BossId;
    private readonly bool _isCreateMode;

    private readonly int? _orderId;
    private readonly Action? _orderObserver;

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

        _orderId = orderId;
        _orderObserver = RefreshOrderFromBl;

        Loaded += async (_, __) =>
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                s_bl.Order.AddObserver(orderId, _orderObserver);
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

        Closed += (_, __) =>
        {
            if (_orderObserver is not null)
                s_bl.Order.RemoveObserver(orderId, _orderObserver);
        };
    }

    private void RefreshOrderFromBl()
    {
        Dispatcher.Invoke(async () =>
        {
            if (_isCreateMode || _orderId is null)
                return;
            // Add defensive check to prevent race condition
            var orderExists = await Task.Run(() =>
            {
                try
                {
                    s_bl.Order.GetOrderDetails(bossId, _orderId.Value);
                    return true;
                }
                catch (BO.BLNotFoundException)
                {
                    return false;
                }
            });

            if (!orderExists)
            {
                // Order was canceled - close this window gracefully
                Close();
                return;
            }

            OrderCurrent = await Task.Run(() =>
                s_bl.Order.GetOrderDetails(bossId, _orderId.Value));
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

    private void btnCancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (OrderCurrent is null)
            return;

        try
        {
            var orderId = OrderCurrent.Id;
            var result = MessageBox.Show(
                $"Are you sure you want to cancel order #{orderId}?\nCustomer: {OrderCurrent.CustomerName}\nAddress: {OrderCurrent.CustomerAddress}",
                "Confirm Order Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            s_bl.Order.CancelOrder(bossId, orderId);

            MessageBox.Show(
                $"Order #{orderId} has been successfully cancelled.",
                "Order Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cancelling order: {ex.Message}", "Cancellation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ValidateFields()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerName))
            errors.Add("Customer name is required.");

        if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerPhone))
            errors.Add("Customer phone is required.");

        if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerAddress))
            errors.Add("Customer address is required.");

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
