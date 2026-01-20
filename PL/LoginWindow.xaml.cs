using System;
using System.Windows;
using System.Windows.Input;
using BlApi;

namespace PL;

/// <summary>
/// LoginWindow - User authentication interface for the Delivery Management System.
/// 
/// This window provides a secure login interface that validates user credentials
/// (ID and password) and determines user role (Administrator/Director or Courier)
/// through the Business Logic layer.
/// 
/// Key Features:
/// - ID and password-based authentication via BL.Courier.Login()
/// - Toggle password visibility (show/hide password text)
/// - Password field clearing on failed login attempts
/// - Enter key support for streamlined input flow (ID → Password → Submit)
/// - Role-based window navigation (MainWindow for Director, CourierWindow for Courier)
/// - User-friendly error messages via MessageBox
/// - Automatic UI reset after login attempts
/// 
/// Authentication Architecture:
/// - Single BL API call per login action (Stage 6 rule compliance)
/// - Uses BL.Courier.Login(id, password) for centralized authentication
/// - Returns Administrator enum indicating user role
/// - Administrator.Director → Open MainWindow (admin interface)
/// - Administrator.Courier → Access denied (redirect not implemented)
/// - Any other result → Login failure
/// 
/// Password Management:
/// - Two password input modes: PasswordBox (hidden) and TextBox (visible)
/// - Toggle button switches between modes with eye/closed-eye emoji
/// - Password automatically switched between controls when toggling visibility
/// - Password cleared from both controls after login attempt (security best practice)
/// 
/// Error Handling:
/// - Validates ID is a positive integer before BL call
/// - Validates password is not empty before BL call
/// - Catches all exceptions and shows generic error message (no technical details)
/// - MessageBox used for user feedback (per project design)
/// </summary>
public partial class LoginWindow : Window
{
    /// <summary>
    /// Business Logic singleton instance for user authentication.
    /// Obtained via Factory.Get() to ensure single instance throughout application lifecycle.
    /// 
    /// Primary use: Call BL.Courier.Login(id, password) to authenticate user
    /// Returns: Administrator enum (Director, Courier, None)
    /// </summary>
    private static readonly IBl s_bl = Factory.Get();

    /// <summary>
    /// Tracks whether password is currently displayed as visible text or hidden.
    /// 
    /// true  → tbPasswordVisible (TextBox) is visible, pbPassword (PasswordBox) is hidden
    /// false → pbPassword (PasswordBox) is visible, tbPasswordVisible (TextBox) is hidden
    /// 
    /// Used to synchronize password content between the two input controls when
    /// user toggles password visibility via the eye icon button.
    /// </summary>
    private bool _isPasswordVisible = false;
    
    /// <summary>
    /// Prevents multiple director logins simultaneously.
    /// </summary>
    public bool _directorLoggedIn = false;

    /// <summary>
    /// Initializes a new instance of the LoginWindow.
    /// 
    /// Calls InitializeComponent() to initialize XAML resources and event handlers.
    /// Sets initial focus to tbId (ID input field) so user can begin typing immediately.
    /// </summary>
    public LoginWindow()
    {
        InitializeComponent();
        tbId.Focus(); // Start with ID field focused for immediate input
    }

