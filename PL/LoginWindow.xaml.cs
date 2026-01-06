using System;
using System.Windows;
using System.Windows.Input;
using BlApi;

namespace PL;

public partial class LoginWindow : Window
{
    private static readonly IBl s_bl = Factory.Get();

    // Password visibility state
    private bool _isPasswordVisible = false;

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Basic input validation (allowed in PL)
            var idText = tbId.Text?.Trim() ?? string.Empty;
            if (!int.TryParse(idText, out int id) || id <= 0)
            {
                MessageBox.Show(
                    "Please enter a valid ID (positive number).",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                tbId.Focus();
                tbId.SelectAll();
                return;
            }

            var password = _isPasswordVisible
                ? (tbPasswordVisible.Text ?? string.Empty)
                : (pbPassword.Password ?? string.Empty);

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Please enter a password.",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (_isPasswordVisible)
                    tbPasswordVisible.Focus();
                else
                    pbPassword.Focus();

                return;
            }

            // Stage 6 rule: one BL call per GUI action
            var role = s_bl.Courier.Login(id, password);

            if (role == BO.Administrator.Director)
            {
                new MainWindow().Show();
                Close();
                return;
            }

            //if (role == BO.Administrator.Courier)
            //{
            //    // Adjust namespace if needed
            //    new PL.Courier.CourierWindow(id).Show();
            //    Close();
            //    return;
            //}

            MessageBox.Show(
                "This account does not have permission to access the system.",
                "Access denied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception)
        {
            // User-friendly error (no technical details)
            MessageBox.Show(
                "Login failed. Please verify your ID and password and try again.",
                "Login error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ResetPasswordUi();
        }
    }

    private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (_isPasswordVisible)
        {
            // Show password
            tbPasswordVisible.Text = pbPassword.Password;
            tbPasswordVisible.Visibility = Visibility.Visible;
            pbPassword.Visibility = Visibility.Collapsed;

            btnTogglePassword.Content = "🙈";

            tbPasswordVisible.Focus();
            tbPasswordVisible.CaretIndex = tbPasswordVisible.Text.Length;
        }
        else
        {
            // Hide password
            pbPassword.Password = tbPasswordVisible.Text ?? string.Empty;
            pbPassword.Visibility = Visibility.Visible;
            tbPasswordVisible.Visibility = Visibility.Collapsed;

            btnTogglePassword.Content = "👁";

            pbPassword.Focus();
        }
    }

    private void ResetPasswordUi()
    {
        // Clear password + return to hidden mode
        pbPassword.Password = string.Empty;
        tbPasswordVisible.Text = string.Empty;

        _isPasswordVisible = false;

        pbPassword.Visibility = Visibility.Visible;
        tbPasswordVisible.Visibility = Visibility.Collapsed;
        btnTogglePassword.Content = "👁";
    }

    private void TbId_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnLogin_Click(sender, new RoutedEventArgs());
    }

    private void PbPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnLogin_Click(sender, new RoutedEventArgs());
    }
}
