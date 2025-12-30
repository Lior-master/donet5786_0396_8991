using System;
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

    private BO.Courier _courierCurrent;
    public BO.Courier CourierCurrent
    {
        get => _courierCurrent;
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

        // Initialize a new courier with editable fields
        CourierCurrent = new BO.Courier
        {
            Name = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Password = string.Empty,
            IsActive = true,
            Transport = DeliveryTransport.Bike,
            MaxDistance = null
        };
    }

    /// <summary>
    /// Update mode constructor (existing courier).
    /// </summary>
    public CourierWindow(int courierId)
    {
        InitializeComponent();
        _isCreateMode = false;

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
            // Minimal validation
            if (string.IsNullOrWhiteSpace(CourierCurrent.Name))
                throw new InvalidOperationException("Courier name is required.");

            if (_isCreateMode)
            {
                s_bl.Courier.addCourier(bossId, CourierCurrent);
            }
            else
            {
                s_bl.Courier.UpdateCourier(bossId, CourierCurrent);
            }

            MessageBox.Show("Courier saved successfully.", "Courier",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save failed",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
