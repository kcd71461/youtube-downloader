using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Frapper;
using Newtonsoft.Json;
using YoutubeExtractor;

namespace YouTubeDownloader
{
    class Program
    {
        private static string ffmpegPath = @"c:\ffmpeg\bin\ffmpeg.exe";

        private static readonly string captionXmlUrlFormat =
            "https://www.youtube.com/api/timedtext?" +
            "asr_langs=en&" +
            "v={0}&" +
            "xorp=True&" +
            "key=yttt1&" +
            "caps=asr&" +
            "lang=en&" +
            "fmt=srv3";

        static void Main(string[] args)
        {
            var youtubeApi = new YouTubeApi();
            youtubeApi.Authorize().Wait();

            var settingFileName = args != null & args.Length > 0 ? args[0] : getSettingFileName();
            var jsonText = File.ReadAllText(settingFileName);
            var setting = JsonConvert.DeserializeObject<DownloadSetting>(jsonText);
            var downloadDirectory = string.IsNullOrEmpty(setting.OutputDirectory)
                ? Directory.GetCurrentDirectory()
                : setting.OutputDirectory;
            if (!string.IsNullOrWhiteSpace(setting.FFMpegPath))
            {
                ffmpegPath = setting.FFMpegPath;
            }


            IEnumerable<string> videoIds = null;

            switch (setting.ListFetchType)
            {
                case DownloadSetting.ListFetchTypes.search:
                    videoIds = youtubeApi.SearchVideos(setting.Query, setting.ChannelId).Select(item => item.Id.VideoId);
                    Console.WriteLine($"search result count: {videoIds.Count()}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!string.IsNullOrEmpty(setting.OutputDirectory) && !Directory.Exists(setting.OutputDirectory))
            {
                Directory.CreateDirectory(setting.OutputDirectory);
            }

            foreach (var videoId in videoIds)
            {
                var fileName =
                    Path.Combine(downloadDirectory, videoId);
                DownloadWithVideoId(videoId, fileName, setting.ExtractAudio);
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
                        File.WriteAllText(Path.Combine(downloadDirectory, videoId + ".xml"), response);
                    }
                }
            }
        }

        private static string getSettingFileName()
        {
            Console.Write("setting json path: ");
            return Console.ReadLine();
        }

        static bool DownloadWithUrl(string url, string outputFileName = null, bool extractAudio = false)
        {

            IEnumerable<VideoInfo> videoInfos=null;
            try
            {
                videoInfos= DownloadUrlResolver.GetDownloadUrls(url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to get {url}");
                return false;
            }
            
            var audioExtractables = videoInfos.Where(info => info.CanExtractAudio);
            var audioTrackExists = false;
            VideoInfo videoInfo = null;

            if (extractAudio && audioExtractables.Any())
            {
                videoInfo = audioExtractables.OrderByDescending(info => info.AudioBitrate).First();
                audioTrackExists = true;
            }
            else
            {
                IEnumerable<VideoInfo> mp4Videos = videoInfos;
                if (extractAudio)
                {
                    mp4Videos = videoInfos.Where(info => info.VideoType == VideoType.Mp4)
                        .OrderByDescending(info => info.AudioBitrate);
                }
                else
                {
                    mp4Videos = videoInfos.Where(info => info.VideoType == VideoType.Mp4 && info.AudioBitrate > 0)
                        .OrderByDescending(info => info.AudioBitrate)
                        .OrderByDescending(info => info.Resolution);
                    LogVideoInfos(mp4Videos);

                }
                if (mp4Videos.Any())
                {
                    videoInfo = mp4Videos.First();
                }
            }

            if (videoInfo == null)
            {
                return false;
            }

            if (videoInfo.RequiresDecryption)
            {
                DownloadUrlResolver.DecryptDownloadUrl(videoInfo);
            }


            if (string.IsNullOrEmpty(outputFileName))
                outputFileName = videoInfo.Title;

            if (!audioTrackExists || !extractAudio)
            {
                var videoFileName = outputFileName + videoInfo.VideoExtension;
                try
                {
                    var videoDownloader = new VideoDownloader(videoInfo, Path.Combine("", videoFileName));
                    // videoDownloader.DownloadProgressChanged += (sender, e) => Console.WriteLine(e.ProgressPercentage);
                    videoDownloader.Execute();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{videoFileName} Download Failed");
                    return false;
                }

                if (extractAudio)
                {
                    var ffmpeg = new FFMPEG(ffmpegPath);
                    ffmpeg.RunCommand($"-i \"{videoFileName}\" \"{outputFileName + ".wav"}\"");
                    // File.Delete(videoFileName);
                }
            }
            else
            {
                var fileName = outputFileName + videoInfo.AudioExtension;
                try
                {
                    var audioDownloader = new AudioDownloader(videoInfo, fileName);
                    // audioDownloader.DownloadProgressChanged += (sender, e) => Console.WriteLine(e.ProgressPercentage * 0.85);
                    // audioDownloader.AudioExtractionProgressChanged += (sender, e) => Console.WriteLine(85 + e.ProgressPercentage * 0.15);
                    audioDownloader.Execute();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{fileName} Download Failed");
                    return false;
                }
            }
            return true;
        }

        static void DownloadWithVideoId(string videoId, string outputFileName = null, bool extractAudio = false)
        {
            DownloadWithUrl($"https://www.youtube.com/watch?v={videoId}", outputFileName, extractAudio);
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