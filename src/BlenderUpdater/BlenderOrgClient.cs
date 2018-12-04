using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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
            version.Tag = el.QuerySelector(".build").TextContent;
            version.Version = Path.GetFileNameWithoutExtension(downloadUrl);
            if (version.Version.EndsWith(".tar"))
                version.Version = version.Version.Replace(".tar", "");

             if (version.Version.Contains("win64"))
                 version.Architecture = "win64";
             else if (version.Version.Contains("win32"))
                 version.Architecture = "win32";
             else if (version.Version.Contains("x86_64"))
                 version.Architecture = "x86_64";
             else if (version.Version.Contains("i686"))
                version.Architecture = "i686";
            
            return version;
        }

        public async Task DownloadVersion(BlenderVersion version, string outputPath) {
            var client = new HttpClient();
            var response = await client.GetAsync(version.DownloadUrl);
            await response.Content.ReadAsFileAsync(outputPath, true);

            Console.WriteLine("Downloaded: " + version.Version);
        }
    }
}
