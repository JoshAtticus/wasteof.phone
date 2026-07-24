using System;
using System.Collections.Generic;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using wasteof.phone.Services;
using wasteof.phone.Models;
using Windows.UI.Xaml.Shapes;

namespace wasteof.phone
{
    public sealed partial class PostDetailsPage : Page
    {
        private string _postId;
        private Post _post;
        private System.Collections.ObjectModel.ObservableCollection<Comment> _comments = new System.Collections.ObjectModel.ObservableCollection<Comment>();
        private string _replyParentId = null;
        private bool _isLoveStatusLoaded = false;
        private int _commentsPage = 1;
        private bool _commentsHasMore = true;
        private bool _isLoadingComments = false;

        public PostDetailsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var postId = e.Parameter as string;
            var post = e.Parameter as Post;

            if (post != null && post.IsEmptyRepost && post.Repost != null)
            {
                post = post.Repost;
            }

            if (postId != null)
            {
                _postId = postId;
                await RefreshPostAndCommentsAsync();
            }
            else if (post != null)
            {
                _post = post;
                _postId = post.Id;
                _isLoveStatusLoaded = false;
                DisplayPost();
                if (ApiService.Instance.IsLoggedIn)
                {
                    _post.IsLoving = await ApiService.Instance.GetPostLoveStatusAsync(_postId, ApiService.Instance.CurrentUsername);
                }
                _isLoveStatusLoaded = true;
                UpdateLoveButtonState();
                await LoadCommentsAsync(true);
            }
        }

        private async System.Threading.Tasks.Task RefreshPostAndCommentsAsync()
        {
            _isLoveStatusLoaded = false;
            _post = await ApiService.Instance.GetPostDetailsAsync(_postId);
            if (_post == null)
            {
                var dialog = new MessageDialog("Failed to load post details.");
                await dialog.ShowAsync();
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }

            if (_post.IsEmptyRepost && _post.Repost != null)
            {
                _post = _post.Repost;
                _postId = _post.Id;
            }

            DisplayPost();

            if (ApiService.Instance.IsLoggedIn)
            {
                _post.IsLoving = await ApiService.Instance.GetPostLoveStatusAsync(_postId, ApiService.Instance.CurrentUsername);
            }
            _isLoveStatusLoaded = true;
            UpdateLoveButtonState();
            await LoadCommentsAsync(true);
        }

