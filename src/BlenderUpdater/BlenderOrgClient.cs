using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task<IEnumerable<BlenderVersion>> GetAvailableVersions(bool experimentalBranches = false) {
            var config = AngleSharp.Configuration.Default.WithDefaultLoader();
            var url = experimentalBranches
                ? "https://builder.blender.org/download/branches/"
                : "https://builder.blender.org/download/daily/";

            var doc = await BrowsingContext.New(config).OpenAsync(url);

            var versions = doc.QuerySelectorAll("ul.builds-list > li .build-info");
            var result = new List<BlenderVersion>();
            foreach (var version in versions) {
                var blenderVersion = ParseVersionListItem(version);
                if(blenderVersion ==null)
                    continue;
                
                result.Add(blenderVersion);
            }

            result = result.Where(x=>Path.GetExtension(x.DownloadUrl.LocalPath) == ".zip").ToList();
            return result;
        }

        private BlenderVersion ParseVersionListItem(IElement el) {
            /*
            <div class="build-info">
                <a class="build-title js-ga"
                    href="https://builder.blender.org/download/daily/blender-2.93.10-candidate+v293.354c22b28c31-windows.amd64-release.zip"
                    title="Download windows 64bit zip file" ga_label="windows 64bit zip file" ga_type="button"
                    ga_cat="download">Blender 2.93.10 - <span class="build-var candidate">Release Candidate</span>
                </a>
                <ul class="build-details">
                    <li title="2022-04-22T02:16:46+02:00">April 22, 02:16:46</li>
                    <li>
                        <a href="https://developer.blender.org/rB354c22b28c31" title="See commit" target="_blank"><i class="i-code pr-2"></i></a>
                        <a href="https://developer.blender.org/diffusion/B/history/v293/;354c22b28c31" title="See history" target="_blank">354c22b28c31</a>
                    </li>
                    <li title="File extension">zip</li>
                    <li title="File size">211.98MB</li>
                </ul>
            </div>
             */

            var version = new BlenderVersion { };
            var downloadUrl = el.QuerySelector("a").Attributes["href"].Value;
            if (downloadUrl.EndsWith(".sha256") || downloadUrl.EndsWith(".msix"))
                return null;

            version.DownloadUrl = new Uri(downloadUrl);
            var dateStr = el.QuerySelector("ul.build-details li:nth-of-type(1)").GetAttribute("title");
            version.BuildDate = DateTime.Parse(dateStr);
            version.Size = el.QuerySelector("li[title=\"File size\"]").TextContent;
            //version.Tag = el.QuerySelector(".build").TextContent;
            version.Tag = el.QuerySelector(".build-var").TextContent?.Replace("branch", string.Empty).Trim();
            version.Version = Path.GetFileNameWithoutExtension(downloadUrl);
            if (version.Version.EndsWith(".tar"))
                version.Version = version.Version.Replace(".tar", "");
            //Console.WriteLine(version);
            if (version.Version.Contains("windows.amd64")){
                version.Architecture = "win64";
                version.OperatingSystem = "Windows";
            }
            else if (version.Version.Contains("win32")) {
                version.OperatingSystem = "Windows";
                version.Architecture = "win32";
            }
            else if (version.Version.Contains("x86_64")) {
                version.OperatingSystem = "Linux";
                version.Architecture = "x86_64";
            }
            else if (version.Version.Contains("i686")) {
                version.Architecture = "i686";
                version.OperatingSystem = "---";
            }
            else if (version.Version.Contains("arm64")) {
                version.Architecture = "arm64";
                version.OperatingSystem = "MacOS";
            }
            else{
                version.Architecture = "---";
                version.OperatingSystem = "---";
            }
            return version;
        }

        public async Task DownloadVersion(BlenderVersion version, string outputPath, Action<DownloadProgress> progressCallback) {
            var client = new HttpClient {Timeout = TimeSpan.FromHours(2)};
            using var response = client.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).Result;
            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
            response.EnsureSuccessStatusCode();

            await using Stream contentStream = await response.Content.ReadAsStreamAsync(),fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write,FileShare.None, 8192, true);
            
            var totalRead = 0L;
            var lastRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;
            var sw = new Stopwatch();
            double lastSpeed = 0.0;
            sw.Start();
            do {
                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) {
                    isMoreToRead = false;
                    progressCallback(new DownloadProgress { TotalBytes = contentLength, DownloadedBytes = totalRead });
                }
                else {
                    await fileStream.WriteAsync(buffer, 0, read);
                    if(sw.Elapsed.TotalSeconds >2){
                        lastSpeed= (totalRead - lastRead) / sw.Elapsed.TotalSeconds;
                        lastRead = totalRead;
                        sw.Restart();
                    }
                    
                    totalRead += read;
                    progressCallback(new DownloadProgress { TotalBytes = contentLength, DownloadedBytes = totalRead, BytesPerSecond = lastSpeed});
                    
                }
            } while (isMoreToRead);
        }
    }

    public class DownloadProgress {
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }

        public double Percentage => (double)DownloadedBytes / TotalBytes;
        public double? BytesPerSecond { get; set; }
    }
}