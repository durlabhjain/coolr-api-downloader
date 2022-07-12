using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Coolr.Api
{
    public class Downloader
    {
        private static readonly CultureInfo cultureInfo = CultureInfo.InvariantCulture;
        private static readonly HttpClient client = new HttpClient();
        public string ServerUrl { get; set; } = "http://localhost:4890";
        public string Username { get; set; } = "Prashanth.K@spraxa.com";
        public string Password { get; set; } = "Paper01@Paper";

        public string ControllerName { get; set; } = "Assetpurity"; //"Alert";//"Assetpurity";
        public int Limit { get; set; } = 100;

        public bool Hourly { get; set; } = true;

        public int HourlyGap { get; set; } = 4;
        public string Sort { get; set; } = "ModifiedOn";

        public bool UsePaging { get; set; } = true;

        public int DelayBetweenRequests { get; set; } = 0;

        public class Records
        {
            public int recordCount { get; set; }
            public List<Dictionary<string, object>>? records { get; set; }
        }

        public class DownloadConfig
        {
            public DateTime StartDate { get; set; } = new DateTime(2020, 1, 1);
        }

        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);

        public async Task Download(DateTime startDate, Dictionary<string, string>? extraParameters = null)
        {
            await Download(new DownloadConfig() { StartDate = startDate }, extraParameters);
        }

        public async Task Download(DownloadConfig downloadConfig, Dictionary<string, string>? extraParameters = null)
        {
            var configFile = $"{ControllerName}.config.json";

            if (File.Exists(configFile))
            {
                downloadConfig = JsonSerializer.Deserialize<DownloadConfig>(File.ReadAllText(configFile));
            }

            while (downloadConfig.StartDate < EndDate)
            {
                var start = 0;
                var fetchUrl = $"{ServerUrl}/Controllers/{ControllerName}.ashx";
                var startDate = downloadConfig.StartDate;
                var endDate = Hourly ? downloadConfig.StartDate.AddHours(HourlyGap) : downloadConfig.StartDate.AddDays(1);
                var filter = new Dictionary<string, object> {
                    { "left",
                        new Dictionary<string, object> {
                            { "fieldName", "ModifiedOn" },
                            { "operatorId", "DATE_GREATER_OR_EQUAL" },
                            { "convert", false },
                            { "values", new string[] { startDate.ToString("yyyy-MM-dd HH:mm:ss") } }
                        }
                    },
                    { "logicalOperator", "AND" },
                    { "right",
                        new Dictionary<string, object> {
                            { "fieldName", "ModifiedOn" },
                            { "operatorId", "DATE_LESS" },
                            { "convert", false },
                            { "values", new string[] { endDate.ToString("yyyy-MM-dd HH:mm:ss") } }
                        }
                    }
                };

                var fetchNextPage = true;
                while (fetchNextPage)
                {

                    var parameters = new Dictionary<string, string> {
                        { "action", "list" },
                        { "limit", Limit.ToString() },
                        { "asArray",  "0" },
                        { "start",  start.ToString() },
                        { "sort", Sort },
                        { "filter", JsonSerializer.Serialize(filter) }
                    };

                    if (extraParameters != null)
                    {
                        foreach (var extraParameter in extraParameters)
                        {
                            parameters[extraParameter.Key] = extraParameter.Value;
                        }
                    }

                    Console.WriteLine($"Downloading from {downloadConfig.StartDate.ToString("yyyy-MM-dd HH:mm:ss")} start:{start}");

                    var outputFile = $"output/{ControllerName}-{downloadConfig.StartDate.ToString("yyyy-MM-dd")}.json";

                    var request = new HttpRequestMessage(HttpMethod.Post, fetchUrl);
                    var byteArray = System.Text.Encoding.ASCII.GetBytes($"{Username}:{Password}");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                    request.Content = new FormUrlEncodedContent(parameters);

                    var response = await client.SendAsync(request);
                    var responseString = await response.Content.ReadAsStringAsync();

                    var json = JsonSerializer.Deserialize<Records>(responseString);

                    Console.WriteLine($"Records: {json.recordCount}");

                    if (json?.records?.Count > 0)
                    {
                        using (var sw = File.AppendText(outputFile))
                        {
                            string records = JsonSerializer.Serialize(json.records);
                            if (sw.BaseStream.Length == 0)
                            {
                                sw.WriteLine("[");
                            }
                            sw.Write(records.Substring(1, records.LastIndexOf("]") - 1));
                        }
                        if (!UsePaging)
                        {
                            downloadConfig.StartDate = DateTime.ParseExact(((JsonElement)json.records.Last()["ModifiedOn"]).GetString(), "yyyyMMddHHmmssfff", cultureInfo).AddSeconds(1);
                            downloadConfig.StartDate = downloadConfig.StartDate.AddMilliseconds(-downloadConfig.StartDate.Millisecond);
                        }
                    }
                    start += json.records.Count;
                    if (start >= json.recordCount)
                    {
                        downloadConfig.StartDate = Hourly ? startDate.AddHours(HourlyGap) : startDate.AddDays(1);
                        if (downloadConfig.StartDate.Date.Subtract(startDate).TotalDays > 0)
                        {
                            if (File.Exists(outputFile))
                            {
                                using (var sw = File.AppendText(outputFile))
                                {
                                    sw.WriteLine("]");
                                }
                            }
                            File.WriteAllText(configFile, JsonSerializer.Serialize(downloadConfig));
                        }
                        fetchNextPage = false;
                    }
                    else
                    {
                        fetchNextPage = UsePaging;
                    }
                }
            }
        }
    }
}