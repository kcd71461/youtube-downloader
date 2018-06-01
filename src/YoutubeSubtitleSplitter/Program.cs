using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NAudio.Wave;
using Newtonsoft.Json;

namespace YoutubeSubtitleSplitter
{
    class Program
    {
        private static List<SplitInformation> splitInformations = new List<SplitInformation>();
        private static readonly string resultJsonFileName = "result.json";

        static void Main(string[] args)
        {
            LoadResultJson();
            Console.WriteLine("Type *.wav path.");
            string directory = Console.ReadLine();
            var files = Directory.GetFiles(directory);
            foreach (var wavName in files.Where(f => f.EndsWith(".wav")))
            {
                var itemName = Path.GetFileName(wavName.TrimEnd((".wav").ToCharArray()));
                var xmlName = Path.Combine(directory, itemName + ".xml");
                if (File.Exists(xmlName))
                {
                    Spit(itemName, wavName, xmlName);
                }
            }
        }

        private static void LoadResultJson()
        {
            if (File.Exists(resultJsonFileName))
            {
                try
                {
                    var json = File.ReadAllText(resultJsonFileName);
                    splitInformations = JsonConvert.DeserializeObject<List<SplitInformation>>(json);
                    Console.WriteLine($"{resultJsonFileName} Loaded!");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{resultJsonFileName} Load Failed!");
                    throw;
                }
            }
        }

        private static void Spit(string itemName, string wavName, string xmlName)
        {
            using (var waveFileReader = new WaveFileReader(wavName))
            {
                int bytesPerMilliseconds = waveFileReader.WaveFormat.AverageBytesPerSecond / 1000;

                var xml = new XmlDocument();
                xml.Load(xmlName);
                var nodes = xml.GetElementsByTagName("p");

                int step = 1;
                long prevEndTime = 0;
                foreach (XmlNode node in nodes)
                {
                    string sentense = node.InnerText;
                    var startTime = Int64.Parse(node.Attributes["t"].Value);
                    var endTime = startTime + Int64.Parse(node.Attributes["d"].Value);
                    if (startTime < prevEndTime + 300)
                    {
                        prevEndTime = endTime;
                        continue;
                    }

                    var saveFileName = itemName + $"{itemName}_step{step++}.wav";
                    using (var waveFileWriter = new WaveFileWriter(saveFileName, waveFileReader.WaveFormat))
                    {
                        long start = bytesPerMilliseconds * startTime, end = bytesPerMilliseconds * (endTime + 200);
                        waveFileReader.Position = start;
                        var buffer = new byte[waveFileReader.BlockAlign * 1024];
                        while (waveFileReader.Position < end)
                        {
                            long bytesRequired = end - waveFileReader.Position;
                            if (bytesRequired > 0)
                            {
                                int bytesToRead = (int)Math.Min(bytesRequired, buffer.Length);
                                int bytesRead = waveFileReader.Read(buffer, 0, bytesToRead);
                                if (bytesRead > 0)
                                {
                                    waveFileWriter.Write(buffer, 0, bytesToRead);
                                }
                            }
                        }
                    }
                    prevEndTime = endTime;
                    splitInformations.Add(new SplitInformation(saveFileName,sentense));
                }
            }
            SaveSplitInformation();
        }

        private static void SaveSplitInformation()
        {
            File.WriteAllText(resultJsonFileName, Newtonsoft.Json.JsonConvert.SerializeObject(splitInformations));
        }

        private class SplitInformation
        {
            [JsonProperty("text")]
            public string Text { get; set; }
            [JsonProperty("file")]
            public string File { get; set; }

            public SplitInformation(string fileName,string text)
            {
                this.File = fileName;
                this.Text = text;
            }
        }
    }
}
