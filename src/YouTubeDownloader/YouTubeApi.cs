using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace YouTubeDownloader
{
    internal class YouTubeApi
    {
        private UserCredential credential;
        private YouTubeService youtubeService;
        public bool LogEnabled { get; set; } = false;

        public async Task<bool> Authorize()
        {
            var success = false;
            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    // This OAuth 2.0 access scope allows for full read/write access to the
                    // authenticated user's account.
                    new[] {YouTubeService.Scope.YoutubeForceSsl, YouTubeService.Scope.Youtubepartner},
                    "user",
                    CancellationToken.None,
                    new FileDataStore(this.GetType().ToString())
                );

                this.youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = this.credential,
                    ApplicationName = this.GetType().ToString()
                });

                success = true;
            }
            return success;
        }

        public async Task Revoke()
        {
            await credential.RevokeTokenAsync(CancellationToken.None);
        }

        public PlaylistListResponse GetPlayLists(string channelId = null)
        {
            var playlistsRequest = youtubeService.Playlists.List("snippet");
            if (channelId == null)
            {
                playlistsRequest.Mine = true;
            }
            else
            {
                playlistsRequest.ChannelId = channelId;
            }

            var playlists = playlistsRequest.Execute();
            return playlists;
        }

        public List<SearchResult> SearchVideos(string searchStr, string channelId = null)
        {
            var searchRequest = this.youtubeService.Search.List("snippet");
            searchRequest.ChannelId = channelId;
            searchRequest.Q = searchStr;
            searchRequest.MaxResults = 50;
            var response = searchRequest.Execute();
            var results = new List<SearchResult>();
            while (true)
            {
                foreach (var responseItem in response.Items)
                {
                    results.Add(responseItem);
                }

                searchRequest.PageToken = response.NextPageToken;
                if (string.IsNullOrEmpty(searchRequest.PageToken))
                {
                    break;
                }

                response = searchRequest.Execute();
            }

            if (this.LogEnabled)
            {
                Console.WriteLine("Search Result: {0}", results.Count);
            }

            return results;
        }

        public IList<Caption> GetCaption(string videoId)
        {
            var captionListRequest = youtubeService.Captions.List("snippet", videoId);
            captionListRequest.Fields = "items(etag,id,snippet(language,trackKind,lastUpdated))";
            var captionList = captionListRequest.Execute();
            return captionList.Items;
        }

        public string DownloadCaption(string captionId, string fileName)
        {
            var downloadRequest = youtubeService.Captions.Download(captionId);
            downloadRequest.Tfmt = CaptionsResource.DownloadRequest.TfmtEnum.Sbv;
            return downloadRequest.Execute();
        }
    }
}