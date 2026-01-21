using BlApi;
using BO;
using PL.Courier;
using Helpers;
using PL.Order;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Implements the presentation layer UI and related view models.
/// </summary>
namespace PL;

/// <summary>
/// Represents the main window component in this layer.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ================================
    //  ACCESS TO BL LAYER (REQUIRED)
    // ================================
    static readonly IBl s_bl = Factory.Get();

    // ================================
    //   SINGLETON WINDOW INSTANCES
    // ================================
    /// <summary>
    /// Stores the order list window instance value.
    /// </summary>
    private static OrderListWindow? _orderListWindowInstance;
    /// <summary>
    /// Stores the courier list window instance value.
    /// </summary>
    private static CourierListWindow? _courierListWindowInstance;

    // ================================
    //   STAGE 7: OBSERVER MUTEXES
    // ================================
    private readonly ObserverMutex _clockMutex = new();        // stage 7
    private readonly ObserverMutex _configMutex = new();       // stage 7
    private readonly ObserverMutex _orderSummaryMutex = new(); // stage 7

    // ================================
    //   DEPENDENCY PROPERTY: CLOCK
    // ================================
    /// <summary>
    /// Gets or sets the current time value.
    /// </summary>
    public DateTime CurrentTime
    {
        get => (DateTime)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    /// <summary>
    /// Stores the current time property value.
    /// </summary>
    public static readonly DependencyProperty CurrentTimeProperty =
        DependencyProperty.Register(nameof(CurrentTime), typeof(DateTime), typeof(MainWindow));

    // ================================
    //   DEPENDENCY PROPERTY: CONFIG
    // ================================
    /// <summary>
    /// Gets or sets the configuration value.
    /// </summary>
    public Config Configuration
    {
        get => (Config)GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    /// <summary>
    /// Stores the configuration property value.
    /// </summary>
    public static readonly DependencyProperty ConfigurationProperty =
        DependencyProperty.Register(nameof(Configuration), typeof(Config), typeof(MainWindow));

    // ================================
    //   DEPENDENCY PROPERTY: SIMULATOR INTERVAL
    // ================================
    /// <summary>
    /// Gets or sets the interval value.
    /// </summary>
    public double Interval
    {
        get => (double)GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <summary>
    /// Stores the interval property value.
    /// </summary>
    public static readonly DependencyProperty IntervalProperty =
        DependencyProperty.Register(nameof(Interval), typeof(double), typeof(MainWindow),
            new PropertyMetadata(1.0));

    // ================================
    //   ORDER SUMMARY DATA BINDING
    // ================================
    /// <summary>
    /// Stores the order summary data value.
    /// </summary>
    private int[] _orderSummaryData = new int[15]; // 3 ScheduleStatus × 5 OrderStatus
    /// <summary>
    /// Gets or sets the order summary data value.
    /// </summary>
    public int[] OrderSummaryData
    {
        get => _orderSummaryData;
        set
        {
            _orderSummaryData = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ================================
    //           CONSTRUCTOR
    // ================================
    /// <summary>
    /// Initializes a new instance of the MainWindow class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    // ================================
    //      OBSERVER: CLOCK
    // ================================
    /// <summary>
    /// Responds to clock observer notifications from the business layer.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Dispatcher"/> to marshal updates to the UI thread (STA), because observers
    /// may execute on background threads. The <see cref="ObserverMutex"/> prevents overlapping
    /// refreshes and triggers a restart if another notification arrives during an active refresh
    /// (stage 7 observer pattern).
    /// </remarks>
    private void ClockObserver()
    {
        if (_clockMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                CurrentTime = s_bl.Admin.GetClock();
                await RefreshOrderSummaryAsync();
            }
            catch
            {
                // UI must stay responsive; ignore observer exceptions here
            }
            finally
            {
                if (await _clockMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    ClockObserver();
            }
        }));
    }

    // ================================
    //      OBSERVER: CONFIG
    // ================================
    /// <summary>
    /// Responds to configuration observer notifications from the business layer.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Dispatcher"/> to update dependency properties on the UI thread, and
    /// <see cref="ObserverMutex"/> to skip overlapping runs while still replaying the latest
    /// notification after completion (stage 7 observer pattern).
    /// </remarks>
    private void ConfigObserver()
    {
        if (_configMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                Configuration = s_bl.Admin.GetConfig();
            }
            catch
            {
                // UI must stay responsive; ignore observer exceptions here
            }
            finally
            {
                if (await _configMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    ConfigObserver();
            }
        }));
    }

    // ================================
    //   OBSERVER: ORDERS SUMMARY
    // ================================
    /// <summary>
    /// Responds to order summary observer notifications from the business layer.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Dispatcher"/> to keep UI updates on the STA thread and
    /// <see cref="ObserverMutex"/> to prevent concurrent refreshes. If a notification arrives
    /// mid-refresh, the mutex requests a restart to ensure the latest summary is displayed.
    /// </remarks>
    private void OrderSummaryObserver()
    {
        if (_orderSummaryMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await RefreshOrderSummaryAsync();
            }
            catch
            {
                // UI must stay responsive; ignore observer exceptions here
            }
            finally
            {
                if (await _orderSummaryMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    OrderSummaryObserver();
            }
        }));
    }

    // ================================
    //   ORDER SUMMARY REFRESH
    // ================================
    private async Task RefreshOrderSummaryAsync()
    {
        const int statusCount = 5;
        const int scheduleCount = 3;
        const int total = statusCount * scheduleCount; // 15

        try
        {
            // Prefer already-loaded config when available
            int bossId;
            try
            {
                bossId = Configuration.BossId;
            }
            catch
            {
                bossId = s_bl.Admin.GetConfig().BossId;
            }

            // BL returns status-major layout:
            // idx = (OrderStatus * scheduleCount) + ScheduleStatus
            var raw = (await s_bl.Order.GetOrdersBySummaryAsync(bossId)).ToArray();

            if (raw.Length < total)
            {
                OrderSummaryData = new int[total];
                return;
            }

            // UI wants schedule-major by rows:
            // row0 = OnTime, row1 = Late, row2 = InRisk
            // schedule enum mapping in your BL: OnTime=0, InRisk=1, Late=2
            int[] scheduleMap = { 0, 2, 1 };

            var ui = new int[total];

            for (int row = 0; row < scheduleCount; row++)
            {
                int scheduleEnumIdx = scheduleMap[row];

                for (int col = 0; col < statusCount; col++)
                {
                    int blIdx = col * scheduleCount + scheduleEnumIdx; // status-major
                    int uiIdx = row * statusCount + col;               // row-major (schedule rows)

                    ui[uiIdx] = raw[blIdx];
                }
            }

            OrderSummaryData = ui;
        }
        catch (Exception ex)
        {
            OrderSummaryData = new int[total];
            System.Diagnostics.Debug.WriteLine($"Error refreshing order summary: {ex.Message}");
        }
    }

    // ================================
    //   INITIALIZATION ON OPENING
    // ================================
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load current data
        CurrentTime = s_bl.Admin.GetClock();
        Configuration = s_bl.Admin.GetConfig();

        // Register observers (stage 7: must be done before simulator starts)
        s_bl.Admin.AddClockObserver(ClockObserver);
        s_bl.Admin.AddConfigObserver(ConfigObserver);
        s_bl.Order.AddObserver(OrderSummaryObserver);

        // Load order summary
        await RefreshOrderSummaryAsync();
    }

    // ================================
    //      CLEANUP ON CLOSING
    // ================================
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            if (_simulatorRunning)
            {
                s_bl.Admin.StopSimulator();
                _simulatorRunning = false;
            }

            s_bl.Admin.RemoveClockObserver(ClockObserver);
            s_bl.Admin.RemoveConfigObserver(ConfigObserver);
            s_bl.Order.RemoveObserver(OrderSummaryObserver);

            var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
            if (loginWindow != null)
            {
                loginWindow._directorLoggedIn = false;
            }
        }
        catch
        {
            // Some BL implementations may throw during shutdown; ignore.
        }
    }

    // ================================
    //   ORDER SUMMARY CELL CLICK
    // ================================
    private void SummaryCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tagString)
            return;

        try
        {
            var parts = tagString.Split(',');
            if (parts.Length != 2) return;

            if (!Enum.TryParse(parts[0].Trim(), out BO.ScheduleStatus scheduleStatus))
                return;

            if (!Enum.TryParse(parts[1].Trim(), out BO.OrderStatus orderStatus))
                return;

            if (_orderListWindowInstance != null && _orderListWindowInstance.IsLoaded)
            {
                if (_orderListWindowInstance.WindowState == WindowState.Minimized)
                    _orderListWindowInstance.WindowState = WindowState.Normal;

                _orderListWindowInstance.Activate();
                _orderListWindowInstance.Focus();
            }
            else
            {
                _orderListWindowInstance = new OrderListWindow();
                _orderListWindowInstance.Closed += (s, args) => _orderListWindowInstance = null;
            }

            _orderListWindowInstance.FilterTypeOrder = PL.FilterTypeOrder.ByOrderAndSchedulStatus;
            _orderListWindowInstance.ScheduleStatus = scheduleStatus;
            _orderListWindowInstance.OrderStatus = orderStatus;

            if (!_orderListWindowInstance.IsLoaded)
                _orderListWindowInstance.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening order list: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =======================================================
    //          BUTTONS: CLOCK (ADVANCE TIME)
    // =======================================================
    private void AddOneSecond_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Second);
    private void AddOneMinute_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Minute);
    private void AddOneHour_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Hour);
    private void AddOneDay_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Day);
    private void AddOneMonth_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Month);
    private void AddOneYear_Click(object sender, RoutedEventArgs e) => s_bl.Admin.ForwardClock(TimeUnit.Year);

    // =======================================================
    //           CONFIGURATION: LOAD / APPLY
    // =======================================================
    private void LoadAllConfig_Click(object sender, RoutedEventArgs e) =>
        Configuration = s_bl.Admin.GetConfig();

    private async void ApplyConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await s_bl.Admin.SetConfigAsync(Configuration);
            MessageBox.Show("Configuration updated successfully.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (BO.BLBadAddressException ex)
        {
            MessageBox.Show(ex.Message,
                "Address Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating configuration:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =======================================================
    //              BUTTONS: DATABASE
    // =======================================================
    private async void InitDB_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Initialize database?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            await s_bl.Admin.InitializeDBAsync();
            MessageBox.Show("Database initialized.");
            await RefreshOrderSummaryAsync();
        }
    }

    private async void ResetDB_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Reset database?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            await s_bl.Admin.ResetDBAsync();
            MessageBox.Show("Database reset.");
            await RefreshOrderSummaryAsync();
        }
    }

    // =======================================================
    //     BUTTONS: OPENING LIST SCREENS (SINGLETON PATTERN)
    // =======================================================
    private void CouriersList_Click(object sender, RoutedEventArgs e)
    {
        if (_courierListWindowInstance != null && _courierListWindowInstance.IsLoaded)
        {
            if (_courierListWindowInstance.WindowState == WindowState.Minimized)
                _courierListWindowInstance.WindowState = WindowState.Normal;

            _courierListWindowInstance.Activate();
            _courierListWindowInstance.Focus();
            return;
        }

        _courierListWindowInstance = new CourierListWindow();
        _courierListWindowInstance.Closed += (s, args) => _courierListWindowInstance = null;
        _courierListWindowInstance.Show();
    }

    private void OrdersList_Click(object sender, RoutedEventArgs e)
    {
        if (_orderListWindowInstance != null && _orderListWindowInstance.IsLoaded)
        {
            if (_orderListWindowInstance.WindowState == WindowState.Minimized)
                _orderListWindowInstance.WindowState = WindowState.Normal;

            _orderListWindowInstance.Activate();
            _orderListWindowInstance.Focus();
            return;
        }

        _orderListWindowInstance = new OrderListWindow();
        _orderListWindowInstance.Closed += (s, args) => _orderListWindowInstance = null;
        _orderListWindowInstance.Show();
    }

    // =======================================================
    //          SIMULATOR CONTROL
    // =======================================================
    /// <summary>
    /// Stores the simulator running value.
    /// </summary>
    private bool _simulatorRunning = false;

    private void SimulatorToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_simulatorRunning)
            {
                s_bl.Admin.StopSimulator();
                _simulatorRunning = false;

                SimulatorToggleButton.Content = "▶ Start Simulator";
                SimulatorToggleButton.Background = new SolidColorBrush(Color.FromArgb(255, 0, 200, 83));
                SimulatorIntervalInput.IsReadOnly = false;

                EnableTimeControlButtons(true);
                EnableDatabaseButtons(true);
            }
            else
            {
                if (Interval <= 0)
                {
                    MessageBox.Show("Please enter a valid positive number for clock speed.",
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                s_bl.Admin.StartSimulator(Interval);
                _simulatorRunning = true;

                SimulatorToggleButton.Content = "⏹ Stop Simulator";
                SimulatorToggleButton.Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54));
                SimulatorIntervalInput.IsReadOnly = true;

                EnableTimeControlButtons(false);
                EnableDatabaseButtons(false);
            }
        }
        catch (Exception ex)
        {
            _simulatorRunning = false;
            MessageBox.Show($"Error controlling simulator: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnableTimeControlButtons(bool enable)
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (button.Content?.ToString()?.StartsWith("+") ?? false)
                button.IsEnabled = enable;
        }
    }

    private void EnableDatabaseButtons(bool enable)
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var content = button.Content?.ToString() ?? "";
            if (content.Contains("Initialize") || content.Contains("Reset"))
                button.IsEnabled = enable;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
                yield return t;

            foreach (T childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }
}
