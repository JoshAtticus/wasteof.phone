using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using wasteof.phone.Services;
using wasteof.phone.Models;

namespace wasteof.phone
{
    public sealed partial class MainPage : Page
    {
        private List<Post> _feedPosts = new List<Post>();
        private List<Post> _explorePosts = new List<Post>();
        private List<Post> _myPosts = new List<Post>();

        private int _feedPage = 1;
        private int _explorePage = 1;
        private int _myPostsPage = 1;

        private bool _feedHasMore = true;
        private bool _exploreHasMore = true;
        private bool _myPostsHasMore = true;

        public MainPage()
        {
            this.InitializeComponent();
            
            if (!ApiService.Instance.IsLoggedIn)
            {
                this.Loaded += (s, e) => Frame.Navigate(typeof(LoginPage));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();

            if (ApiService.Instance.IsLoggedIn)
            {
                RefreshActiveTab();
            }
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ApiService.Instance.IsLoggedIn)
            {
                RefreshActiveTab();
            }
        }

        private void RefreshActiveTab()
        {
            switch (MainPivot.SelectedIndex)
            {
                case 0:
                    _feedPage = 1;
                    _feedPosts.Clear();
                    _feedHasMore = true;
                    LoadFeedAsync();
                    break;
                case 1:
                    _explorePage = 1;
                    _explorePosts.Clear();
                    _exploreHasMore = true;
                    LoadExploreAsync();
                    break;
                case 2:
                    _myPostsPage = 1;
                    _myPosts.Clear();
                    _myPostsHasMore = true;
                    LoadAccountAsync();
                    break;
            }
        }

        private async System.Threading.Tasks.Task AugmentPostLoveStatusAsync(IEnumerable<Post> posts)
        {
            if (posts == null || !ApiService.Instance.IsLoggedIn) return;

            var username = ApiService.Instance.CurrentUsername;
            foreach (var post in posts)
            {
                if (post != null)
                {
                    if (post.IsEmptyRepost && post.Repost != null)
                    {
                        var loved = await ApiService.Instance.GetPostLoveStatusAsync(post.Repost.Id, username);
                        post.Repost.IsLoving = loved;
                        post.RaisePropertyChanged("DisplayLoveHeartFill");
                        post.RaisePropertyChanged("DisplayLoveHeartStroke");
                    }
                    else
                    {
                        var loved = await ApiService.Instance.GetPostLoveStatusAsync(post.Id, username);
                        post.IsLoving = loved;
                    }
                }
            }
        }

        private async void LoadFeedAsync()
        {
            FeedProgressBar.Visibility = Visibility.Visible;
            LoadMoreFeedButton.IsEnabled = false;
            var feed = await ApiService.Instance.GetFeedAsync(ApiService.Instance.CurrentUsername, _feedPage);
            FeedProgressBar.Visibility = Visibility.Collapsed;

            if (feed != null && feed.Posts != null)
            {
                _feedPosts.AddRange(feed.Posts);
                FeedListView.ItemsSource = null;
                FeedListView.ItemsSource = _feedPosts;

                _feedHasMore = feed.Posts.Count >= 20;
                LoadMoreFeedButton.Visibility = _feedHasMore ? Visibility.Visible : Visibility.Collapsed;

                var task = AugmentPostLoveStatusAsync(feed.Posts);
            }
            LoadMoreFeedButton.IsEnabled = true;
        }

        private async void LoadExploreAsync()
        {
            ExploreProgressBar.Visibility = Visibility.Visible;
            LoadMoreExploreButton.IsEnabled = false;
            var explore = await ApiService.Instance.GetExplorePostsAsync(_explorePage);
            ExploreProgressBar.Visibility = Visibility.Collapsed;

            if (explore != null && explore.Posts != null)
            {
                _explorePosts.AddRange(explore.Posts);
                ExploreListView.ItemsSource = null;
                ExploreListView.ItemsSource = _explorePosts;

                _exploreHasMore = explore.Posts.Count >= 20;
                LoadMoreExploreButton.Visibility = _exploreHasMore ? Visibility.Visible : Visibility.Collapsed;

                var task = AugmentPostLoveStatusAsync(explore.Posts);
            }
            LoadMoreExploreButton.IsEnabled = true;
        }



