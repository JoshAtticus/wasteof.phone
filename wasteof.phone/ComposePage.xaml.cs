using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using wasteof.phone.Services;
using wasteof.phone.Models;

namespace wasteof.phone
{
    public sealed partial class ComposePage : Page
    {
        private string _repostId = null;
        private string _editPostId = null;
        private readonly ObservableCollection<string> _uploadedImageUrls = new ObservableCollection<string>();

        public ComposePage()
        {
            this.InitializeComponent();
            ImagesGridView.ItemsSource = _uploadedImageUrls;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var targetId = e.Parameter as string;
            _editPostId = null;
            _repostId = null;

            if (targetId != null)
            {
                if (targetId.StartsWith("edit:"))
                {
                    _editPostId = targetId.Substring(5);
                    PageHeaderTitle.Text = "edit post";
                    RepostIndicator.Visibility = Visibility.Collapsed;
                    PostContentTextBox.PlaceholderText = "edit your post...";

                    LoadPostForEditingAsync(_editPostId);
                }
                else
                {
                    _repostId = targetId;
                    PageHeaderTitle.Text = "repost";
                    RepostIndicator.Text = $"reposting: {targetId}";
                    RepostIndicator.Visibility = Visibility.Visible;
                    PostContentTextBox.PlaceholderText = "add a comment to this repost (optional)...";
                }
            }
            else
            {
                PageHeaderTitle.Text = "compose";
                RepostIndicator.Visibility = Visibility.Collapsed;
                PostContentTextBox.PlaceholderText = "what's on your mind?";
            }

            if (_editPostId == null)
            {
                _uploadedImageUrls.Clear();
            }
            PostContentTextBox.Focus(FocusState.Programmatic);
        }

        private void PostContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CharCountTextBlock.Text = $"{PostContentTextBox.Text.Length} / 5000";
        }

        private async void PostAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            string rawContent = PostContentTextBox.Text.Trim();

            // Build content with appended images (as HTML tags at the end of the post)
            var finalContentBuilder = new StringBuilder();
            finalContentBuilder.Append(rawContent);

            if (_uploadedImageUrls.Count > 0)
            {
                if (finalContentBuilder.Length > 0)
                {
                    // Add newline break before appending images
                    finalContentBuilder.Append("<br/>");
                }
                foreach (var url in _uploadedImageUrls)
                {
                    finalContentBuilder.AppendFormat("<img src=\"{0}\"></img>", url);
                }
            }

