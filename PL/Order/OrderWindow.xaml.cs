using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using BlApi;
using BO;
using Helpers; // ObserverMutex

namespace PL.Order;

public partial class OrderWindow : Window, INotifyPropertyChanged
{
    private static readonly IBl s_bl = Factory.Get();

    private readonly int bossId = s_bl.Admin.GetConfig().BossId;
    private readonly bool _isCreateMode;

    private readonly int? _orderId;
    private readonly Action? _orderObserver;

    // Stage 7: prevents concurrent re-entrant refreshes from background threads
    private readonly ObserverMutex _orderItemMutex = new(); // stage 7

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

        Loaded += OrderWindow_Loaded;
        Closed += OrderWindow_Closed;
    }

    private async void OrderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isCreateMode || _orderId is null || _orderObserver is null)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            // Register BEFORE fetching so we don't miss updates
            s_bl.Order.AddObserver(_orderId.Value, _orderObserver);

            // Initial load
            OrderCurrent = await s_bl.Order.GetOrderDetailsAsync(bossId, _orderId.Value);
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
    }

    private void OrderWindow_Closed(object? sender, EventArgs e)
    {
        if (_isCreateMode || _orderId is null || _orderObserver is null)
            return;

        try
        {
            s_bl.Order.RemoveObserver(_orderId.Value, _orderObserver);
        }
        catch
        {
            // ignore shutdown issues
        }
    }

    /// <summary>
    /// Stage 7-safe observer callback: can be invoked from background threads.
    /// Uses Dispatcher + ObserverMutex to prevent overlapping refreshes.
    /// </summary>
    private void RefreshOrderFromBl()
    {
        // Stage 7: prevent re-entrancy (multiple observer notifications)
        if (_orderItemMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                if (_isCreateMode || _orderId is null)
                    return;

                try
                {
                    // Single BL call:
                    // If order was canceled/deleted -> BLNotFoundException -> close window gracefully
                    OrderCurrent = await s_bl.Order.GetOrderDetailsAsync(bossId, _orderId.Value);
                }
                catch (BO.BLNotFoundException)
                {
                    Close();
                }
            }
            catch
            {
                // keep observer resilient (avoid crashing UI due to background updates)
            }
            finally
            {
                if (await _orderItemMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    RefreshOrderFromBl();
            }
        }));
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ValidateFields())
                return;

            if (_isCreateMode)
            {
                await s_bl.Order.AddOrderAsync(bossId, OrderCurrent);
                MessageBox.Show("Order created successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await s_bl.Order.UpdateOrderDetailsAsync(bossId, OrderCurrent);
                MessageBox.Show("Order updated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Close();
        }
        catch (BO.BLBadAddressException ex)
        {
            MessageBox.Show(ex.Message, "Address Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"Error cancelling order: {ex.Message}", "Cancellation Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
