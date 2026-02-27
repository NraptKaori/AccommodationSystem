using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccommodationSystem.Data;
using AccommodationSystem.Models;
using AccommodationSystem.Services;

namespace AccommodationSystem.Views
{
    public partial class CheckinPage : Page
    {
        public CheckinPage()
        {
            InitializeComponent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) DoSearch();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        private void DoSearch()
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show(
                    LanguageService.T("search_err_empty"),
                    LanguageService.T("err_input_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var results = DatabaseService.SearchReservations(query);
            WelcomePanel.Visibility = Visibility.Collapsed;

            if (results.Count == 0)
            {
                ResultList.Visibility = Visibility.Collapsed;
                NoResultPanel.Visibility = Visibility.Visible;
            }
            else
            {
                NoResultPanel.Visibility = Visibility.Collapsed;
                ResultList.ItemsSource = results;
                ResultList.Visibility = Visibility.Visible;
            }
        }

        private void ResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultList.SelectedItem is Reservation r)
            {
                var dlg = new PaymentWindow(r) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
                // 画面を更新
                ResultList.UnselectAll();
                DoSearch();
            }
        }
    }
}