        private async void LoadAccountAsync()
        {
            AccountProgressBar.Visibility = Visibility.Visible;
            
            var user = await ApiService.Instance.GetUserProfileAsync(ApiService.Instance.CurrentUsername);
            if (user != null)
            {
                AccountProfilePictureImage.Source = new BitmapImage(new Uri(user.ProfilePictureUrl));
                AccountBannerImage.Source = new BitmapImage(new Uri(user.BannerUrl));
                AccountDisplayName.Text = user.Name;
                AccountBio.Text = string.IsNullOrEmpty(user.Bio) ? "no bio yet." : user.Bio;

                if (user.Stats != null)
                {
                    MyPostsCount.Text = user.Stats.Posts.ToString();
                    MyFollowersCount.Text = user.Stats.Followers.ToString();
                    MyFollowingCount.Text = user.Stats.Following.ToString();
                }
            }

            LoadMoreMyPostsButton.IsEnabled = false;
            var feed = await ApiService.Instance.GetUserPostsAsync(ApiService.Instance.CurrentUsername, _myPostsPage);
            AccountProgressBar.Visibility = Visibility.Collapsed;

            if (feed != null && feed.Posts != null)
            {
                _myPosts.AddRange(feed.Posts);
                MyPostsListView.ItemsSource = null;
                MyPostsListView.ItemsSource = _myPosts;

                _myPostsHasMore = feed.Posts.Count >= 20;
                LoadMoreMyPostsButton.Visibility = _myPostsHasMore ? Visibility.Visible : Visibility.Collapsed;

                var task = AugmentPostLoveStatusAsync(feed.Posts);
            }
            LoadMoreMyPostsButton.IsEnabled = true;
        }

        private void PostListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as Post;
            if (post != null)
            {
                var targetPost = (post.IsEmptyRepost && post.Repost != null) ? post.Repost : post;
                Frame.Navigate(typeof(PostDetailsPage), targetPost.Id);
            }
        }

        private void Username_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var username = element.Tag as string;
                if (!string.IsNullOrEmpty(username))
                {
                    Frame.Navigate(typeof(UserProfilePage), username);
                    return;
                }

                var post = element.DataContext as Post;
                if (post != null)
                {
                    var targetPosterName = (post.IsEmptyRepost && post.Repost != null) ? post.Repost.Poster.Name : post.Poster.Name;
                    Frame.Navigate(typeof(UserProfilePage), targetPosterName);
                    return;
                }

                var comment = element.DataContext as Comment;
                if (comment != null)
                {
                    Frame.Navigate(typeof(UserProfilePage), comment.Poster.Name);
                    return;
                }

                var notif = element.DataContext as Notification;
                if (notif != null && notif.Data != null && notif.Data.Actor != null)
                {
                    Frame.Navigate(typeof(UserProfilePage), notif.Data.Actor.Name);
                    return;
                }
            }
        }

        private void NotificationsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(NotificationsPage));
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

            var targetPost = (post.IsEmptyRepost && post.Repost != null) ? post.Repost : post;

            button.IsEnabled = false;
            var response = await ApiService.Instance.ToggleLoveAsync(targetPost.Id);
            if (response != null && response.NewState != null)
            {
                targetPost.IsLoving = response.NewState.IsLoving;
                targetPost.LovesCount = response.NewState.Loves;
                if (post.IsEmptyRepost)
                {
                    post.RaisePropertyChanged("DisplayLovesCount");
                    post.RaisePropertyChanged("DisplayLoveHeartFill");
                    post.RaisePropertyChanged("DisplayLoveHeartStroke");
                }
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
                var targetPost = (post.IsEmptyRepost && post.Repost != null) ? post.Repost : post;
                Frame.Navigate(typeof(PostDetailsPage), targetPost.Id);
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

                var targetPost = (post.IsEmptyRepost && post.Repost != null) ? post.Repost : post;
                Frame.Navigate(typeof(ComposePage), targetPost.Id);
            }
        }

        private void ComposeButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ComposePage));
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshActiveTab();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            ApiService.Instance.ClearSession();
            Frame.Navigate(typeof(LoginPage));
        }

        private void LoadMoreFeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_feedHasMore)
            {
                _feedPage++;
                LoadFeedAsync();
            }
        }

        private void LoadMoreExploreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_exploreHasMore)
            {
                _explorePage++;
                LoadExploreAsync();
            }
        }

        private void LoadMoreMyPostsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_myPostsHasMore)
            {
                _myPostsPage++;
                LoadAccountAsync();
            }
        }
    }
}
