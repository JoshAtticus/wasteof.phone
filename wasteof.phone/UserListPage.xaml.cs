using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using wasteof.phone.Models;
using wasteof.phone.Services;

namespace wasteof.phone
{
    public sealed partial class UserListPage : Page
    {
        private string _username;
        private string _listType; // "followers" or "following"

        public UserListPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameter = e.Parameter as string;
            if (string.IsNullOrEmpty(parameter)) return;

            var parts = parameter.Split(':');
            if (parts.Length < 2) return;

            _listType = parts[0];
            _username = parts[1];

            PageHeaderTitle.Text = _listType;

            await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            ListProgressBar.Visibility = Visibility.Visible;
            List<User> users = null;

            if (_listType.Equals("followers", StringComparison.OrdinalIgnoreCase))
            {
                users = await ApiService.Instance.GetFollowersAsync(_username);
            }
            else if (_listType.Equals("following", StringComparison.OrdinalIgnoreCase))
            {
                users = await ApiService.Instance.GetFollowingAsync(_username);
            }

            if (users != null)
            {
                UsersListView.ItemsSource = users;
            }

            ListProgressBar.Visibility = Visibility.Collapsed;
        }

        private void UsersListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var user = e.ClickedItem as User;
            if (user != null)
            {
                Frame.Navigate(typeof(UserProfilePage), user.Name);
            }
        }
    }
}
