using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using YoutubeExtractor;

namespace YouTubeDownloader
{
    class Program
    {
        private static readonly string captionXmlUrlFormat =
            "https://www.youtube.com/api/timedtext?asr_langs=en&v={0}&xorp=True&key=yttt1&caps=asr&lang=en&fmt=srv3";
        static void Main(string[] args)
        {
            var youtubeApi = new YouTubeApi();
            youtubeApi.Authorize().Wait();

            var settingFileName = args != null & args.Length > 0 ? args[0] : getSettingFileName();
            var jsonText = File.ReadAllText(settingFileName);
            var setting = JsonConvert.DeserializeObject<DownloadSetting>(jsonText);
            IEnumerable<string> videoIds = null;

            switch (setting.ListFetchType)
            {
                case DownloadSetting.ListFetchTypes.search:
                    videoIds = youtubeApi.SearchVideos(setting.Query, setting.ChannelId).Select(item=>item.Id.VideoId);
                    Console.WriteLine($"search result count: {videoIds.Count()}");
                    break;
            }

            if (!string.IsNullOrEmpty(setting.OutputDirectory)&& !Directory.Exists(setting.OutputDirectory))
            {
                Directory.CreateDirectory(setting.OutputDirectory);
            }

            if (videoIds != null)
            {
                foreach (var videoId in videoIds)
                {
                    var fileName =
                        Path.Combine(
                            string.IsNullOrEmpty(setting.OutputDirectory)
                                ? Directory.GetCurrentDirectory()
                                : setting.OutputDirectory,
                            videoId);
                    DownloadWithVideoId(videoId, fileName);
                    using (var wc = new WebClient())
                    {
                        var response = wc.DownloadString(string.Format(captionXmlUrlFormat, videoId));
                        if (string.IsNullOrWhiteSpace(response))
                        {
                            response = wc.DownloadString(string.Format(captionXmlUrlFormat, videoId) + "&name=English");
                        }
                        
                        if (string.IsNullOrWhiteSpace(response))
                        {
                            Console.WriteLine($"{videoId} caption not found");
                        }
                        else
                        {
                            File.WriteAllText(videoId + ".xml", response);
                        }
                    }
                }
            }
        }

        private static string getSettingFileName()
        {
            Console.Write("setting json path: ");
            return Console.ReadLine();
        }

        static void DownloadWithUrl(string url, string outputFileName = null)
        {

            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(url);
            // LogVideoInfos(videoInfos);
            var audioExtractables = videoInfos.Where(info => info.CanExtractAudio);
            var wavConvertNeeded = !audioExtractables.Any();
            VideoInfo videoInfo = null;

            if (wavConvertNeeded) //TODO: audio extractable이 없으면 영상 다운로드 후 wav 변환
            {
                var mp4Videos = videoInfos.Where(info => info.VideoType == VideoType.Mp4)
                    .OrderByDescending(info => info.AudioBitrate);
                if (mp4Videos.Any())
                {
                    videoInfo = mp4Videos.First();
                }
            }
            else
            {
                videoInfo = audioExtractables.OrderByDescending(info => info.AudioBitrate).First();
            }

            if (videoInfo == null)
            {
                return;
            }

            if (videoInfo.RequiresDecryption)
            {
                DownloadUrlResolver.DecryptDownloadUrl(videoInfo);
            }


            if (string.IsNullOrEmpty(outputFileName))
                outputFileName = videoInfo.Title;

            if (wavConvertNeeded)
            {
                var fileName = outputFileName + videoInfo.VideoExtension;

                var videoDownloader = new VideoDownloader(videoInfo, Path.Combine("", fileName));
                // videoDownloader.DownloadProgressChanged += (sender, e) => Console.WriteLine(e.ProgressPercentage);
                videoDownloader.Execute();
            }
            else
            {
                var fileName = outputFileName + videoInfo.AudioExtension;

                var audioDownloader = new AudioDownloader(videoInfo, fileName);
                // audioDownloader.DownloadProgressChanged += (sender, e) => Console.WriteLine(e.ProgressPercentage * 0.85);
                // audioDownloader.AudioExtractionProgressChanged += (sender, e) => Console.WriteLine(85 + e.ProgressPercentage * 0.15);
                audioDownloader.Execute();
            }
        }

        static void DownloadWithVideoId(string videoId, string outputFileName = null)
        {
            DownloadWithUrl($"https://www.youtube.com/watch?v={videoId}", outputFileName);
        }

        private static string ReplaceInvalideFileCharactor(string fileName)
        {
            return System.IO.Path.GetInvalidFileNameChars()
                .Aggregate(fileName, (current, c) => current.Replace(c, '_'));
        }

        private static void LogVideoInfos(IEnumerable<VideoInfo> videos)
        {
            foreach (var video in videos.OrderByDescending(info => info.AudioBitrate))
            {
                Console.WriteLine(video.ToString() + video.VideoType + " " + video.AudioBitrate);
            }
        }
    }
}