    /// <summary>
    /// Handles Login button click and Enter key press events.
    /// This is the main authentication entry point.
    /// 
    /// Execution Flow:
    /// 1. Parse and validate ID input (must be positive integer, required)
    /// 2. Validate password input (must not be empty, required)
    /// 3. Call BL.Courier.Login(id, password) - SINGLE BL CALL (Stage 6 rule)
    /// 4. Check returned Administrator role:
    ///    - Administrator.Director → Create and show MainWindow, close LoginWindow
    ///    - Not Director → Show "Access denied" message, remain on login screen
    /// 5. Catch any exception and show generic error message
    /// 6. Always call ResetPasswordUi() in finally block to clear sensitive data
    /// 
    /// Error Messages (User-Friendly):
    /// - Invalid input → "Please enter a valid ID (positive number)."
    /// - Missing password → "Please enter a password."
    /// - Login failure → "Login failed. Please verify your ID and password and try again."
    /// - Access denied → "This account does not have permission to access the system."
    /// 
    /// Security Notes:
    /// - Passwords are never logged or displayed in error messages
    /// - ResetPasswordUi() ensures password is cleared from both input controls
    /// - After login attempt, UI returns to default hidden password mode
    /// </summary>
    /// <param name="sender">Event originator (LoginButton or PasswordBox or IDField)</param>
    /// <param name="e">Event routing arguments</param>
    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // ============================================================
            // STEP 1: VALIDATE ID INPUT
            // ============================================================
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

            // ============================================================
            // STEP 2: VALIDATE AND RETRIEVE PASSWORD INPUT
            // ============================================================
            // Get password from active control based on visibility state
            var password = _isPasswordVisible
                ? (tbPasswordVisible.Text ?? string.Empty)
                : (pbPassword.Password ?? string.Empty);

            // ============================================================
            // STEP 3: VALIDATE PASSWORD IS NOT EMPTY
            // ============================================================
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Please enter a password.",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // Focus appropriate password field
                if (_isPasswordVisible)
                    tbPasswordVisible.Focus();
                else
                    pbPassword.Focus();

