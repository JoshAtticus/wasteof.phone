using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using wasteof.phone.Services;
using wasteof.phone.Models;

namespace wasteof.phone
{
    public sealed partial class UserProfilePage : Page
    {
        private string _username;
        private User _user;
        private bool _isFollowing = false;
        private ObservableCollection<Post> _posts = new ObservableCollection<Post>();
        private int _profilePostsPage = 1;
        private bool _profilePostsHasMore = true;

        // Wall state
        private int _wallPage = 1;
        private bool _wallHasMore = true;
        private string _replyParentId = null;
        private ObservableCollection<Comment> _wallComments = new ObservableCollection<Comment>();
        private bool _isWallLoaded = false;

        // Following & Followers state
        private int _followingPage = 1;
        private bool _followingHasMore = true;
        private int _followersPage = 1;
        private bool _followersHasMore = true;
        private ObservableCollection<User> _followingList = new ObservableCollection<User>();
        private ObservableCollection<User> _followersList = new ObservableCollection<User>();
        private bool _isFollowingLoaded = false;
        private bool _isFollowersLoaded = false;

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
                ProfilePivot.Title = username.ToLower();
                
                // Reset states
                _isWallLoaded = false;
                _isFollowingLoaded = false;
                _isFollowersLoaded = false;
                ProfilePivot.SelectedIndex = 0;

