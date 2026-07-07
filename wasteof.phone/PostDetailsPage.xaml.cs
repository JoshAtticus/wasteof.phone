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

        public PostDetailsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var postId = e.Parameter as string;
            var post = e.Parameter as Post;
            if (postId != null)
            {
                _postId = postId;
                await RefreshPostAndCommentsAsync();
            }
            else if (post != null)
            {
                _post = post;
                _postId = post.Id;
                DisplayPost();
                if (ApiService.Instance.IsLoggedIn && _post.IsLoving == null)
                {
                    _post.IsLoving = await ApiService.Instance.GetPostLoveStatusAsync(_postId, ApiService.Instance.CurrentUsername);
                    UpdateLoveButtonState();
                }
                await LoadCommentsAsync();
            }
        }

        private async System.Threading.Tasks.Task RefreshPostAndCommentsAsync()
        {
            _post = await ApiService.Instance.GetPostDetailsAsync(_postId);
            if (_post == null)
            {
                var dialog = new MessageDialog("Failed to load post details.");
                await dialog.ShowAsync();
                if (Frame.CanGoBack) Frame.GoBack();
                return;
            }

            if (ApiService.Instance.IsLoggedIn)
            {
                _post.IsLoving = await ApiService.Instance.GetPostLoveStatusAsync(_postId, ApiService.Instance.CurrentUsername);
            }

            DisplayPost();
            await LoadCommentsAsync();
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
            PostContentTextBlock.Text = _post.CleanContent;

            // Display Repost if available
            if (_post.Repost != null)
            {
                RepostPosterName.Text = _post.Repost.Poster.Name;
                RepostContent.Text = _post.Repost.CleanContent;
                RepostBorder.Visibility = Visibility.Visible;
            }
            else
            {
                RepostBorder.Visibility = Visibility.Collapsed;
            }

            // Style love button
            UpdateLoveButtonState();

            // Show delete button if current user is owner
            if (ApiService.Instance.IsLoggedIn && 
                _post.Poster.Name.Equals(ApiService.Instance.CurrentUsername, StringComparison.OrdinalIgnoreCase))
            {
                DeleteButton.Visibility = Visibility.Visible;
            }
            else
            {
                DeleteButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateLoveButtonState()
        {
            if (_post == null) return;
            bool isLoving = _post.IsLoving ?? false;
            LoveIcon.Fill = new SolidColorBrush(isLoving ? Windows.UI.Colors.Red : Windows.UI.Colors.Transparent);
            LoveIcon.Stroke = new SolidColorBrush(isLoving ? Windows.UI.Colors.Red : Windows.UI.Color.FromArgb(255, 136, 136, 136));
            LoveCountTextBlock.Text = _post.LovesCount.ToString();
            RepostCountTextBlock.Text = _post.RepostsCount.ToString();
        }

        private async System.Threading.Tasks.Task LoadCommentsAsync()
        {
            var response = await ApiService.Instance.GetCommentsAsync(_postId);
            _comments.Clear();
            CommentsListView.ItemsSource = _comments;

            if (response != null && response.Comments != null)
            {
                foreach (var comment in response.Comments)
                {
                    comment.Level = 0;
                    _comments.Add(comment);

                    // Fetch replies dynamically in the background
                    var task = FetchAndAppendRepliesBackgroundAsync(comment, 1);
                }
            }
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
                                    reply.Level = level;

                                    // Find parent index dynamically in case elements shifted
                                    int parentIndex = _comments.IndexOf(parentComment);
                                    if (parentIndex >= 0)
                                    {
                                        int targetIndex = parentIndex + insertOffset;
                                        if (targetIndex > _comments.Count) targetIndex = _comments.Count;
                                        _comments.Insert(targetIndex, reply);
                                        insertOffset++;
                                    }

                                    // Recursively fetch replies for this nested comment reply
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
                await LoadCommentsAsync();
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
    }
}
