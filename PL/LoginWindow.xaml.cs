using System;
using System.Windows;
using BlApi;

namespace PL;

public partial class LoginWindow : Window
{
    // BL access
    private static readonly IBl s_bl = Factory.Get();

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        // Clear all previous error messages
        ClearErrorMessages();

        try
        {
            bool hasErrors = false;

            // 1) Parse ID
            if (!int.TryParse(tbId.Text?.Trim(), out int id) || id <= 0)
            {
                ShowFieldError(tbIdError, "Please enter a valid ID");
                hasErrors = true;
            }

            // 2) Password check
            string password = pbPassword.Password ?? "";
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowFieldError(tbPasswordError, "Please enter a password");
                hasErrors = true;
            }

            // Stop if there are validation errors
            if (hasErrors)
                return;

            // Use the parsed id variable instead of incorrect TryParse usage
            if (s_bl.Courier.Login(id, password) == BO.Administrator.Director)
            {
                new MainWindow().Show();
                Close();
            }
            //else if(s_bl.Courier.Login(id, password) == BO.Administrator.Courier)
            //{
            //    new CourierWindow(id).Show();
            //}
        }
        catch (Exception ex)
        {
            // Stage requirement: catch exceptions and show user-friendly message
            ShowGeneralError("Login failed.\n" + ex.Message);
        }
    }

    private void ClearInputs()
    {
        tbId.Text = string.Empty;
        pbPassword.Password = string.Empty;
        ClearErrorMessages();
    }

    private void ClearErrorMessages()
    {
        tbIdError.Visibility = Visibility.Collapsed;
        tbPasswordError.Visibility = Visibility.Collapsed;
        tbGeneralError.Visibility = Visibility.Collapsed;
        tbIdError.Text = string.Empty;
        tbPasswordError.Text = string.Empty;
        tbGeneralError.Text = string.Empty;
    }

    private void ShowFieldError(System.Windows.Controls.TextBlock errorLabel, string message)
    {
        errorLabel.Text = message;
        errorLabel.Visibility = Visibility.Visible;
    }

    private void ShowGeneralError(string message)
    {
        tbGeneralError.Text = message;
        tbGeneralError.Visibility = Visibility.Visible;
    }

    // Add the missing event handlers referenced in XAML
    private void TbId_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnLogin_Click(sender, new RoutedEventArgs());
        }
    }

    private void TbId_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Clear ID error when user starts typing
        if (tbIdError != null && tbIdError.Visibility == Visibility.Visible)
        {
            tbIdError.Visibility = Visibility.Collapsed;
            tbIdError.Text = string.Empty;
        }
    }


    private void PbPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnLogin_Click(sender, new RoutedEventArgs());
        }
    }
}
