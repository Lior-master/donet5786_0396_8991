using BlApi;
using BO;
using PL.Courier;
using Helpers; 
using PL.Order;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PL;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ================================
    //  ACCESS TO BL LAYER (REQUIRED)
    // ================================
    static readonly IBl s_bl = Factory.Get();

    // ================================
    //   SINGLETON WINDOW INSTANCES
    // ================================
    private static OrderListWindow? _orderListWindowInstance;
    private static CourierListWindow? _courierListWindowInstance;

    // ================================
    //   STAGE 7: OBSERVER MUTEXES
    // ================================
    private readonly ObserverMutex _clockMutex = new(); // stage 7
    private readonly ObserverMutex _configMutex = new(); // stage 7
    private readonly ObserverMutex _orderSummaryMutex = new(); // stage 7

    // ================================
    //   DEPENDENCY PROPERTY: CLOCK
    // ================================
    public DateTime CurrentTime
    {
        get => (DateTime)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public static readonly DependencyProperty CurrentTimeProperty =
        DependencyProperty.Register("CurrentTime", typeof(DateTime), typeof(MainWindow));

    // ================================
    //   DEPENDENCY PROPERTY: CONFIG
    // ================================
    public Config Configuration
    {
        get => (Config)GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public static readonly DependencyProperty ConfigurationProperty =
        DependencyProperty.Register("Configuration", typeof(Config), typeof(MainWindow));

    // ================================
    //   DEPENDENCY PROPERTY: SIMULATOR INTERVAL
    // ================================
    public double Interval
    {
        get => (double)GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    public static readonly DependencyProperty IntervalProperty =
        DependencyProperty.Register("Interval", typeof(double), typeof(MainWindow), 
            new PropertyMetadata(1.0)); // Default value of 1.0

    // ================================
    //   ORDER SUMMARY DATA BINDING
    // ================================
    private int[] _orderSummaryData = new int[15]; // 3 ScheduleStatus × 5 OrderStatus
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ================================
    //           CONSTRUCTOR
    // ================================
    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    // ================================
    //      OBSERVER: CLOCK
    // ================================
    private void ClockObserver()
    {
        #region Stage 7 (for multithreading)
        if (_clockMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                CurrentTime = s_bl.Admin.GetClock();
                // Refresh order summary when clock updates
                await RefreshOrderSummaryAsync();
            }
            catch { }
            finally
            {
                if (await _clockMutex.UnsetLoadInProgressAndCheckRestartRequested())
                {
                    ClockObserver();
                }
            }
        });
        #endregion Stage 7 (for multithreading)
    }

    // ================================
    //      OBSERVER: CONFIG
    // ================================
    private void ConfigObserver()
    {
        #region Stage 7 (for multithreading)
        if (_configMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                Configuration = s_bl.Admin.GetConfig();
            }
            catch { }
            finally
            {
                if (await _configMutex.UnsetLoadInProgressAndCheckRestartRequested())
                {
                    ConfigObserver();
                }
            }
        });
        #endregion Stage 7 (for multithreading)
    }

    // ================================
    //   OBSERVER: ORDERS SUMMARY
    // ================================
    private void OrderSummaryObserver()
    {
        #region Stage 7 (for multithreading)
        if (_orderSummaryMutex.CheckAndSetLoadInProgressOrRestartRequired())
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await RefreshOrderSummaryAsync();
            }
            catch { }
            finally
            {
                if (await _orderSummaryMutex.UnsetLoadInProgressAndCheckRestartRequested())
                {
                    OrderSummaryObserver();
                }
            }
        });
        #endregion Stage 7 (for multithreading)
    }

    // ================================
    //   ORDER SUMMARY REFRESH
    // ================================
    private async Task RefreshOrderSummaryAsync()
    {
        try
        {
            var bossId = s_bl.Admin.GetConfig().BossId;

            // BL returns status-major layout:
            // idx = OrderStatus * 4 + ScheduleStatus
            var raw = (await s_bl.Order.GetOrdersBySummaryAsync(bossId)).ToArray();

            // We expect 5 statuses (Pending..Returned) and 3 schedules (OnTime, InRisk, Late)
            // UI wants schedule-major by ROWS in this order: OnTime, Late, InRisk, Unknown
            const int statusCount = 5;
            const int scheduleCount = 3;

            var ui = new int[statusCount * scheduleCount]; // 15

            // UI row -> enum schedule index mapping
            // Row0 OnTime   -> 0
            // Row1 Late     -> 2
            // Row2 InRisk   -> 1
            // Row3 Unknown  -> 3
            int[] scheduleMap = { 0, 2, 1 };

            if (raw.Length >= 15)
            {
                for (int row = 0; row < scheduleCount; row++)
                {
                    int scheduleEnumIdx = scheduleMap[row];

                    for (int col = 0; col < statusCount; col++)
                    {
                        int blIdx = col * scheduleCount + scheduleEnumIdx; // status-major
                        int uiIdx = row * statusCount + col;               // schedule-major (row blocks)

                        ui[uiIdx] = raw[blIdx];
                    }
                }

                OrderSummaryData = ui;
            }
            else
            {
                // Fallback: pad with zeros
                OrderSummaryData = new int[15];
            }
        }
        catch (Exception ex)
        {
            OrderSummaryData = new int[15];
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
        
        // Load order summary
        await RefreshOrderSummaryAsync();

        // Register observers
        s_bl.Admin.AddClockObserver(ClockObserver);
        s_bl.Admin.AddConfigObserver(ConfigObserver);
        s_bl.Order.AddObserver(OrderSummaryObserver);
    }

    // ================================
    //      CLEANUP ON CLOSING
    // ================================
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Stop simulator if running
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
            // Some BL versions do not implement RemoveObserver — ignore.
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
            // Parse the Tag string to extract ScheduleStatus and OrderStatus
            var parts = tagString.Split(',');
            if (parts.Length != 2) return;

            // Parse ScheduleStatus from the first part
            if (!Enum.TryParse<BO.ScheduleStatus>(parts[0].Trim(), out var scheduleStatus))
                return;

            // Parse OrderStatus from the second part
            if (!Enum.TryParse<BO.OrderStatus>(parts[1].Trim(), out var orderStatus))
                return;

            // Check if OrderListWindow instance exists and is loaded
            if (_orderListWindowInstance != null && _orderListWindowInstance.IsLoaded)
            {
                // If window is minimized, restore it
                if (_orderListWindowInstance.WindowState == WindowState.Minimized)
                {
                    _orderListWindowInstance.WindowState = WindowState.Normal;
                }
                
                // Bring window to front and activate it
                _orderListWindowInstance.Activate();
                _orderListWindowInstance.Focus();
            }
            else
            {
                // Create new instance
                _orderListWindowInstance = new OrderListWindow();
                
                // Handle window closed event to reset instance
                _orderListWindowInstance.Closed += (s, args) => _orderListWindowInstance = null;
            }

            // Apply filters based on the clicked cell
            // First, set ScheduleStatus filter
            _orderListWindowInstance.FilterTypeOrder = PL.FilterTypeOrder.ByOrderAndSchedulStatus;
            _orderListWindowInstance.ScheduleStatus = scheduleStatus;
            _orderListWindowInstance.OrderStatus = orderStatus;

            // Show the window if it's not already visible
            if (!_orderListWindowInstance.IsLoaded)
            {
                _orderListWindowInstance.Show();
            }
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

    private void AddOneSecond_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Second);
    }

    private void AddOneMinute_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Minute);
    }

    private void AddOneHour_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Hour);
    }

    private void AddOneDay_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Day);
    }

    private void AddOneMonth_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Month);
    }

    private void AddOneYear_Click(object sender, RoutedEventArgs e)
    {
        s_bl.Admin.ForwardClock(TimeUnit.Year);
    }

    // =======================================================
    //           CONFIGURATION: LOAD / APPLY
    // =======================================================

    private void LoadAllConfig_Click(object sender, RoutedEventArgs e)
    {
        Configuration = s_bl.Admin.GetConfig();
    }

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
            // Refresh order summary after DB initialization
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
            // Refresh order summary after DB reset
            await RefreshOrderSummaryAsync();
        }
    }

    // =======================================================
    //     BUTTONS: OPENING LIST SCREENS (SINGLETON PATTERN)
    // =======================================================

    private void CouriersList_Click(object sender, RoutedEventArgs e)
    {
        // Check if window instance exists and is still loaded
        if (_courierListWindowInstance != null && _courierListWindowInstance.IsLoaded)
        {
            // If window is minimized, restore it
            if (_courierListWindowInstance.WindowState == WindowState.Minimized)
            {
                _courierListWindowInstance.WindowState = WindowState.Normal;
            }
            
            // Bring window to front and activate it
            _courierListWindowInstance.Activate();
            _courierListWindowInstance.Focus();
            return;
        }

        // Create new instance only if none exists or previous one was closed
        _courierListWindowInstance = new CourierListWindow();
        
        // Handle window closed event to reset instance
        _courierListWindowInstance.Closed += (s, args) => _courierListWindowInstance = null;
        
        _courierListWindowInstance.Show();
    }

    private void OrdersList_Click(object sender, RoutedEventArgs e)
    {
        // Check if window instance exists and is still loaded
        if (_orderListWindowInstance != null && _orderListWindowInstance.IsLoaded)
        {
            // If window is minimized, restore it
            if (_orderListWindowInstance.WindowState == WindowState.Minimized)
            {
                _orderListWindowInstance.WindowState = WindowState.Normal;
            }
            
            // Bring window to front and activate it
            _orderListWindowInstance.Activate();
            _orderListWindowInstance.Focus();
            return;
        }

        // Create new instance only if none exists or previous one was closed
        _orderListWindowInstance = new OrderListWindow();
        
        // Handle window closed event to reset instance
        _orderListWindowInstance.Closed += (s, args) => _orderListWindowInstance = null;
        
        _orderListWindowInstance.Show();
    }


    // =======================================================
    //          SIMULATOR CONTROL
    // =======================================================

    private bool _simulatorRunning = false;
   
    private void SimulatorToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_simulatorRunning)
            {
                // Stop simulator
                s_bl.Admin.StopSimulator();
                _simulatorRunning = false;
                
                // Update UI
                SimulatorToggleButton.Content = "▶ Start Simulator";
                SimulatorToggleButton.Background = new SolidColorBrush(Color.FromArgb(255, 0, 200, 83)); // Green
                SimulatorIntervalInput.IsReadOnly = false;
                
                // Re-enable time control buttons
                EnableTimeControlButtons(true);
                EnableDatabaseButtons(true);
            }
            else
            {
                // Start simulator using the bound Interval property
                if (Interval <= 0)
                {
                    MessageBox.Show("Please enter a valid positive number for clock speed.",
                                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                s_bl.Admin.StartSimulator(Interval);
                _simulatorRunning = true;
                
                // Update UI
                SimulatorToggleButton.Content = "⏹ Stop Simulator";
                SimulatorToggleButton.Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)); // Red
                SimulatorIntervalInput.IsReadOnly = true;
                
                // Disable time control buttons
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
        // Iterate through visual tree to find buttons
        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (button.Content?.ToString()?.StartsWith("+") ?? false)
            {
                button.IsEnabled = enable;
            }
        }
    }

    private void EnableDatabaseButtons(bool enable)
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var content = button.Content?.ToString() ?? "";
            if (content.Contains("Initialize") || content.Contains("Reset"))
            {
                button.IsEnabled = enable;
            }
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
