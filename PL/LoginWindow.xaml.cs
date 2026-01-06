using System;
using System.Windows;
using BlApi;

namespace PL;

public partial class LoginWindow : Window
{
    // BL access (adapt if your Factory is elsewhere)
    private static readonly IBl s_bl = Factory.Get();

    // If you implemented the password bonus, set to true.
    // If not implemented, set to false => the password is ignored.
    private const bool USE_PASSWORD = false;

    public LoginWindow()
    {
        InitializeComponent();

        // Hide password UI if bonus not implemented
        if (!USE_PASSWORD)
        {
            pbPassword.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 1) Parse ID
            if (!int.TryParse(tbId.Text?.Trim(), out int id) || id <= 0)
            {
                MessageBox.Show("Veuillez entrer un ID numérique valide.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2) Optional password check (bonus)
            if (USE_PASSWORD)
            {
                string password = pbPassword.Password ?? "";
                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Veuillez entrer un mot de passe.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // IMPORTANT:
                // Replace this with your real validation logic if you implemented password.
                // Example idea:
                // - Admin password stored in config
                // - Courier password stored in courier entity
                //
                // Here we just show where it should happen.
            }

            bool isAdmin = rbAdmin.IsChecked == true;

            if (isAdmin)
            {
                // 3) Validate admin ID from configuration
                // Adapt: your config property name might be BossId / AdminId etc.
                int adminId = s_bl.Admin.GetConfig().BossId;

                if (id != adminId)
                {
                    MessageBox.Show("ID administrateur incorrect.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4) Open admin main window, DO NOT close login window
                var w = new AdminMainWindow();
                w.Show();

                ClearInputs();
                return;
            }
            else
            {
                // 3) Validate courier exists in DB via BL
                // Adapt this line to your BL API:
                // - could be s_bl.Courier.Get(id)
                // - could be s_bl.Couriers.Read(id)
                // - etc.
                var courier = s_bl.Courier.Read(id); // <-- CHANGE if needed

                // 4) Open courier window in update mode (existing courier)
                var w = new PL.Courier.CourierWindow(id); // <-- adapt constructor to yours
                w.Show();

                ClearInputs();
                return;
            }
        }
        catch (Exception ex)
        {
            // Stage requirement: catch exceptions and show user-friendly message
            MessageBox.Show(
                "Connexion impossible.\n" + ex.Message,
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ClearInputs()
    {
        tbId.Text = string.Empty;
        pbPassword.Password = string.Empty;
        rbAdmin.IsChecked = true;
    }
}