            string content = finalContentBuilder.ToString().Trim();

            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(_repostId) && string.IsNullOrEmpty(_editPostId))
            {
                var dialog = new MessageDialog("Post content cannot be empty.");
                await dialog.ShowAsync();
                return;
            }

            PostAppBarButton.IsEnabled = false;
            AddImageAppBarButton.IsEnabled = false;
            CancelAppBarButton.IsEnabled = false;
            PostContentTextBox.IsEnabled = false;

            Post createdPost = null;
            if (!string.IsNullOrEmpty(_editPostId))
            {
                createdPost = await ApiService.Instance.EditPostAsync(_editPostId, content);
            }
            else if (!string.IsNullOrEmpty(_repostId))
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
                AddImageAppBarButton.IsEnabled = true;
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

        private void AddImageAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".gif");

            picker.PickSingleFileAndContinue();
        }

        public async void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            if (args != null && args.Files != null && args.Files.Count > 0)
            {
                var file = args.Files[0];
                await UploadImageAsync(file);
            }
        }

        private async System.Threading.Tasks.Task UploadImageAsync(StorageFile file)
        {
            PostContentTextBox.IsEnabled = false;
            PostAppBarButton.IsEnabled = false;
            AddImageAppBarButton.IsEnabled = false;
            UploadProgressRing.IsActive = true;

            try
            {
                using (var client = new HttpClient())
                {
                    // Add authentication header as documented
                    client.DefaultRequestHeaders.Add("X-API-Key", "srv_jUI1POAyiPszn_wwyqtGkTAaNWjpzZfS24c4i67O7k4");

                    var formContent = new MultipartFormDataContent();

                    // Read local storage file as stream
                    var randomAccessStream = await file.OpenAsync(FileAccessMode.Read);
                    var stream = randomAccessStream.AsStreamForRead();
                    var streamContent = new StreamContent(stream);

                    // Map MIME-type according to extension
                    string ext = Path.GetExtension(file.Name).ToLower();
                    string mimeType = "image/jpeg";
                    if (ext == ".png") mimeType = "image/png";
                    else if (ext == ".gif") mimeType = "image/gif";

                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                    formContent.Add(streamContent, "file", file.Name);

                    // POST to the proxy endpoint
                    var response = await client.PostAsync("https://ibbwom.joshattic.us/api/upload", formContent);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var uploadResult = Newtonsoft.Json.JsonConvert.DeserializeObject<UploadResponse>(json);
                        
                        if (uploadResult != null && uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Url))
                        {
                            _uploadedImageUrls.Add(uploadResult.Url);
                        }
                        else
                        {
                            var dialog = new MessageDialog("Upload succeeded, but the server response was invalid.");
                            await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        var responseText = await response.Content.ReadAsStringAsync();
                        var dialog = new MessageDialog($"Upload failed with status {response.StatusCode}:\n{responseText}");
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog($"Error during image upload: {ex.Message}");
                await dialog.ShowAsync();
            }
            finally
            {
                PostContentTextBox.IsEnabled = true;
                PostAppBarButton.IsEnabled = true;
                AddImageAppBarButton.IsEnabled = true;
                UploadProgressRing.IsActive = false;
            }
        }

        private string _previewingImageUrl = null;

        private void PreviewGrid_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid != null)
            {
                var url = grid.DataContext as string;
                if (url != null)
                {
                    _previewingImageUrl = url;
                    LargePreviewImage.Source = new BitmapImage(new Uri(url));
                    ImagePreviewModal.Visibility = Visibility.Visible;
                }
            }
        }

        private void ClosePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            ImagePreviewModal.Visibility = Visibility.Collapsed;
            _previewingImageUrl = null;
        }

        private void DeletePreviewedImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_previewingImageUrl))
            {
                _uploadedImageUrls.Remove(_previewingImageUrl);
                _previewingImageUrl = null;
            }
            ImagePreviewModal.Visibility = Visibility.Collapsed;
        }

        private void ImagePreviewModal_BackdropTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (e.OriginalSource == ImagePreviewModal)
            {
                ImagePreviewModal.Visibility = Visibility.Collapsed;
                _previewingImageUrl = null;
            }
        }

        private void PreviewGrid_ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid == null) return;

            var transform = grid.RenderTransform as CompositeTransform;
            if (transform == null)
            {
                transform = new CompositeTransform();
                grid.RenderTransform = transform;
            }

            transform.TranslateX += e.Delta.Translation.X;
        }

        private void PreviewGrid_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid == null) return;

            var transform = grid.RenderTransform as CompositeTransform;
            if (transform != null)
            {
                double shift = transform.TranslateX;
                transform.TranslateX = 0; // Reset visual position instantly

                var url = grid.DataContext as string;
                if (url != null)
                {
                    int index = _uploadedImageUrls.IndexOf(url);
                    if (index != -1)
                    {
                        if (shift < -40) // Dragged left by more than 40px
                        {
                            int newIndex = index - 1;
                            if (newIndex >= 0)
                            {
                                _uploadedImageUrls.Move(index, newIndex);
                            }
                        }
                        else if (shift > 40) // Dragged right by more than 40px
                        {
                            int newIndex = index + 1;
                            if (newIndex < _uploadedImageUrls.Count)
                            {
                                _uploadedImageUrls.Move(index, newIndex);
                            }
                        }
                    }
                }
            }
        }

        private class UploadResponse
        {
            [Newtonsoft.Json.JsonProperty("success")]
            public bool Success { get; set; }

            [Newtonsoft.Json.JsonProperty("url")]
            public string Url { get; set; }

            [Newtonsoft.Json.JsonProperty("filename")]
            public string Filename { get; set; }
        }

        private async void LoadPostForEditingAsync(string postId)
        {
            PostAppBarButton.IsEnabled = false;
            UploadProgressRing.IsActive = true;
            try
            {
                var post = await ApiService.Instance.GetPostDetailsAsync(postId);
                if (post != null)
                {
                    _uploadedImageUrls.Clear();
                    var urls = post.ImageUrls;
                    foreach (var url in urls)
                    {
                        _uploadedImageUrls.Add(url);
                    }

                    string text = post.Content;
                    text = StripImageTags(text);
                    PostContentTextBox.Text = text.Trim();
                }
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("Failed to load post for editing: " + ex.Message);
                await dialog.ShowAsync();
            }
            finally
            {
                UploadProgressRing.IsActive = false;
                PostAppBarButton.IsEnabled = true;
            }
        }

        private string StripImageTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var text = html;
            while (true)
            {
                int start = text.IndexOf("<img");
                if (start == -1) break;
                int end = text.IndexOf(">", start);
                if (end == -1) break;

                int closeTag = text.IndexOf("</img>", end);
                if (closeTag != -1 && closeTag - end < 10)
                {
                    text = text.Remove(start, (closeTag + 6) - start);
                }
                else
                {
                    text = text.Remove(start, (end + 1) - start);
                }
            }

            text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
            text = text.Replace("<p>", "").Replace("</p>", "\n");
            
            // basic decode of XML entities
            text = text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
            
            return text;
        }
    }
}