                await LoadProfileDataAsync();
            }
        }

        private async Task LoadProfileDataAsync()
        {
            ProfileProgressBar.Visibility = Visibility.Visible;
            _profilePostsPage = 1;
            _posts.Clear();
            _profilePostsHasMore = true;
            ProfilePostsListView.ItemsSource = _posts;

            _user = await ApiService.Instance.GetUserProfileAsync(_username);
            ProfileProgressBar.Visibility = Visibility.Collapsed;

            if (_user == null)
            {
                var dialog = new MessageDialog("Failed to load user profile.");
                await dialog.ShowAsync();
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }

            ProfilePictureImage.Source = new BitmapImage(new Uri(_user.ProfilePictureUrl));
            DisplayNameTextBlock.Text = _user.Name;
            OnlineStatusTextBlock.Text = _user.Online ? "online" : "offline";
            BioTextBlock.Text = string.IsNullOrEmpty(_user.Bio) ? "no bio." : _user.Bio;

            if (_user.Stats != null)
            {
                PostsCountTextBlock.Text = _user.Stats.Posts.ToString();
                FollowersCountTextBlock.Text = _user.Stats.Followers.ToString();
                FollowingCountTextBlock.Text = _user.Stats.Following.ToString();
            }

            if (ApiService.Instance.IsLoggedIn)
            {
                if (_username.Equals(ApiService.Instance.CurrentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    FollowButton.Visibility = Visibility.Collapsed;
                    EditProfileButton.Visibility = Visibility.Visible;
                }
                else
                {
                    FollowButton.Visibility = Visibility.Visible;
                    EditProfileButton.Visibility = Visibility.Collapsed;
                    _isFollowing = await ApiService.Instance.GetFollowStatusAsync(_username, ApiService.Instance.CurrentUsername);
                    UpdateFollowButtonText();
                }
            }
            else
            {
                FollowButton.Visibility = Visibility.Visible;
                EditProfileButton.Visibility = Visibility.Collapsed;
                FollowButton.Content = "follow";
            }

            await LoadUserPostsAsync();
        }

        private async Task LoadUserPostsAsync()
        {
            ProfileProgressBar.Visibility = Visibility.Visible;
            LoadMoreProfilePostsButton.IsEnabled = false;

            var result = await ApiService.Instance.GetUserPostsAsync(_username, _profilePostsPage);
            ProfileProgressBar.Visibility = Visibility.Collapsed;

            if (result != null && result.Posts != null)
            {
                foreach (var post in result.Posts)
                {
                    _posts.Add(post);
                }

                _profilePostsHasMore = result.Posts.Count >= 20;
                LoadMoreProfilePostsButton.Visibility = _profilePostsHasMore ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _profilePostsHasMore = false;
                LoadMoreProfilePostsButton.Visibility = Visibility.Collapsed;
            }

            LoadMoreProfilePostsButton.IsEnabled = true;
        }

        private void UpdateFollowButtonText()
        {
            FollowButton.Content = _isFollowing ? "unfollow" : "follow";
        }

        // PIVOT SELECTION CHANGED
        private async void ProfilePivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfilePivot.SelectedIndex == 1 && !_isWallLoaded)
            {
                await RefreshWallCommentsAsync();
            }
            else if (ProfilePivot.SelectedIndex == 2 && !_isFollowingLoaded)
            {
                await RefreshFollowingAsync();
            }
            else if (ProfilePivot.SelectedIndex == 3 && !_isFollowersLoaded)
            {
                await RefreshFollowersAsync();
            }
        }

        // --- WALL COMMENTS TAB LOGIC ---
        private async Task RefreshWallCommentsAsync()
        {
            _wallPage = 1;
            _wallComments.Clear();
            _wallHasMore = true;
            WallCommentsListView.ItemsSource = _wallComments;
            await LoadWallCommentsAsync();
            _isWallLoaded = true;
        }

        private async Task LoadWallCommentsAsync()
        {
            WallProgressBar.Visibility = Visibility.Visible;
            LoadMoreWallCommentsButton.IsEnabled = false;

            var response = await ApiService.Instance.GetWallCommentsAsync(_username, _wallPage);
            WallProgressBar.Visibility = Visibility.Collapsed;

            if (response != null && response.Comments != null)
            {
                foreach (var comment in response.Comments)
                {
                    comment.Level = 0;
                    _wallComments.Add(comment);
                    var task = FetchAndAppendRepliesBackgroundAsync(comment, 1);
                }

                _wallHasMore = response.Comments.Count >= 20;
                LoadMoreWallCommentsButton.Visibility = _wallHasMore ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _wallHasMore = false;
                LoadMoreWallCommentsButton.Visibility = Visibility.Collapsed;
            }

            LoadMoreWallCommentsButton.IsEnabled = true;
        }

        private async Task FetchAndAppendRepliesBackgroundAsync(Comment parentComment, int level)
        {
            try
            {
                if (parentComment.HasReplies)
                {
                    int page = 1;
                    bool hasMore = true;
                    while (hasMore)
                    {
                        var response = await ApiService.Instance.GetCommentRepliesAsync(parentComment.Id, page);
                        if (response != null && response.Comments != null && response.Comments.Count > 0)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                foreach (var reply in response.Comments)
                                {
                                    reply.Level = level;
                                    
                                    int parentIndex = _wallComments.IndexOf(parentComment);
                                    if (parentIndex != -1)
                                    {
                                        int targetIndex = parentIndex + 1;
                                        while (targetIndex < _wallComments.Count && _wallComments[targetIndex].Level > parentComment.Level)
                                        {
                                            targetIndex++;
                                        }
                                        if (targetIndex > _wallComments.Count) targetIndex = _wallComments.Count;
                                        _wallComments.Insert(targetIndex, reply);
                                    }

                                    var task = FetchAndAppendRepliesBackgroundAsync(reply, level + 1);
                                }
                            });

                            hasMore = response.Comments.Count >= 20;
                            page++;
                        }
                        else
                        {
                            hasMore = false;
                        }
                    }
                }
            }
            catch { }
        }

        private async void SendWallCommentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            string content = NewWallCommentTextBox.Text.Trim();
            if (string.IsNullOrEmpty(content)) return;

            SendWallCommentButton.IsEnabled = false;
            NewWallCommentTextBox.IsEnabled = false;

            var comment = await ApiService.Instance.CreateWallCommentAsync(_username, content, _replyParentId);
            if (comment != null)
            {
                NewWallCommentTextBox.Text = string.Empty;
                ClearReplyParent();
                await RefreshWallCommentsAsync();
            }
            else
            {
                var dialog = new MessageDialog("Failed to post wall comment.");
                await dialog.ShowAsync();
            }

            SendWallCommentButton.IsEnabled = true;
            NewWallCommentTextBox.IsEnabled = true;
        }

        private void WallCommentsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var comment = e.ClickedItem as Comment;
            if (comment != null)
            {
                _replyParentId = comment.Id;
                ReplyToTextBlock.Text = $"replying to @{comment.Poster.Name}";
                ReplyToIndicator.Visibility = Visibility.Visible;
                NewWallCommentTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void ReplyWallCommentButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var comment = button.DataContext as Comment;
            if (comment != null)
            {
                _replyParentId = comment.Id;
                ReplyToTextBlock.Text = $"replying to @{comment.Poster.Name}";
                ReplyToIndicator.Visibility = Visibility.Visible;
                NewWallCommentTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void ReplyToIndicator_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ClearReplyParent();
        }

        private void ClearReplyParent()
        {
            _replyParentId = null;
            ReplyToIndicator.Visibility = Visibility.Collapsed;
        }

        private async void LoadMoreWallCommentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHasMore)
            {
                _wallPage++;
                await LoadWallCommentsAsync();
            }
        }

        // --- FOLLOWING TAB LOGIC ---
        private async Task RefreshFollowingAsync()
        {
            _followingPage = 1;
            _followingList.Clear();
            _followingHasMore = true;
            FollowingListView.ItemsSource = _followingList;
            await LoadFollowingAsync();
            _isFollowingLoaded = true;
        }

        private async Task LoadFollowingAsync()
        {
            FollowingProgressBar.Visibility = Visibility.Visible;
            LoadMoreFollowingButton.IsEnabled = false;

            var list = await ApiService.Instance.GetFollowingAsync(_username, _followingPage);
            FollowingProgressBar.Visibility = Visibility.Collapsed;

            if (list != null)
            {
                foreach (var user in list)
                {
                    _followingList.Add(user);
                }

                _followingHasMore = list.Count >= 20;
                LoadMoreFollowingButton.Visibility = _followingHasMore ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _followingHasMore = false;
                LoadMoreFollowingButton.Visibility = Visibility.Collapsed;
            }

            LoadMoreFollowingButton.IsEnabled = true;
        }

        private async void LoadMoreFollowingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_followingHasMore)
            {
                _followingPage++;
                await LoadFollowingAsync();
            }
        }

        // --- FOLLOWERS TAB LOGIC ---
        private async Task RefreshFollowersAsync()
        {
            _followersPage = 1;
            _followersList.Clear();
            _followersHasMore = true;
            FollowersListView.ItemsSource = _followersList;
            await LoadFollowersAsync();
            _isFollowersLoaded = true;
        }

        private async Task LoadFollowersAsync()
        {
            FollowersProgressBar.Visibility = Visibility.Visible;
            LoadMoreFollowersButton.IsEnabled = false;

            var list = await ApiService.Instance.GetFollowersAsync(_username, _followersPage);
            FollowersProgressBar.Visibility = Visibility.Collapsed;

            if (list != null)
            {
                foreach (var user in list)
                {
                    _followersList.Add(user);
                }

                _followersHasMore = list.Count >= 20;
                LoadMoreFollowersButton.Visibility = _followersHasMore ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _followersHasMore = false;
                LoadMoreFollowersButton.Visibility = Visibility.Collapsed;
            }

            LoadMoreFollowersButton.IsEnabled = true;
        }

        private async void LoadMoreFollowersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_followersHasMore)
            {
                _followersPage++;
                await LoadFollowersAsync();
            }
        }

        private void UserListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var user = e.ClickedItem as User;
            if (user != null)
            {
                Frame.Navigate(typeof(UserProfilePage), user.Name);
            }
        }

        // --- HEADER LOGIC ---
        private void PostsStat_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ProfilePivot.SelectedIndex = 0;
        }

        private void FollowersStat_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ProfilePivot.SelectedIndex = 3;
        }

        private void FollowingStat_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ProfilePivot.SelectedIndex = 2;
        }

        private void CommenterName_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var comment = element.DataContext as Comment;
                if (comment != null && comment.Poster != null)
                {
                    Frame.Navigate(typeof(UserProfilePage), comment.Poster.Name);
                }
            }
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
                
                FollowersCountTextBlock.Text = response.NewState.Followers.ToString();
            }
            FollowButton.IsEnabled = true;
        }

        private void ProfilePostsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as Post;
            if (post != null)
            {
                var targetPost = (post.IsEmptyRepost && post.Repost != null) ? post.Repost : post;
                Frame.Navigate(typeof(PostDetailsPage), targetPost.Id);
            }
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

        private async void RepostButton_Click(object sender, RoutedEventArgs e)
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

            var dialog = new MessageDialog("repost this post?", "repost");
            dialog.Commands.Add(new UICommand("repost", async (command) =>
            {
                button.IsEnabled = false;
                var result = await ApiService.Instance.RepostAsync(targetPost.Id);
                if (result != null)
                {
                    targetPost.RepostsCount++;
                    if (post.IsEmptyRepost)
                    {
                        post.RaisePropertyChanged("DisplayRepostsCount");
                    }
                }
                button.IsEnabled = true;
            }));
            dialog.Commands.Add(new UICommand("quote", (command) =>
            {
                Frame.Navigate(typeof(ComposePage), targetPost.Id);
            }));
            dialog.Commands.Add(new UICommand("cancel"));

            await dialog.ShowAsync();
        }

        private async void LoadMoreProfilePostsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_profilePostsHasMore)
            {
                _profilePostsPage++;
                await LoadUserPostsAsync();
            }
        }

        private async void EditProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var textBox = new TextBox { Text = string.IsNullOrEmpty(_user.Bio) || _user.Bio == "no bio." ? "" : _user.Bio, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 120 };
            var dialog = new ContentDialog
            {
                Title = "edit profile bio",
                Content = textBox,
                PrimaryButtonText = "save",
                SecondaryButtonText = "cancel"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newBio = textBox.Text.Trim();
                bool success = await ApiService.Instance.UpdateBioAsync(newBio);
                if (success)
                {
                    _user.Bio = newBio;
                    BioTextBlock.Text = string.IsNullOrEmpty(newBio) ? "no bio." : newBio;
                }
                else
                {
                    var errorDialog = new MessageDialog("Failed to update profile bio.");
                    await errorDialog.ShowAsync();
                }
            }
        }

        // --- IMAGE HANDLERS ---
        private void PostImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                var bitmapImage = image.Source as BitmapImage;
                if (bitmapImage != null)
                {
                    string url = bitmapImage.UriSource != null ? bitmapImage.UriSource.ToString() : null;
                    if (!string.IsNullOrEmpty(url))
                    {
                        FullscreenImage.Source = new BitmapImage(new Uri(url));
                        FullscreenImageOverlay.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void CloseFullscreenImage_Click(object sender, RoutedEventArgs e)
        {
            FullscreenImageOverlay.Visibility = Visibility.Collapsed;
            FullscreenImage.Source = null;
        }

        private async void DownloadFullscreenImage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            button.IsEnabled = false;
            try
            {
                var bitmapImage = FullscreenImage.Source as BitmapImage;
                if (bitmapImage != null && bitmapImage.UriSource != null)
                {
                    string url = bitmapImage.UriSource.ToString();
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        var bytes = await client.GetByteArrayAsync(url);
                        string fileName = "wasteof_image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                        int lastSlash = url.LastIndexOf('/');
                        if (lastSlash != -1 && lastSlash < url.Length - 1)
                        {
                            string potentialName = url.Substring(lastSlash + 1);
                            int qIdx = potentialName.IndexOf('?');
                            if (qIdx != -1) potentialName = potentialName.Substring(0, qIdx);
                            if (potentialName.EndsWith(".jpg") || potentialName.EndsWith(".jpeg") || potentialName.EndsWith(".png") || potentialName.EndsWith(".gif"))
                            {
                                fileName = potentialName;
                            }
                        }

                        var file = await Windows.Storage.KnownFolders.PicturesLibrary.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);

                        var dialog = new MessageDialog($"Image saved to Pictures Library as {file.Name}");
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("Failed to download image: " + ex.Message);
                await dialog.ShowAsync();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void Image_ImageOpened(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                var parent = image.Parent as Grid;
                if (parent != null)
                {
                    var progress = parent.FindName("ImageProgress") as ProgressRing;
                    if (progress != null)
                    {
                        progress.IsActive = false;
                        progress.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                var parent = image.Parent as Grid;
                if (parent != null)
                {
                    var progress = parent.FindName("ImageProgress") as ProgressRing;
                    if (progress != null)
                    {
                        progress.IsActive = false;
                        progress.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer) return depObj as ScrollViewer;

            for (int i = 0; i < Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null)
            {
                var scrollViewer = GetScrollViewer(listView);
                if (scrollViewer != null)
                {
                    scrollViewer.ViewChanged += async (s, args) =>
                    {
                        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 200)
                        {
                            await TriggerLoadMoreAsync(listView);
                        }
                    };
                }
            }
        }

        private async System.Threading.Tasks.Task TriggerLoadMoreAsync(ListView listView)
        {
            if (listView == ProfilePostsListView)
            {
                if (_profilePostsHasMore && LoadMoreProfilePostsButton.IsEnabled)
                {
                    _profilePostsPage++;
                    await LoadUserPostsAsync();
                }
            }
            else if (listView == WallCommentsListView)
            {
                if (_wallHasMore && LoadMoreWallCommentsButton.IsEnabled)
                {
                    _wallPage++;
                    await LoadWallCommentsAsync();
                }
            }
            else if (listView == FollowingListView)
            {
                if (_followingHasMore && LoadMoreFollowingButton.IsEnabled)
                {
                    _followingPage++;
                    await LoadFollowingAsync();
                }
            }
            else if (listView == FollowersListView)
            {
                if (_followersHasMore && LoadMoreFollowersButton.IsEnabled)
                {
                    _followersPage++;
                    await LoadFollowersAsync();
                }
            }
        }

        private void FlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var flipView = sender as FlipView;
            if (flipView != null)
            {
                var parentGrid = flipView.Parent as Grid;
                if (parentGrid != null)
                {
                    var indicatorText = parentGrid.FindName("ImageIndicatorTextBlock") as TextBlock;
                    var list = flipView.ItemsSource as System.Collections.IList;
                    if (indicatorText != null && list != null && list.Count > 0)
                    {
                        indicatorText.Text = $"{flipView.SelectedIndex + 1}/{list.Count}";
                    }
                }
            }
        }

    }
}

