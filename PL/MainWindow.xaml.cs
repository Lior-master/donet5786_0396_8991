using BlApi;
using BO;
using PL.Courier;
using PL.Order;
using System;
using System.ComponentModel;
using System.Windows;

namespace PL
{
    public partial class MainWindow : Window
    {
        // ================================
        //  ACCESS TO BL LAYER (REQUIRED)
        // ================================
        static readonly IBl s_bl = Factory.Get();

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
            try
            {
                CurrentTime = s_bl.Admin.GetClock();
            }
            catch { }
        }

        // ================================
        //      OBSERVER: CONFIG
        // ================================
        private void ConfigObserver()
        {
            try
            {
                Configuration = s_bl.Admin.GetConfig();
            }
            catch { }
        }

        // ================================
        //   INITIALIZATION ON OPENING
        // ================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load current data
            CurrentTime = s_bl.Admin.GetClock();
            Configuration = s_bl.Admin.GetConfig();

            // Register observers
            s_bl.Admin.AddClockObserver(ClockObserver);
            s_bl.Admin.AddConfigObserver(ConfigObserver);
        }

        // ================================
        //      CLEANUP ON CLOSING
        // ================================
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                s_bl.Admin.RemoveClockObserver(ClockObserver);
                s_bl.Admin.RemoveConfigObserver(ConfigObserver);
            }
            catch
            {
                // Some BL versions do not implement RemoveObserver — ignore.
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

        private void ApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                s_bl.Admin.SetConfig(Configuration);
                MessageBox.Show("Configuration updated successfully.",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void InitDB_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Initialize database?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                s_bl.Admin.InitializeDB();
                MessageBox.Show("Database initialized.");
            }
        }

        private void ResetDB_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset database?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                s_bl.Admin.ResetDB();
                MessageBox.Show("Database reset.");
            }
        }

        // =======================================================
        //     BUTTONS: OPENING LIST SCREENS
        // =======================================================

        private void CouriersList_Click(object sender, RoutedEventArgs e)
        {
            new CourierListWindow().Show();
        }

        private void OrdersList_Click(object sender, RoutedEventArgs e)
        {
            new OrderListWindow().Show();
        }
    }
}
