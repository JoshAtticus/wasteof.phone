using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Windows.UI.Xaml;

namespace wasteof.phone.Models
{
    public class User
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("stats")]
        public UserStats Stats { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }

        [JsonIgnore]
        public string ProfilePictureUrl => $"http://api.wasteof.money/users/{Name}/picture";

        [JsonIgnore]
        public string BannerUrl => $"http://api.wasteof.money/users/{Name}/banner";
    }

    public class UserStats
    {
        [JsonProperty("followers")]
        public int Followers { get; set; }

        [JsonProperty("following")]
        public int Following { get; set; }

        [JsonProperty("posts")]
        public int Posts { get; set; }
    }

    public class FeedResponse
    {
        [JsonProperty("posts")]
        public List<Post> Posts { get; set; }

        [JsonProperty("pinned")]
        public List<Post> Pinned { get; set; }

        [JsonProperty("last")]
        public bool Last { get; set; }
    }

    public class Poster
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonIgnore]
        public string ProfilePictureUrl => $"http://api.wasteof.money/users/{Name}/picture";
    }

    public class Post : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }

        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("poster")]
        public Poster Poster { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("repost")]
        public Post Repost { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("comments")]
        public int CommentsCount { get; set; }

        private int _lovesCount;
        [JsonProperty("loves")]
        public int LovesCount
        {
            get { return _lovesCount; }
            set 
            { 
                _lovesCount = value; 
                OnPropertyChanged("LovesCount"); 
                OnPropertyChanged("DisplayLovesCount"); 
            }
        }

        [JsonProperty("reposts")]
        public int RepostsCount { get; set; }

        [JsonProperty("pinned")]
        public bool? Pinned { get; set; }

        private bool? _isLoving;
        [JsonProperty("isLoving")]
        public bool? IsLoving
        {
            get { return _isLoving; }
            set 
            { 
                _isLoving = value; 
                OnPropertyChanged("IsLoving"); 
                OnPropertyChanged("LoveHeartColor"); 
                OnPropertyChanged("LoveHeartGlyph"); 
                OnPropertyChanged("LoveHeartFill"); 
                OnPropertyChanged("LoveHeartStroke"); 
                OnPropertyChanged("DisplayLoveHeartFill"); 
                OnPropertyChanged("DisplayLoveHeartStroke"); 
            }
        }

        public void RaisePropertyChanged(string name)
        {
            OnPropertyChanged(name);
        }

        // Helper properties for UI
        [JsonIgnore]
        public string LoveHeartColor => (IsLoving == true) ? "Red" : "#888888";
        [JsonIgnore]
        public string LoveHeartGlyph => (IsLoving == true) ? "\uE0A5" : "\uE006";
        [JsonIgnore]
        public string LoveHeartFill => (IsLoving == true) ? "Red" : "Transparent";
        [JsonIgnore]
        public string LoveHeartStroke => (IsLoving == true) ? "Red" : "#888888";

        // Image parsing helpers
        [JsonIgnore]
        public string FirstImageUrl
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return null;
                try
                {
                    int index = Content.IndexOf("<img");
                    if (index != -1)
                    {
                        int srcIndex = Content.IndexOf("src=", index);
                        if (srcIndex != -1)
                        {
                            char quoteChar = Content[srcIndex + 4];
                            if (quoteChar == '"' || quoteChar == '\'')
                            {
                                int start = srcIndex + 5;
                                int end = Content.IndexOf(quoteChar, start);
                                if (end != -1)
                                {
                                    return Content.Substring(start, end - start);
                                }
                            }
                        }
                    }
                }
                catch { }
                return null;
            }
        }
        
        [JsonIgnore]
        public List<string> ImageUrls
        {
            get
            {
                var urls = new List<string>();
                if (string.IsNullOrEmpty(Content)) return urls;
                try
                {
                    int index = 0;
                    while (true)
                    {
                        index = Content.IndexOf("<img", index);
                        if (index == -1) break;
                        
                        int srcIndex = Content.IndexOf("src=", index);
                        if (srcIndex != -1)
                        {
                            char quoteChar = Content[srcIndex + 4];
                            if (quoteChar == '"' || quoteChar == '\'')
                            {
                                int start = srcIndex + 5;
                                int end = Content.IndexOf(quoteChar, start);
                                if (end != -1)
                                {
                                    urls.Add(Content.Substring(start, end - start));
                                    index = end + 1;
                                    continue;
                                }
                            }
                        }
                        index += 4;
                    }
                }
                catch { }
                return urls;
            }
        }
        
        [JsonIgnore]
        public Visibility ImageVisibility => (ImageUrls.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

        // Empty Repost UI redirection helpers
        [JsonIgnore]
        public bool IsEmptyRepost => Repost != null && string.IsNullOrWhiteSpace(CleanContent);

        [JsonIgnore]
        public Poster DisplayPoster => IsEmptyRepost ? Repost.Poster : Poster;

        [JsonIgnore]
        public string DisplayTime => IsEmptyRepost ? Repost.FormattedTime : FormattedTime;

        [JsonIgnore]
        public string DisplayContent => IsEmptyRepost ? Repost.CleanContent : CleanContent;

        [JsonIgnore]
        public string DisplayHtml => IsEmptyRepost ? Repost.Content : Content;

        [JsonIgnore]
        public string DisplayFirstImageUrl => IsEmptyRepost ? Repost.FirstImageUrl : FirstImageUrl;

        [JsonIgnore]
        public List<string> DisplayImageUrls => IsEmptyRepost ? Repost.ImageUrls : ImageUrls;

        [JsonIgnore]
        public Visibility DisplayImageVisibility => IsEmptyRepost ? Repost.ImageVisibility : ImageVisibility;

        [JsonIgnore]
        public string RepostHeader => IsEmptyRepost ? $"@{Poster.Name} reposted" : null;

        [JsonIgnore]
        public Visibility RepostHeaderVisibility => IsEmptyRepost ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore]
        public Visibility NormalRepostBorderVisibility => (Repost != null && !IsEmptyRepost) ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore]
        public int DisplayLovesCount => IsEmptyRepost ? Repost.LovesCount : LovesCount;

        [JsonIgnore]
        public int DisplayCommentsCount => IsEmptyRepost ? Repost.CommentsCount : CommentsCount;

        [JsonIgnore]
        public int DisplayRepostsCount => IsEmptyRepost ? Repost.RepostsCount : RepostsCount;

        [JsonIgnore]
        public string DisplayLoveHeartFill => IsEmptyRepost ? Repost.LoveHeartFill : LoveHeartFill;

        [JsonIgnore]
        public string DisplayLoveHeartStroke => IsEmptyRepost ? Repost.LoveHeartStroke : LoveHeartStroke;
        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                try
                {
                    DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime date = start.AddMilliseconds(Time).ToLocalTime();
                    
                    var span = DateTime.Now - date;
                    if (span.TotalDays > 30)
                        return date.ToString("MMM dd, yyyy");
                    if (span.TotalDays >= 1)
                        return $"{(int)span.TotalDays}d ago";
                    if (span.TotalHours >= 1)
                        return $"{(int)span.TotalHours}h ago";
                    if (span.TotalMinutes >= 1)
                        return $"{(int)span.TotalMinutes}m ago";
                    return "just now";
                }
                catch
                {
                    return "";
                }
            }
        }

        [JsonIgnore]
        public string CleanContent
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return "";
                // Stripping basic HTML from post content for clean rendering
                var text = Content;
                text = text.Replace("<p>", "").Replace("</p>", "\n");
                text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
                // Regex would be nice but simple replace handles 90% of wasteof posts
                while (text.Contains("<"))
                {
                    int start = text.IndexOf("<");
                    int end = text.IndexOf(">");
                    if (end > start)
                    {
                        text = text.Remove(start, end - start + 1);
                    }
                    else
                    {
                        break;
                    }
                }
                // Decode basic XML entities
                text = text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
                return text.Trim();
            }
        }
    }

    public class NotificationResponse
    {
        [JsonProperty("unread")]
        public List<Notification> Unread { get; set; }

        [JsonProperty("read")]
        public List<Notification> Read { get; set; }

        [JsonProperty("last")]
        public bool Last { get; set; }
    }

    public class Notification
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("to")]
        public Poster To { get; set; }

        [JsonProperty("data")]
        public NotificationData Data { get; set; }

        [JsonProperty("read")]
        public bool Read { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonIgnore]
        public string NotificationText
        {
            get
            {
                string actorName = Data?.Actor?.Name ?? "Someone";
                switch (Type)
                {
                    case "love":
                        return $"{actorName} loved your post";
                    case "repost":
                        return $"{actorName} reposted your post";
                    case "comment":
                        return $"{actorName} commented on your post";
                    case "follow":
                        return $"{actorName} started following you";
                    case "mention":
                    case "post_mention":
                        return $"{actorName} mentioned you in a post";
                    case "comment_mention":
                        return $"{actorName} mentioned you in a comment";
                    case "wall_post":
                        return $"{actorName} posted on your wall";
                    default:
                        return $"{actorName} interacted with you ({Type})";
                }
            }
        }

        [JsonIgnore]
        public string NotificationContent
        {
            get
            {
                if (Data == null) return string.Empty;

                if (Data.Comment != null)
                {
                    return Data.Comment.CleanContent;
                }

                if (Data.Post != null)
                {
                    return Data.Post.CleanContent;
                }

                if (!string.IsNullOrEmpty(Data.Content))
                {
                    var text = Data.Content;
                    text = text.Replace("<p>", "").Replace("</p>", "\n");
                    text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
                    while (text.Contains("<"))
                    {
                        int start = text.IndexOf("<");
                        int end = text.IndexOf(">");
                        if (end > start)
                            text = text.Remove(start, end - start + 1);
                        else
                            break;
                    }
                    return text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ").Trim();
                }

                return string.Empty;
            }
        }

        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                try
                {
                    DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime date = start.AddMilliseconds(Time).ToLocalTime();
                    var span = DateTime.Now - date;
                    if (span.TotalDays > 7)
                        return date.ToString("MMM dd, yyyy");
                    if (span.TotalDays >= 1)
                        return $"{(int)span.TotalDays}d ago";
                    if (span.TotalHours >= 1)
                        return $"{(int)span.TotalHours}h ago";
                    if (span.TotalMinutes >= 1)
                        return $"{(int)span.TotalMinutes}m ago";
                    return "just now";
                }
                catch
                {
                    return "";
                }
            }
        }
    }

    public class NotificationData
    {
        [JsonProperty("actor")]
        public Poster Actor { get; set; }

        [JsonProperty("post")]
        public Post Post { get; set; }

        [JsonProperty("comment")]
        public Comment Comment { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("wall")]
        public Poster Wall { get; set; }
    }

    public class CommentResponse
    {
        [JsonProperty("comments")]
        public List<Comment> Comments { get; set; }

        [JsonProperty("last")]
        public bool Last { get; set; }
    }

    public class Comment
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("post")]
        public string PostId { get; set; }

        [JsonProperty("poster")]
        public Poster Poster { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("hasReplies")]
        public bool HasReplies { get; set; }

        [JsonProperty("top")]
        public string Top { get; set; }

        [JsonIgnore]
        public int Level { get; set; }

        [JsonIgnore]
        public Windows.UI.Xaml.Thickness CommentMargin => new Windows.UI.Xaml.Thickness(System.Math.Min(96, 24 * Level), 0, 0, 16);

        // Helper properties for UI
        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                try
                {
                    DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime date = start.AddMilliseconds(Time).ToLocalTime();
                    var span = DateTime.Now - date;
                    if (span.TotalDays > 7)
                        return date.ToString("MMM dd, yyyy");
                    if (span.TotalDays >= 1)
                        return $"{(int)span.TotalDays}d ago";
                    if (span.TotalHours >= 1)
                        return $"{(int)span.TotalHours}h ago";
                    if (span.TotalMinutes >= 1)
                        return $"{(int)span.TotalMinutes}m ago";
                    return "just now";
                }
                catch
                {
                    return "";
                }
            }
        }

        [JsonIgnore]
        public string CleanContent
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return "";
                var text = Content;
                text = text.Replace("<p>", "").Replace("</p>", "\n");
                text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
                while (text.Contains("<"))
                {
                    int start = text.IndexOf("<");
                    int end = text.IndexOf(">");
                    if (end > start)
                    {
                        text = text.Remove(start, end - start + 1);
                    }
                    else
                    {
                        break;
                    }
                }
                text = text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
                return text.Trim();
            }
        }
    }

    public class LoginRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }

    public class SessionResponse
    {
        [JsonProperty("user")]
        public User User { get; set; }
    }

    public class LoveToggleResponse
    {
        [JsonProperty("ok")]
        public string Ok { get; set; }

        [JsonProperty("new")]
        public LoveState NewState { get; set; }
    }

    public class LoveState
    {
        [JsonProperty("isLoving")]
        public bool IsLoving { get; set; }

        [JsonProperty("loves")]
        public int Loves { get; set; }
    }

    public class FollowToggleResponse
    {
        [JsonProperty("ok")]
        public string Ok { get; set; }

        [JsonProperty("new")]
        public FollowState NewState { get; set; }
    }

    public class FollowState
    {
        [JsonProperty("isFollowing")]
        public bool IsFollowing { get; set; }

        [JsonProperty("followers")]
        public int Followers { get; set; }

        [JsonProperty("following")]
        public int Following { get; set; }
    }

    public class CreatePostRequest
    {
        [JsonProperty("post")]
        public string PostContent { get; set; }

        [JsonProperty("repost")]
        public string RepostId { get; set; }
    }

    public class CreateCommentRequest
    {
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }
    }

    public class FollowersWrapper
    {
        [JsonProperty("followers")]
        public List<User> Followers { get; set; }
    }

    public class FollowingWrapper
    {
        [JsonProperty("following")]
        public List<User> Following { get; set; }
    }

    public class SavedAccount
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }
}
