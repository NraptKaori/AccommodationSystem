using System;
using System.Windows;
using System.Windows.Input;
using AccommodationSystem.Data;

namespace AccommodationSystem.Views
{
    public partial class AdminLoginWindow : Window
    {
        public AdminLoginWindow()
        {
            InitializeComponent();
            CheckLockStatus();
        }

        private void CheckLockStatus()
        {
            if (DatabaseService.IsLoginLocked())
            {
                var lockUntil = DatabaseService.GetLockUntil();
                ErrorText.Text = "ログインがロックされています。\n解除時刻: " + lockUntil.ToString("HH:mm:ss");
                PasswordBox.IsEnabled = false;
                LoginButton.IsEnabled = false;
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryLogin();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void TryLogin()
        {
            if (DatabaseService.IsLoginLocked())
            {
                CheckLockStatus();
                return;
            }

            var settings = DatabaseService.GetSettings();
            if (BCrypt.Net.BCrypt.Verify(PasswordBox.Password, settings.AdminPasswordHash))
            {
                DatabaseService.RecordLoginAttempt(true);
                DatabaseService.Log("admin_login", "Admin login succeeded");
                DialogResult = true;
                Close();
            }
            else
            {
                DatabaseService.RecordLoginAttempt(false);

                if (DatabaseService.IsLoginLocked())
                {
                    var lockUntil = DatabaseService.GetLockUntil();
                    ErrorText.Text = "5回失敗したためロックしました。\n30分後に再試行してください。";
                    PasswordBox.IsEnabled = false;
                    LoginButton.IsEnabled = false;
                }
                else
                {
                    ErrorText.Text = "パスワードが正しくありません。";
                }
                PasswordBox.Clear();
            }
        }
    }
}
