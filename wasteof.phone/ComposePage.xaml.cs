using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using wasteof.phone.Services;
using wasteof.phone.Models;

namespace wasteof.phone
{
    public sealed partial class ComposePage : Page
    {
        private string _repostId = null;

        public ComposePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var targetId = e.Parameter as string;
            if (targetId != null)
            {
                _repostId = targetId;
                PageHeaderTitle.Text = "repost";
                RepostIndicator.Text = $"reposting: {targetId}";
                RepostIndicator.Visibility = Visibility.Visible;
                PostContentTextBox.PlaceholderText = "add a comment to this repost (optional)...";
            }
            else
            {
                _repostId = null;
                PageHeaderTitle.Text = "compose";
                RepostIndicator.Visibility = Visibility.Collapsed;
                PostContentTextBox.PlaceholderText = "what's on your mind?";
            }

            PostContentTextBox.Focus(FocusState.Programmatic);
        }

        private void PostContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CharCountTextBlock.Text = $"{PostContentTextBox.Text.Length} / 5000";
        }

        private async void PostAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            string content = PostContentTextBox.Text.Trim();

            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(_repostId))
            {
                var dialog = new MessageDialog("Post content cannot be empty.");
                await dialog.ShowAsync();
                return;
            }

            PostAppBarButton.IsEnabled = false;
            CancelAppBarButton.IsEnabled = false;
            PostContentTextBox.IsEnabled = false;

            Post createdPost = null;
            if (!string.IsNullOrEmpty(_repostId))
            {
                createdPost = await ApiService.Instance.CreatePostAsync(content, _repostId);
            }
            else
            {
                createdPost = await ApiService.Instance.CreatePostAsync(content);
            }

            if (createdPost != null)
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else
                {
                    Frame.Navigate(typeof(MainPage));
                }
            }
            else
            {
                var dialog = new MessageDialog("Failed to publish post. Please check your connection and try again.");
                await dialog.ShowAsync();

                PostAppBarButton.IsEnabled = true;
                CancelAppBarButton.IsEnabled = true;
                PostContentTextBox.IsEnabled = true;
            }
        }

        private void CancelAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MainPage));
            }
        }
    }
}
