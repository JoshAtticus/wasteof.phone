using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using wasteof.phone.Models;

namespace wasteof.phone.Services
{
    public class ApiService
    {
        private static ApiService _instance;
        public static ApiService Instance => _instance ?? (_instance = new ApiService());

        private const string BaseUrl = "https://api.wasteof.money/";
        private readonly HttpClient _httpClient;

        public string Token { get; private set; }
        public string CurrentUsername { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        private ApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            LoadSession();
        }

        private void LoadSession()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("token") && localSettings.Values.ContainsKey("username"))
            {
                Token = localSettings.Values["token"] as string;
                var savedUsername = localSettings.Values["username"] as string;
                CurrentUsername = savedUsername != null ? savedUsername.ToLowerInvariant() : null;
                
                if (!string.IsNullOrEmpty(Token))
                {
                    _httpClient.DefaultRequestHeaders.Remove("authorization");
                    _httpClient.DefaultRequestHeaders.Add("authorization", Token);
                }
            }
        }

        public void SaveSession(string token, string username)
        {
            Token = token;
            CurrentUsername = username != null ? username.ToLowerInvariant() : null;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["token"] = token;
            localSettings.Values["username"] = CurrentUsername;

            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(CurrentUsername))
            {
                SaveAccount(CurrentUsername, token);
            }

            _httpClient.DefaultRequestHeaders.Remove("authorization");
            if (!string.IsNullOrEmpty(Token))
            {
                _httpClient.DefaultRequestHeaders.Add("authorization", Token);
            }
        }

        public void ClearSession()
        {
            Token = null;
            CurrentUsername = null;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values.Remove("token");
            localSettings.Values.Remove("username");
            localSettings.Values.Remove("sessions");

            _httpClient.DefaultRequestHeaders.Remove("authorization");
        }

        public List<SavedAccount> GetSavedAccounts()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("sessions"))
            {
                try
                {
                    var json = localSettings.Values["sessions"] as string;
                    if (!string.IsNullOrEmpty(json))
                    {
                        return JsonConvert.DeserializeObject<List<SavedAccount>>(json);
                    }
                }
                catch { }
            }
            
            var list = new List<SavedAccount>();
            if (IsLoggedIn && !string.IsNullOrEmpty(CurrentUsername))
            {
                list.Add(new SavedAccount { Username = CurrentUsername, Token = Token });
            }
            return list;
        }

        private void SaveAccount(string username, string token)
        {
            var accounts = GetSavedAccounts();
            accounts.RemoveAll(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            accounts.Add(new SavedAccount { Username = username.ToLowerInvariant(), Token = token });
            
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["sessions"] = JsonConvert.SerializeObject(accounts);
        }

        public void SwitchAccount(string username)
        {
            var accounts = GetSavedAccounts();
            var target = accounts.Find(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                SaveSession(target.Token, target.Username);
            }
        }

        public void RemoveAccount(string username)
        {
            var accounts = GetSavedAccounts();
            accounts.RemoveAll(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["sessions"] = JsonConvert.SerializeObject(accounts);
            
            if (CurrentUsername != null && CurrentUsername.Equals(username, StringComparison.OrdinalIgnoreCase))
            {
                if (accounts.Count > 0)
                {
                    SaveSession(accounts[0].Token, accounts[0].Username);
                }
                else
                {
                    ClearSession();
                }
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var requestBody = new LoginRequest { Username = username, Password = password };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("session", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var loginResult = JsonConvert.DeserializeObject<LoginResponse>(responseJson);
                    
                    if (loginResult != null && !string.IsNullOrEmpty(loginResult.Token))
                    {
                        SaveSession(loginResult.Token, username);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // In production, log or bubble up
            }
            return false;
        }

        public async Task<User> GetUserProfileAsync(string username)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<User>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<FeedResponse> GetFeedAsync(string username, int page = 1)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username}/following/posts?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<FeedResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<FeedResponse> GetUserPostsAsync(string username, int page = 1)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username}/posts?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<FeedResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<FeedResponse> GetExplorePostsAsync(int page = 1)
        {
            try
            {
                var response = await _httpClient.GetAsync($"explore/posts/trending?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<FeedResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Post> GetPostDetailsAsync(string postId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"posts/{postId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Post>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Post> CreatePostAsync(string contentText, string repostId = null)
        {
            try
            {
                var requestBody = new CreatePostRequest { PostContent = contentText, RepostId = repostId };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("posts", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Post>(responseJson);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Post> EditPostAsync(string postId, string newContent)
        {
            try
            {
                var requestBody = new Dictionary<string, string> { { "post", newContent } };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"posts/{postId}", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Post>(responseJson);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<bool> UpdateBioAsync(string bio)
        {
            try
            {
                var requestBody = new Dictionary<string, string> { { "bio", bio } };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"users/{CurrentUsername}/bio", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception) { }
            return false;
        }

        public async Task<CommentResponse> GetWallCommentsAsync(string username, int page = 1)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username.ToLowerInvariant()}/wall?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<CommentResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Comment> CreateWallCommentAsync(string username, string contentText, string parentCommentId = null)
        {
            try
            {
                var requestBody = new CreateCommentRequest { Content = contentText, Parent = parentCommentId };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"users/{username.ToLowerInvariant()}/wall", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Comment>(responseJson);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Post> RepostAsync(string postId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"posts/{postId}/reposts", new StringContent("", Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Post>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<bool> DeletePostAsync(string postId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"posts/{postId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception) { }
            return false;
        }

        public async Task<LoveToggleResponse> ToggleLoveAsync(string postId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"posts/{postId}/loves", new StringContent("", Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<LoveToggleResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<bool> GetPostLoveStatusAsync(string postId, string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username)) return false;
                
                var response = await _httpClient.GetAsync($"posts/{postId}/loves/{username.ToLowerInvariant()}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(content)) return false;
                    
                    content = content.Trim();
                    
                    // Direct raw JSON boolean checks
                    if (content.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (content.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // Fallback resilient parse as dictionary (e.g. if API shifts to {"loved": true})
                    try
                    {
                        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                        if (obj != null)
                        {
                            var keys = new[] { "loved", "isLoving", "lovedByMe" };
                            foreach (var key in keys)
                            {
                                object val;
                                if (obj.TryGetValue(key, out val))
                                {
                                    if (val is bool)
                                    {
                                        return (bool)val;
                                    }
                                    if (val != null)
                                    {
                                        bool bParsed;
                                        if (bool.TryParse(val.ToString(), out bParsed))
                                        {
                                            return bParsed;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception) { }
            return false;
        }

        public async Task<FollowToggleResponse> ToggleFollowAsync(string username)
        {
            try
            {
                var response = await _httpClient.PostAsync($"users/{username}/followers", new StringContent("", Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<FollowToggleResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<bool> GetFollowStatusAsync(string username, string follower)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username}/followers/{follower}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<bool>(json);
                }
            }
            catch (Exception) { }
            return false;
        }

        public async Task<List<Notification>> GetNotificationsAsync(bool unreadOnly, int page = 1)
        {
            try
            {
                string endpoint = unreadOnly ? "messages/unread" : "messages/read";
                var response = await _httpClient.GetAsync($"{endpoint}?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var notifResponse = JsonConvert.DeserializeObject<NotificationResponse>(json);
                    return unreadOnly ? notifResponse?.Unread : notifResponse?.Read;
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<bool> MarkNotificationsReadAsync(List<string> ids)
        {
            try
            {
                var requestBody = new Dictionary<string, List<string>> { { "messages", ids } };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("messages/mark/read", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception) { }
            return false;
        }

        public async Task<CommentResponse> GetCommentsAsync(string postId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"posts/{postId}/comments");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<CommentResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<CommentResponse> GetCommentRepliesAsync(string commentId, int page = 1)
        {
            try
            {
                var response = await _httpClient.GetAsync($"comments/{commentId}/replies?page={page}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<CommentResponse>(json);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<Comment> CreateCommentAsync(string postId, string contentText, string parentCommentId = null)
        {
            try
            {
                var requestBody = new CreateCommentRequest { Content = contentText, Parent = parentCommentId };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"posts/{postId}/comments", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Comment>(responseJson);
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<List<User>> GetFollowersAsync(string username)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username.ToLowerInvariant()}/followers");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var wrapper = JsonConvert.DeserializeObject<FollowersWrapper>(json);
                    return wrapper?.Followers;
                }
            }
            catch (Exception) { }
            return null;
        }

        public async Task<List<User>> GetFollowingAsync(string username)
        {
            try
            {
                var response = await _httpClient.GetAsync($"users/{username.ToLowerInvariant()}/following");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var wrapper = JsonConvert.DeserializeObject<FollowingWrapper>(json);
                    return wrapper?.Following;
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
