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
    public sealed partial class NotificationsPage : Page
    {
        private List<Notification> _unreadNotifications = new List<Notification>();
        private List<Notification> _readNotifications = new List<Notification>();
        private int _unreadPage = 1;
        private int _readPage = 1;
        private bool _unreadHasMore = true;
        private bool _readHasMore = true;
        private bool _isUnreadLoading = false;
        private bool _isReadLoading = false;

        public NotificationsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            RefreshActiveTab();
        }

        private void NotificationsPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshActiveTab();
        }

        private void RefreshActiveTab()
        {
            if (NotificationsPivot.SelectedIndex == 0)
            {
                _unreadPage = 1;
                _unreadHasMore = true;
                LoadUnreadNotificationsAsync();
            }
            else
            {
                _readPage = 1;
                _readHasMore = true;
                LoadReadNotificationsAsync();
            }
        }

        private async void LoadUnreadNotificationsAsync()
        {
            if (_isUnreadLoading) return;
            _isUnreadLoading = true;

            UnreadProgressBar.Visibility = Visibility.Visible;
            LoadMoreUnreadButton.IsEnabled = false;

            var list = await ApiService.Instance.GetNotificationsAsync(true, _unreadPage);
            if (list != null)
            {
                if (_unreadPage == 1)
                {
                    _unreadNotifications.Clear();
                }
                _unreadNotifications.AddRange(list);
                UnreadListView.ItemsSource = null;
                UnreadListView.ItemsSource = _unreadNotifications;

                _unreadHasMore = list.Count >= 20;
                LoadMoreUnreadButton.Visibility = _unreadHasMore ? Visibility.Visible : Visibility.Collapsed;
            }

            UnreadProgressBar.Visibility = Visibility.Collapsed;
            LoadMoreUnreadButton.IsEnabled = true;
            _isUnreadLoading = false;
        }

        private async void LoadReadNotificationsAsync()
        {
            if (_isReadLoading) return;
            _isReadLoading = true;

            ReadProgressBar.Visibility = Visibility.Visible;
            LoadMoreReadButton.IsEnabled = false;

            var list = await ApiService.Instance.GetNotificationsAsync(false, _readPage);
            if (list != null)
            {
                if (_readPage == 1)
                {
                    _readNotifications.Clear();
                }
                _readNotifications.AddRange(list);
                ReadListView.ItemsSource = null;
                ReadListView.ItemsSource = _readNotifications;

                _readHasMore = list.Count >= 20;
                LoadMoreReadButton.Visibility = _readHasMore ? Visibility.Visible : Visibility.Collapsed;
            }

            ReadProgressBar.Visibility = Visibility.Collapsed;
            LoadMoreReadButton.IsEnabled = true;
            _isReadLoading = false;
        }

        private void LoadMoreUnreadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_unreadHasMore)
            {
                _unreadPage++;
                LoadUnreadNotificationsAsync();
            }
        }

        private void LoadMoreReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_readHasMore)
            {
                _readPage++;
                LoadReadNotificationsAsync();
            }
        }

        private async void NotificationListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var notif = e.ClickedItem as Notification;
            if (notif == null || notif.Data == null) return;

            if (!notif.Read)
            {
                await ApiService.Instance.MarkNotificationsReadAsync(new List<string> { notif.Id });
            }

            if (notif.Data.Post != null)
            {
                Frame.Navigate(typeof(PostDetailsPage), notif.Data.Post.Id);
            }
            else if (notif.Data.Comment != null && !string.IsNullOrEmpty(notif.Data.Comment.PostId))
            {
                Frame.Navigate(typeof(PostDetailsPage), notif.Data.Comment.PostId);
            }
            else if (notif.Data.Actor != null)
            {
                Frame.Navigate(typeof(UserProfilePage), notif.Data.Actor.Name);
            }
        }

        private void Username_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var notif = element.DataContext as Notification;
                if (notif != null && notif.Data != null && notif.Data.Actor != null)
                {
                    Frame.Navigate(typeof(UserProfilePage), notif.Data.Actor.Name);
                }
            }
        }

        private async void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotificationsPivot.SelectedIndex == 0)
            {
                var unreadIds = new List<string>();
                foreach (var notif in _unreadNotifications)
                {
                    if (!notif.Read) unreadIds.Add(notif.Id);
                }

                if (unreadIds.Count > 0)
                {
                    UnreadProgressBar.Visibility = Visibility.Visible;
                    bool success = await ApiService.Instance.MarkNotificationsReadAsync(unreadIds);
                    UnreadProgressBar.Visibility = Visibility.Collapsed;
                    if (success)
                    {
                        RefreshActiveTab();
                    }
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshActiveTab();
        }
    }
}
