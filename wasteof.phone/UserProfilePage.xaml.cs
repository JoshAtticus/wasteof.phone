using System;
using System.Collections.Generic;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Input;
using wasteof.phone.Services;
using wasteof.phone.Models;

namespace wasteof.phone
{
    public sealed partial class UserProfilePage : Page
    {
        private string _username;
        private User _user;
        private bool _isFollowing = false;
        private List<Post> _posts = new List<Post>();
        private int _profilePostsPage = 1;
        private bool _profilePostsHasMore = true;

        public UserProfilePage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var username = e.Parameter as string;
            if (username != null)
            {
                _username = username;
                UsernameHeaderTextBlock.Text = username.ToLower();
                await LoadProfileDataAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadProfileDataAsync()
        {
            _profilePostsPage = 1;
            _posts.Clear();
            _profilePostsHasMore = true;
            _user = await ApiService.Instance.GetUserProfileAsync(_username);
            if (_user == null)
            {
                var dialog = new MessageDialog("Failed to load user profile.");
                await dialog.ShowAsync();
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }

            // Update Header fields
            ProfilePictureImage.Source = new BitmapImage(new Uri(_user.ProfilePictureUrl));
            BannerImage.Source = new BitmapImage(new Uri(_user.BannerUrl));
            DisplayNameTextBlock.Text = _user.Name;
            OnlineStatusTextBlock.Text = _user.Online ? "online" : "offline";
            BioTextBlock.Text = string.IsNullOrEmpty(_user.Bio) ? "no bio." : _user.Bio;

            if (_user.Stats != null)
            {
                PostsCountTextBlock.Text = _user.Stats.Posts.ToString();
                FollowersCountTextBlock.Text = _user.Stats.Followers.ToString();
                FollowingCountTextBlock.Text = _user.Stats.Following.ToString();
            }

            // Determine if Follow Button should be shown
            if (ApiService.Instance.IsLoggedIn)
            {
                if (_username.Equals(ApiService.Instance.CurrentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    FollowButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FollowButton.Visibility = Visibility.Visible;
                    // Fetch follow status
                    _isFollowing = await ApiService.Instance.GetFollowStatusAsync(_username, ApiService.Instance.CurrentUsername);
                    UpdateFollowButtonText();
                }
            }
            else
            {
                FollowButton.Visibility = Visibility.Visible;
                FollowButton.Content = "follow";
            }

            // Load user posts
            await LoadUserPostsAsync();
        }

        private void UpdateFollowButtonText()
        {
            FollowButton.Content = _isFollowing ? "unfollow" : "follow";
        }

        private async System.Threading.Tasks.Task AugmentPostLoveStatusAsync(IEnumerable<Post> posts)
        {
            if (posts == null || !ApiService.Instance.IsLoggedIn) return;

            var username = ApiService.Instance.CurrentUsername;
            foreach (var post in posts)
            {
                if (post != null)
                {
                    var loved = await ApiService.Instance.GetPostLoveStatusAsync(post.Id, username);
                    post.IsLoving = loved;
                }
            }
        }

        private async System.Threading.Tasks.Task LoadUserPostsAsync()
        {
            LoadMoreProfilePostsButton.IsEnabled = false;
            var response = await ApiService.Instance.GetUserPostsAsync(_username, _profilePostsPage);
            if (response != null && response.Posts != null)
            {
                _posts.AddRange(response.Posts);
                UserPostsListView.ItemsSource = null;
                UserPostsListView.ItemsSource = _posts;

                _profilePostsHasMore = response.Posts.Count >= 20;
                LoadMoreProfilePostsButton.Visibility = _profilePostsHasMore ? Visibility.Visible : Visibility.Collapsed;

                var task = AugmentPostLoveStatusAsync(response.Posts);
            }
            LoadMoreProfilePostsButton.IsEnabled = true;
        }

        private async void FollowButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            FollowButton.IsEnabled = false;
            var response = await ApiService.Instance.ToggleFollowAsync(_username);
            if (response != null && response.NewState != null)
            {
                _isFollowing = response.NewState.IsFollowing;
                UpdateFollowButtonText();
                
                // Update followers stat dynamically
                FollowersCountTextBlock.Text = response.NewState.Followers.ToString();
            }
            FollowButton.IsEnabled = true;
        }

        private void UserPostsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as Post;
            if (post != null)
            {
                Frame.Navigate(typeof(PostDetailsPage), post.Id);
            }
        }

        private void FollowersStat_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(UserListPage), $"followers:{_username}");
        }

        private void FollowingStat_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(UserListPage), $"following:{_username}");
        }

        private async void LoveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var post = button.DataContext as Post;
            if (post == null) return;

            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            button.IsEnabled = false;
            var response = await ApiService.Instance.ToggleLoveAsync(post.Id);
            if (response != null && response.NewState != null)
            {
                post.IsLoving = response.NewState.IsLoving;
                post.LovesCount = response.NewState.Loves;
            }
            button.IsEnabled = true;
        }

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var post = button.DataContext as Post;
            if (post != null)
            {
                Frame.Navigate(typeof(PostDetailsPage), post.Id);
            }
        }

        private void RepostButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var post = button.DataContext as Post;
            if (post != null)
            {
                if (!ApiService.Instance.IsLoggedIn)
                {
                    Frame.Navigate(typeof(LoginPage));
                    return;
                }

                Frame.Navigate(typeof(ComposePage), post.Id);
            }
        }

        private async void LoadMoreProfilePostsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_profilePostsHasMore)
            {
                _profilePostsPage++;
                await LoadUserPostsAsync();
            }
        }
    }
}
