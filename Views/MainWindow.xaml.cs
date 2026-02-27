using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AccommodationSystem.Services;

namespace AccommodationSystem.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new CheckinPage());
            UpdateLangToggleUI();
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

        private void LangToggle_Click(object sender, MouseButtonEventArgs e)
        {
            LanguageService.Toggle();
            UpdateLangToggleUI();
        }

        private void UpdateLangToggleUI()
        {
            bool isJA = LanguageService.Current == AppLanguage.JA;

            // Active language pill: white background + dark text
            // Inactive language pill: transparent background + muted text
            var activeBg   = Brushes.White;
            var activeFg   = new SolidColorBrush(Color.FromRgb(0x15, 0x58, 0xA0));   // #1558A0
            var inactiveBg = Brushes.Transparent;
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0xAC, 0xCE));   // #7AACCE

            EnPill.Background = isJA ? inactiveBg : activeBg;
            EnText.Foreground = isJA ? inactiveFg : activeFg;
            JaPill.Background = isJA ? activeBg   : inactiveBg;
            JaText.Foreground = isJA ? activeFg   : inactiveFg;
        }
    }
}
