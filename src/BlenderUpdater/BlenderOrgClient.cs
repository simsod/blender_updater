using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace BlenderUpdater {
    public class BlenderOrgClient {
        public async Task<IEnumerable<BlenderVersion>> GetAvailableVersions() {
            var config = AngleSharp.Configuration.Default.WithDefaultLoader();
            var url = "https://builder.blender.org/download";

            var doc = await BrowsingContext.New(config).OpenAsync(url);

            var versions = doc.QuerySelectorAll(".builds-list .os");

            var result = new List<BlenderVersion>();
            foreach (var version in versions) {
                var os = version.ClassList.Where(x => x != "os").FirstOrDefault();
                result.Add(ParseVersionListItem(version, os));                
            }

            return result;
        }

        private BlenderVersion ParseVersionListItem(IElement el, string os) {
            /*
            <li class="os windows">
                <a href="/download//blender-2.79-cd9ab9d99eb-win64.zip">
                    <span class="name">
                        Windows 64 bit
                        <small>November 16, 01:30:14</small>
                    </span>
                    <span class="build">
                        <span class="build-var master">Master</span>
                    </span>
                    <span class="size">127.99MB</span>
                </a>
            </li>
             */

            var version = new BlenderVersion { OperatingSystem = os };
            var downloadUrl = el.QuerySelector("a").Attributes["href"].Value;
            version.DownloadUrl = new Uri("https://builder.blender.org" + downloadUrl);
            
            var dateAndHash = el.QuerySelector("a .name small").TextContent.Split('-',StringSplitOptions.RemoveEmptyEntries);
            var dateStr = dateAndHash[0].Trim();

            version.BuildDate = DateTime.ParseExact(dateStr, "MMMM dd, HH:mm:ss", CultureInfo.InvariantCulture);            

            version.Size = el.QuerySelector("a .size").TextContent;
            //version.Tag = el.QuerySelector(".build").TextContent;
            version.Tag = el.QuerySelector(".build-var").TextContent;
            version.Version = Path.GetFileNameWithoutExtension(downloadUrl);
            if (version.Version.EndsWith(".tar"))
                version.Version = version.Version.Replace(".tar", "");

             if (version.Version.Contains("windows64"))
                 version.Architecture = "win64";
             else if (version.Version.Contains("win32"))
                 version.Architecture = "win32";
             else if (version.Version.Contains("x86_64"))
                 version.Architecture = "x86_64";
             else if (version.Version.Contains("i686"))
                version.Architecture = "i686";
            else
                version.Architecture = "---";
            
            return version;
        }

        public async Task DownloadVersion(BlenderVersion version, string outputPath) {
            
            // var done = false;
            // double? perc = null;
            // using(var client = new HttpClientDownloadWithProgress(version.DownloadUrl.ToString(),outputPath)){
            //     client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) => {
            //         perc = progressPercentage;
            //         Console.WriteLine($"{progressPercentage}% ({totalBytesDownloaded}/{totalFileSize})");
            //         if(progressPercentage == 100.0){
            //             done = true;
            //         }
            //     };                                
            // }            

            // while(!done){
            //     Thread.Sleep(1000);
            // }

                    
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromHours(2);
            var response = await client.GetAsync(version.DownloadUrl);
            await response.Content.ReadAsFileAsync(outputPath, true);
            
            Console.WriteLine("Downloaded: " + version.Version);
        }
    }


public class HttpClientDownloadWithProgress : IDisposable
{
    private readonly string _downloadUrl;
    private readonly string _destinationFilePath;

    private HttpClient _httpClient;

    public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

    public event ProgressChangedHandler ProgressChanged;

    public HttpClientDownloadWithProgress(string downloadUrl, string destinationFilePath)
    {
        _downloadUrl = downloadUrl;
        _destinationFilePath = destinationFilePath;
    }

    public async Task StartDownload()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromDays(1) };

        using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            await DownloadFileFromHttpResponseMessage(response);
    }

    private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        using (var contentStream = await response.Content.ReadAsStreamAsync())
            await ProcessContentStream(totalBytes, contentStream);
    }

    private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
    {
        var totalBytesRead = 0L;
        var readCount = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;

        using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    continue;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead);

                totalBytesRead += bytesRead;
                readCount += 1;

                if (readCount % 100 == 0)
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead);
            }
            while (isMoreToRead);
        }
    }

    private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
    {
        if (ProgressChanged == null)
            return;

        double? progressPercentage = null;
        if (totalDownloadSize.HasValue)
            progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

        ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}    
}