                return;
            }

            // ============================================================
            // STEP 4: CALL BL AUTHENTICATION METHOD
            // ============================================================
            // Single BL call per Stage 6 requirements
            var role = s_bl.Courier.Login(id, password);

            // ============================================================
            // STEP 5: HANDLE AUTHENTICATION RESULT - DIRECTOR/ADMIN ROLE
            // ============================================================
            if (role == BO.Administrator.Director)
            { 
                // Prevent concurrent director sessions
                if (_directorLoggedIn)
                {
                    MessageBox.Show(
                        "A director is already logged in.",
                        "Access denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Open main admin interface
                new MainWindow().Show();
                _directorLoggedIn = true;
                ResetLoginForm();
                return;
            }

            // ============================================================
            // STEP 6: HANDLE COURIER ROLE(FUTURE IMPLEMENTATION)
            // ============================================================
            if (role == BO.Administrator.Courier)
            {                
                new CourierPersonalWindow(id).Show();
                ResetLoginForm();
                return;
            }

            // ============================================================
            // STEP 7: HANDLE LOGIN FAILURE - ACCESS DENIED
            // ============================================================
            MessageBox.Show(
                "This account does not have permission to access the system.",
                "Access denied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception)
        {
            // Generic error message for security (no technical details exposed)
            MessageBox.Show(
                "Login failed. Please verify your ID and password and try again.",
                "Login error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            // Always clear sensitive data regardless of outcome
            ResetPasswordUi();
        }
    }

    /// <summary>
    /// Handles password visibility toggle button click.
    /// Switches between hidden (PasswordBox) and visible (TextBox) password input modes.
    /// 
    /// Toggle Logic:
    /// 1. Flip _isPasswordVisible flag
    /// 2. If becoming visible:
    ///    - Copy password from PasswordBox to TextBox
    ///    - Show TextBox, hide PasswordBox
    ///    - Change button emoji to 🙈 (closed eyes)
    ///    - Move focus and cursor to end of TextBox
    /// 3. If becoming hidden:
    ///    - Copy password from TextBox to PasswordBox
    ///    - Show PasswordBox, hide TextBox
    ///    - Change button emoji to 👁 (open eye)
    ///    - Move focus to PasswordBox
    /// 
    /// Data Flow:
    /// - Password is transferred between controls using Copy→Paste approach
    /// - Only one control is visible at a time (XOR visibility pattern)
    /// - Focus follows the active input control for seamless UX
    /// - Cursor position maintained at end of text for typing continuity
    /// 
    /// UI State Transitions:
    /// - Initial state: PasswordBox visible, TextBox hidden, button shows 👁
    /// - After click: TextBox visible, PasswordBox hidden, button shows 🙈
    /// - After click: Back to initial state
    /// </summary>
    /// <param name="sender">The toggle button that was clicked</param>
    /// <param name="e">Event routing arguments (unused)</param>
    private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (_isPasswordVisible)
        {
            // Switch to visible password mode
            tbPasswordVisible.Text = pbPassword.Password;
            tbPasswordVisible.Visibility = Visibility.Visible;
            pbPassword.Visibility = Visibility.Collapsed;
            btnTogglePassword.Content = "🙈"; // Hide password icon
            tbPasswordVisible.Focus();
            tbPasswordVisible.CaretIndex = tbPasswordVisible.Text.Length;
        }
        else
        {
            // Switch to hidden password mode
            pbPassword.Password = tbPasswordVisible.Text ?? string.Empty;
            pbPassword.Visibility = Visibility.Visible;
            tbPasswordVisible.Visibility = Visibility.Collapsed;
            btnTogglePassword.Content = "👁"; // Show password icon
            pbPassword.Focus();
        }
    }

    /// <summary>
    /// Clears all password data and resets UI to default hidden password state.
    /// This is a security-critical method called after every login attempt.
    /// 
    /// Cleanup Actions:
    /// 1. Clear PasswordBox content (secure storage)
    /// 2. Clear TextBox content (visible storage)
    /// 3. Set _isPasswordVisible to false (default hidden mode)
    /// 4. Make PasswordBox visible, hide TextBox
    /// 5. Reset button content to 👁 (show password icon)
    /// 
    /// Security Rationale:
    /// - Clearing both controls ensures password is not left in memory
    /// - Resetting to PasswordBox ensures default secure mode
    /// - Called in finally block of BtnLogin_Click to execute after any outcome
    /// - Prevents accidental password exposure on subsequent login attempts
    /// 
    /// Called in Contexts:
    /// - After successful login (in finally block)
    /// - After login failure (in finally block)
    /// - After any exception during authentication (in finally block)
    /// </summary>
    private void ResetPasswordUi()
    {
        // Clear sensitive data from both controls
        pbPassword.Password = string.Empty;
        tbPasswordVisible.Text = string.Empty;

        // Reset to secure default state
        _isPasswordVisible = false;
        pbPassword.Visibility = Visibility.Visible;
        tbPasswordVisible.Visibility = Visibility.Collapsed;
        btnTogglePassword.Content = "👁";
    }

    /// <summary>
    /// Clears all login form fields and resets to initial state.
    /// </summary>
    private void ResetLoginForm()
    {
        tbId.Text = string.Empty;
        ResetPasswordUi();
    }

    /// <summary>
    /// Handles Enter key press in ID field to advance to password field.
    /// </summary>
    private void TbId_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnLogin_Click(sender, new RoutedEventArgs());
    }

    /// <summary>
    /// Handles Enter key press in the password input field (pbPassword or tbPasswordVisible).
    /// Triggers login process same as clicking the Login button.
    /// 
    /// Keyboard Navigation Flow:
    /// - User enters password and presses Enter in password field
    /// - This event fires and invokes BtnLogin_Click
    /// - Login validation and authentication occurs
    /// - If successful, MainWindow opens and LoginWindow closes
    /// 
    /// This method enables efficient keyboard-only workflow without mouse interaction.
    /// Works with both PasswordBox (pbPassword) and TextBox (tbPasswordVisible) modes.
    /// </summary>
    /// <param name="sender">The password field (PasswordBox or TextBox) where Enter was pressed</param>
    /// <param name="e">Keyboard event containing key information</param>
    private void PbPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnLogin_Click(sender, new RoutedEventArgs());
    }

    /// <summary>
    /// Handles close button click to terminate the application.
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
