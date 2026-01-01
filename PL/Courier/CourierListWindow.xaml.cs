using BO;
using PL.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PL.Courier;

/// <summary>
/// Interaction logic for CourierListWindow.xaml
/// </summary>
public partial class CourierListWindow : Window
{
    static readonly BlApi.IBl s_bl = BlApi.Factory.Get();
    
    public CourierListWindow()
    {
        InitializeComponent();
    }

    public IEnumerable<BO.CourierInList> CourierList
    {
        get { return (IEnumerable<BO.CourierInList>)GetValue(CourierListProperty); }
        set { SetValue(CourierListProperty, value); }
    }
    
    public static readonly DependencyProperty CourierListProperty =
        DependencyProperty.Register("CourierList", typeof(IEnumerable<BO.CourierInList>), typeof(CourierListWindow), new PropertyMetadata(null));

    public BO.DeliveryTransport CourierDelivery { get; set; } = BO.DeliveryTransport.All;

    private void queryCourierList()
    {
        try
        {
            int bossId = s_bl.Admin.GetConfig().BossId;
            CourierList = (CourierDelivery == BO.DeliveryTransport.All) ?
                s_bl?.Courier.GetCouriersList(bossId, null, null)! : 
                s_bl?.Courier.GetCouriersList(bossId, null, CourierDelivery)!;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading couriers: {ex.Message}",
               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CourierList = new List<BO.CourierInList>();
        }
    }

    private void courierListObserver()
        => queryCourierList();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        queryCourierList();
        try
        {
            s_bl.Courier.AddObserver(courierListObserver);
        }
        catch (Exception ex)
        {
            // Some BL implementations might not support observers
            MessageBox.Show($"Observer registration failed: {ex.Message}", 
                           "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        try
        {
            s_bl.Courier.RemoveObserver(courierListObserver);
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private void TransportFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is BO.DeliveryTransport selectedTransport)
        {
            CourierDelivery = selectedTransport;
            queryCourierList(); // refresh the list based on the new filter
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
}
