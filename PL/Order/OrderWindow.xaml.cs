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
            Weight = null,
            Volume = null,
            Fragility = null,
            OrderDescription = null,
            Distance = 0
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

                var order = await Task.Run(() => s_bl.Order.GetOrderDetails(bossId, orderId));
                OrderCurrent = order;
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
            // Sanity checks min (tu peux en ajouter)
            if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerName))
                throw new InvalidOperationException("Customer name is required.");

            if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerPhone))
                throw new InvalidOperationException("Customer phone is required.");

            if (string.IsNullOrWhiteSpace(OrderCurrent.CustomerAddress))
                throw new InvalidOperationException("Customer address is required.");

            // Persist
            if (_isCreateMode)
            {
                s_bl.Order.AddOrder(bossId,OrderCurrent);
            }
            else
            {
                s_bl.Order.UpdateOrderDetails(bossId, OrderCurrent);
            }

            MessageBox.Show("Saved successfully.", "Order", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
