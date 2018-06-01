using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YouTubeDownloader
{
    internal class DownloadSetting
    {
        public enum ListFetchTypes
        {
            search
        }

        [JsonProperty("list_fetch_type")]
        public ListFetchTypes ListFetchType { get; set; }
        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }
        [JsonProperty("query")]
        public string Query { get; set; }
        [JsonProperty("output_directory")]
        public string OutputDirectory { get; set; }
        [JsonProperty("ffmpeg_path")]
        public string FFMpegPath { get; set; }

        [JsonProperty("extract_audio")]
        public bool ExtractAudio { get; set; } = false;

    }
}