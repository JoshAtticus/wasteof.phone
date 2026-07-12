using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using wasteof.phone.Services;

namespace wasteof.phone
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            bool isAdding = false;
            var param = e.Parameter as string;
            if (param != null && param == "add")
            {
                isAdding = true;
            }

            if (!isAdding)
            {
                Frame.BackStack.Clear();
                
                if (ApiService.Instance.IsLoggedIn)
                {
                    Frame.Navigate(typeof(MainPage));
                }
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ErrorTextBlock.Text = "Please enter both username and password.";
                return;
            }

            LoginButton.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            LoginProgress.IsActive = true;
            ErrorTextBlock.Text = string.Empty;

            bool success = await ApiService.Instance.LoginAsync(username, password);

            LoginProgress.IsActive = false;
            LoginButton.IsEnabled = true;
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;

            if (success)
            {
                Frame.Navigate(typeof(MainPage));
            }
            else
            {
                ErrorTextBlock.Text = "Login failed. Check your credentials or internet connection.";
            }
        }
    }
}