        private void DisplayPost()
        {
            if (_post == null) return;

            if (_post.Poster != null)
            {
                PostProfilePictureImage.Source = new BitmapImage(new Uri(_post.Poster.ProfilePictureUrl));
            }

            PosterNameTextBlock.Text = _post.Poster.Name;
            PostTimeTextBlock.Text = _post.FormattedTime;
            Helpers.HtmlHelper.SetHtml(PostContentTextBlock, _post.Content);

            var urls = _post.ImageUrls;
            if (urls != null && urls.Count == 1)
            {
                PostSingleImage.Source = new BitmapImage(new Uri(urls[0]));
                PostSingleImageGrid.Visibility = Visibility.Visible;
                PostImagesGrid.Visibility = Visibility.Collapsed;
            }
            else if (urls != null && urls.Count > 1)
            {
                PostImagesFlipView.ItemsSource = urls;
                PostImagesGrid.Visibility = Visibility.Visible;
                PostSingleImageGrid.Visibility = Visibility.Collapsed;
                PostImageIndicatorTextBlock.Text = $"1/{urls.Count}";
            }
            else
            {
                PostSingleImageGrid.Visibility = Visibility.Collapsed;
                PostImagesGrid.Visibility = Visibility.Collapsed;
            }


            if (_post.Repost != null)
            {
                RepostPosterName.Text = _post.Repost.Poster.Name;
                Helpers.HtmlHelper.SetHtml(RepostContent, _post.Repost.Content);
                RepostBorder.Visibility = Visibility.Visible;
            }
            else
            {
                RepostBorder.Visibility = Visibility.Collapsed;
            }

            UpdateLoveButtonState();

            if (ApiService.Instance.IsLoggedIn && 
                _post.Poster.Name.Equals(ApiService.Instance.CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                DeleteButton.Visibility = Visibility.Visible;
                EditButton.Visibility = Visibility.Visible;
            }
            else
            {
                DeleteButton.Visibility = Visibility.Collapsed;
                EditButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateLoveButtonState()
        {
            if (_post == null) return;
            bool isLoving = _isLoveStatusLoaded ? (_post.IsLoving ?? false) : false;
            LoveIcon.Fill = new SolidColorBrush(isLoving ? Windows.UI.Colors.Red : Windows.UI.Colors.Transparent);
            LoveIcon.Stroke = new SolidColorBrush(isLoving ? Windows.UI.Colors.Red : Windows.UI.Color.FromArgb(255, 136, 136, 136));
            LoveCountTextBlock.Text = _post.LovesCount.ToString();
            RepostCountTextBlock.Text = _post.RepostsCount.ToString();
        }

        private async System.Threading.Tasks.Task LoadCommentsAsync(bool refresh = false)
        {
            if (_isLoadingComments) return;
            _isLoadingComments = true;

            if (refresh)
            {
                _commentsPage = 1;
                _commentsHasMore = true;
                _comments.Clear();
                CommentsListView.ItemsSource = _comments;
            }

            var response = await ApiService.Instance.GetCommentsAsync(_postId, _commentsPage);

            if (response != null && response.Comments != null)
            {
                foreach (var comment in response.Comments)
                {
                    if (!System.Linq.Enumerable.Any(_comments, c => c.Id == comment.Id))
                    {
                        comment.Level = 0;
                        _comments.Add(comment);
                        var task = FetchAndAppendRepliesBackgroundAsync(comment, 1);
                    }
                }

                _commentsHasMore = response.Comments.Count >= 20;
            }

            else
            {
                _commentsHasMore = false;
            }

            _isLoadingComments = false;
        }

        private async System.Threading.Tasks.Task FetchAndAppendRepliesBackgroundAsync(Comment parentComment, int level)
        {
            try
            {
                if (parentComment.HasReplies)
                {
                    int page = 1;
                    bool hasMore = true;
                    int insertOffset = 1;

                    while (hasMore)
                    {
                        var response = await ApiService.Instance.GetCommentRepliesAsync(parentComment.Id, page);
                        if (response != null && response.Comments != null && response.Comments.Count > 0)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                foreach (var reply in response.Comments)
                                {
                                    if (!System.Linq.Enumerable.Any(_comments, c => c.Id == reply.Id))
                                    {
                                        reply.Level = level;

                                        int parentIndex = _comments.IndexOf(parentComment);
                                        if (parentIndex >= 0)
                                        {
                                            int targetIndex = parentIndex + insertOffset;
                                            if (targetIndex > _comments.Count) targetIndex = _comments.Count;
                                            _comments.Insert(targetIndex, reply);
                                            insertOffset++;
                                        }

                                        var task = FetchAndAppendRepliesBackgroundAsync(reply, level + 1);
                                    }
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
            catch (Exception)
            {
            }
        }

        private async void LoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            LoveButton.IsEnabled = false;
            var response = await ApiService.Instance.ToggleLoveAsync(_postId);
            if (response != null && response.NewState != null)
            {
                _post.IsLoving = response.NewState.IsLoving;
                _post.LovesCount = response.NewState.Loves;
                UpdateLoveButtonState();
            }
            LoveButton.IsEnabled = true;
        }

        private void RepostButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            Frame.Navigate(typeof(ComposePage), _postId);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new MessageDialog("Are you sure you want to delete this post?", "Delete Post");
            confirmDialog.Commands.Add(new UICommand("Yes") { Id = 0 });
            confirmDialog.Commands.Add(new UICommand("No") { Id = 1 });
            confirmDialog.DefaultCommandIndex = 1;
            confirmDialog.CancelCommandIndex = 1;

            var result = await confirmDialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                DeleteButton.IsEnabled = false;
                bool deleted = await ApiService.Instance.DeletePostAsync(_postId);
                if (deleted)
                {
                    if (Frame.CanGoBack) Frame.GoBack();
                }
                else
                {
                    var errorDialog = new MessageDialog("Failed to delete post.");
                    await errorDialog.ShowAsync();
                    DeleteButton.IsEnabled = true;
                }
            }
        }

        private async void SendCommentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiService.Instance.IsLoggedIn)
            {
                Frame.Navigate(typeof(LoginPage));
                return;
            }

            string content = NewCommentTextBox.Text.Trim();
            if (string.IsNullOrEmpty(content)) return;

            SendCommentButton.IsEnabled = false;
            NewCommentTextBox.IsEnabled = false;

            var comment = await ApiService.Instance.CreateCommentAsync(_postId, content, _replyParentId);
            if (comment != null)
            {
                NewCommentTextBox.Text = string.Empty;
                ClearReplyParent();
                await RefreshPostAndCommentsAsync();
            }

            else
            {
                var dialog = new MessageDialog("Failed to post comment.");
                await dialog.ShowAsync();
            }

            SendCommentButton.IsEnabled = true;
            NewCommentTextBox.IsEnabled = true;
        }

        private void PosterNameTextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_post != null)
            {
                Frame.Navigate(typeof(UserProfilePage), _post.Poster.Name);
            }
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

        private void CommentsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var comment = e.ClickedItem as Comment;
            if (comment != null)
            {
                _replyParentId = comment.Id;
                ReplyToTextBlock.Text = $"replying to @{comment.Poster.Name}";
                ReplyToIndicator.Visibility = Visibility.Visible;
                NewCommentTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void ReplyCommentButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var comment = button.DataContext as Comment;
            if (comment != null)
            {
                _replyParentId = comment.Id;
                ReplyToTextBlock.Text = $"replying to @{comment.Poster.Name}";
                ReplyToIndicator.Visibility = Visibility.Visible;
                NewCommentTextBox.Focus(FocusState.Programmatic);
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

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_post != null)
            {
                Frame.Navigate(typeof(ComposePage), "edit:" + _post.Id);
            }
        }

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
            if (listView == CommentsListView)
            {
                if (_commentsHasMore && !_isLoadingComments)
                {
                    _commentsPage++;
                    await LoadCommentsAsync();
                }
            }
        }

        private void PostImagesFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_post != null && _post.ImageUrls != null && _post.ImageUrls.Count > 1)
            {
                PostImageIndicatorTextBlock.Text = $"{PostImagesFlipView.SelectedIndex + 1}/{_post.ImageUrls.Count}";
            }
        }
    }
}

