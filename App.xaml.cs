using System.Windows;
using AccommodationSystem.Data;

namespace AccommodationSystem
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseService.Initialize();

            if (DatabaseService.ApplyPasswordResetIfRequested())
            {
                MessageBox.Show(
                    "管理者パスワードを初期値にリセットしました。\n\nパスワード: admin1234\n\nログイン後すぐに新しいパスワードに変更してください。",
                    "パスワードリセット完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
