using System;
using System.Collections.Generic;
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
        private readonly List<string> _uploadedImageUrls = new List<string>();

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

            _uploadedImageUrls.Clear();
            UpdateImagesPreview();
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

            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(_repostId))
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
                            UpdateImagesPreview();
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
            }
        }

        private void UpdateImagesPreview()
        {
            ImagesPreviewStackPanel.Children.Clear();
            for (int index = 0; index < _uploadedImageUrls.Count; index++)
            {
                var url = _uploadedImageUrls[index];
                var itemUI = CreateImagePreviewItem(url, index);
                ImagesPreviewStackPanel.Children.Add(itemUI);
            }
        }

        private Grid CreateImagePreviewItem(string url, int index)
        {
            var itemGrid = new Grid
            {
                Width = 90,
                Height = 90,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Windows.UI.Colors.Transparent)
            };

            // Main preview image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(url)),
                Width = 90,
                Height = 90,
                Stretch = Stretch.UniformToFill
            };
            itemGrid.Children.Add(image);

            // Overlay toolbar for sorting/deleting
            var overlay = new Grid
            {
                Height = 28,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 0, 0, 0))
            };
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlay.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Move left
            var btnLeft = new Button
            {
                Content = "←",
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Windows.UI.Colors.Transparent),
                FontSize = 12,
                IsEnabled = index > 0
            };
            btnLeft.Click += (s, e) => { MoveImage(index, index - 1); };
            Grid.SetColumn(btnLeft, 0);
            overlay.Children.Add(btnLeft);

            // Remove
            var btnDelete = new Button
            {
                Content = "✕",
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Windows.UI.Colors.Transparent),
                Foreground = new SolidColorBrush(Windows.UI.Colors.Red),
                FontSize = 12
            };
            btnDelete.Click += (s, e) => { RemoveImage(index); };
            Grid.SetColumn(btnDelete, 1);
            overlay.Children.Add(btnDelete);

            // Move right
            var btnRight = new Button
            {
                Content = "→",
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Windows.UI.Colors.Transparent),
                FontSize = 12,
                IsEnabled = index < _uploadedImageUrls.Count - 1
            };
            btnRight.Click += (s, e) => { MoveImage(index, index + 1); };
            Grid.SetColumn(btnRight, 2);
            overlay.Children.Add(btnRight);

            itemGrid.Children.Add(overlay);
            return itemGrid;
        }

        private void MoveImage(int oldIndex, int newIndex)
        {
            if (newIndex < 0 || newIndex >= _uploadedImageUrls.Count) return;
            var temp = _uploadedImageUrls[oldIndex];
            _uploadedImageUrls.RemoveAt(oldIndex);
            _uploadedImageUrls.Insert(newIndex, temp);
            UpdateImagesPreview();
        }

        private void RemoveImage(int index)
        {
            if (index < 0 || index >= _uploadedImageUrls.Count) return;
            _uploadedImageUrls.RemoveAt(index);
            UpdateImagesPreview();
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
    }
}
