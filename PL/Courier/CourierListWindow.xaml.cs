using BO;
using Helpers;
using PL.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PL.Courier;

/// <summary>
/// Interaction logic for CourierListWindow.xaml
/// </summary>
public partial class CourierListWindow : Window
{
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();

    // Stage 7: Mutex for courier list observer
    private readonly ObserverMutex _courierListMutex = new(); // stage 7

    private bool _observerRegistered = false;

    public CourierListWindow()
    {
        InitializeComponent();
    }

    public IEnumerable<BO.CourierInList> CourierList
    {
        get => (IEnumerable<BO.CourierInList>)GetValue(CourierListProperty);
        set => SetValue(CourierListProperty, value);
    }

    public static readonly DependencyProperty CourierListProperty =
        DependencyProperty.Register(
            nameof(CourierList),
            typeof(IEnumerable<BO.CourierInList>),
            typeof(CourierListWindow),
            new PropertyMetadata(null));

    public PL.FilterTypeCourier FilterTypeCourier { get; set; } = PL.FilterTypeCourier.All;

    public BO.DeliveryTransport CourierDelivery { get; set; } = DeliveryTransport.All;

    public BO.Administrator AdministratorFilter { get; set; } = Administrator.All;

    public bool IsFilterActiveStatus { get; set; }

    private void queryCourierList()
    {
        try
        {
            int bossId = s_bl.Admin.GetConfig().BossId;

            if (FilterTypeCourier == PL.FilterTypeCourier.All)
            {
                CourierList = s_bl.Courier.GetCouriersList(bossId, null, null)!;
            }
            else if (FilterTypeCourier == PL.FilterTypeCourier.ByActiveStatus)
            {
                CourierList = s_bl.Courier.GetCouriersList(bossId, IsFilterActiveStatus, null)!;
            }
            else if (FilterTypeCourier == PL.FilterTypeCourier.ByTransportType)
            {
                // Only pass filter if it's not "All"
                var transportFilter = CourierDelivery == DeliveryTransport.All ? null : (Enum?)CourierDelivery;
                CourierList = s_bl.Courier.GetCouriersList(bossId, null, transportFilter)!;
            }
            else if (FilterTypeCourier == PL.FilterTypeCourier.ByAdministratorType)
            {
                // Only pass filter if it's not "All"
                var adminFilter = AdministratorFilter == Administrator.All ? null : (Enum?)AdministratorFilter;
                CourierList = s_bl.Courier.GetCouriersList(bossId, null, adminFilter)!;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading couriers: {ex.Message}",
               "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            CourierList = Array.Empty<BO.CourierInList>();
        }
    }

    private void courierListObserver()
    {
        // Stage 7 (for multithreading)
        if (_courierListMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                queryCourierList();
            }
            catch
            {
                // keep observer resilient
            }
            finally
            {
                if (await _courierListMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    courierListObserver();
            }
        }));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // initial load
        queryCourierList();

        // register observer once per window instance
        if (_observerRegistered)
            return;

        try
        {
            s_bl.Courier.AddObserver(courierListObserver);
            _observerRegistered = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Observer registration failed: {ex.Message}",
                           "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (!_observerRegistered)
            return;

        try
        {
            s_bl.Courier.RemoveObserver(courierListObserver);
        }
        catch
        {
            // ignore on shutdown
        }
        finally
        {
            _observerRegistered = false;
        }
    }

    private void TransportFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.DeliveryTransport selectedTransport)
        {
            CourierDelivery = selectedTransport;
            queryCourierList();
        }
    }

    private void ActiveFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.IsChecked == true)
        {
            if (radioButton.Name == "rbActiveOnly")
            {
                FilterTypeCourier = FilterTypeCourier.ByActiveStatus;
                IsFilterActiveStatus = true;
            }
            else if (radioButton.Name == "rbInactiveOnly")
            {
                FilterTypeCourier = FilterTypeCourier.ByActiveStatus;
                IsFilterActiveStatus = false;
            }
            else if (radioButton.Name == "rbAll")
            {
                FilterTypeCourier = PL.FilterTypeCourier.All;
                queryCourierList();
                return;
            }

            queryCourierList();
        }
    }

    private void AdministratorFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.Administrator selectedAdmin)
        {
            AdministratorFilter = selectedAdmin;
            queryCourierList();
        }
    }

    private void FilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (sender is ComboBox comboBox && comboBox.SelectedItem is PL.FilterTypeCourier selectedFilter)
        {
            FilterTypeCourier = selectedFilter;

            lblSpecificFilter.Visibility = Visibility.Collapsed;
            cmbTransportFilter.Visibility = Visibility.Collapsed;
            cmbAdministratorFilter.Visibility = Visibility.Collapsed;
            pnlActiveFilter.Visibility = Visibility.Collapsed;

            switch (selectedFilter)
            {
                case PL.FilterTypeCourier.ByTransportType:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Transport:";
                    cmbTransportFilter.Visibility = Visibility.Visible;

                    CourierDelivery = DeliveryTransport.All;
                    if (cmbTransportFilter != null)
                        cmbTransportFilter.SelectedValue = DeliveryTransport.All;
                    break;

                case PL.FilterTypeCourier.ByAdministratorType:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Administrator:";
                    cmbAdministratorFilter.Visibility = Visibility.Visible;

                    AdministratorFilter = Administrator.All;
                    if (cmbAdministratorFilter != null)
                        cmbAdministratorFilter.SelectedValue = Administrator.All;
                    break;

                case PL.FilterTypeCourier.ByActiveStatus:
                    lblSpecificFilter.Visibility = Visibility.Visible;
                    lblSpecificFilter.Text = "Select Status:";
                    pnlActiveFilter.Visibility = Visibility.Visible;

                    if (rbAll != null)
                        rbAll.IsChecked = true;

                    // show all by default until user picks active/inactive
                    FilterTypeCourier = PL.FilterTypeCourier.All;
                    break;

                case PL.FilterTypeCourier.All:
                default:
                    CourierDelivery = DeliveryTransport.All;
                    AdministratorFilter = Administrator.All;
                    IsFilterActiveStatus = false;

                    if (cmbTransportFilter != null)
                        cmbTransportFilter.SelectedValue = DeliveryTransport.All;
                    if (cmbAdministratorFilter != null)
                        cmbAdministratorFilter.SelectedValue = Administrator.All;

                    if (rbAll != null)
                        rbAll.IsChecked = true;
                    break;
            }

            queryCourierList();
        }
    }

    private void dgCourierList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        if (dataGrid.SelectedItem is not BO.CourierInList selectedCourier)
            return;

        new CourierWindow(selectedCourier.Id).Show();
    }

    private void btnAddCourier_Click(object sender, RoutedEventArgs e)
    {
        new CourierWindow().Show();
    }

    private void btnRemoveCourier_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not BO.CourierInList selectedCourier)
            return;

        try
        {
            var result = MessageBox.Show(
                $"Are you sure you want to remove courier '{selectedCourier.Name}' (ID: {selectedCourier.Id})?\n\nThis action cannot be undone.",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int bossId = s_bl.Admin.GetConfig().BossId;
                s_bl.Courier.removeCourier(bossId, selectedCourier.Id);

                MessageBox.Show(
                    $"Courier '{selectedCourier.Name}' has been successfully removed.",
                    "Removal Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (BO.BLInvalidOperationException ex)
        {
            MessageBox.Show(
                $"Cannot remove courier '{selectedCourier.Name}':\n{ex.Message}",
                "Removal Not Allowed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error removing courier '{selectedCourier.Name}':\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
