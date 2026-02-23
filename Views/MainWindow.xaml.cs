using System.Windows;
using AccommodationSystem.Data;

namespace AccommodationSystem.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new CheckinPage());
        }

        private void AdminButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AdminLoginWindow { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                var adminWin = new AdminWindow { Owner = this };
                adminWin.ShowDialog();
            }
        }
    }
}
