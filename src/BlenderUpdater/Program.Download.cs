using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ByteSizeLib;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Spectre.Console;

namespace BlenderUpdater {
    partial class Program {
        static int RunDownloadAndReturnExitCode(DownloadOptions options) {
            var client = new BlenderOrgClient();
            var result = client.GetAvailableVersions(experimentalBranches: options.Experimental).GetAwaiter()
                .GetResult();

            var filtered = result
                .Where(x => x.Tag == options.Branch);

            if(!string.IsNullOrEmpty(options.Name)){
                filtered = filtered.Where(x=>x.Version == "blender-"+options.Name);
            }
            else if (!string.IsNullOrEmpty(options.Architecture)) {
                filtered = filtered.Where(x => x.Architecture == options.Architecture);
            }
            else {
                if (string.IsNullOrEmpty(options.OperatingSystem)) {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        filtered = filtered.Where(x => x.OperatingSystem.ToLower() == "macos");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        filtered = filtered.Where(x => x.OperatingSystem.ToLower() == "windows");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        filtered = filtered.Where(x => x.OperatingSystem.ToLower() == "linux");
                }
                else {
                    filtered = filtered.Where(x => x.OperatingSystem == options.OperatingSystem);
                }
            }

            if (filtered.Count() > 1) {
                AnsiConsole.WriteLine("Multiple options, specify arch");
                var t = new Table();
                t.AddColumns("OS", "Variation", "Name", "Arch", "Size", "Built On");
                foreach (var v in filtered) {
                    AddVersionLine(t, v);
                }

                AnsiConsole.Write(t);
                return 1;
            }

            var final = filtered.ToArray().FirstOrDefault();
            if (final == null) {
                Console.WriteLine("Your choices yielded no results, further specify which version you want.");
                return 1;
            }


            AnsiConsole.WriteLine("Downloading: ");
            var table = new Table();
            table.AddColumns("OS", "Variation", "Name", "Arch", "Size", "Built On");
            AddVersionLine(table, final);
            AnsiConsole.Write(table);

            if (!Directory.Exists(DownloadFolder))
                Directory.CreateDirectory(DownloadFolder);
            var zipFilePath = Path.Combine($"{DownloadFolder}", Path.GetFileName(final.DownloadUrl.ToString()));

            if (!File.Exists(zipFilePath)) {
                AnsiConsole.Progress()
                    .AutoClear(false)
                    .Columns(new ProgressColumn[] {
                        new TaskDescriptionColumn(),
                        new PercentageColumn(),
                        new ProgressBarColumn(),
                        new SpinnerColumn(),
                    })
                    .Start(context => {
                        var task = context.AddTask("Downloading:");
                        client.DownloadVersion(final, zipFilePath, (progress) => {
                            var percentage = (int) (progress.Percentage * 100);
                            var diff = percentage - task.Value;
                            var speed = ByteSize.FromBytes(progress.BytesPerSecond.GetValueOrDefault(0)).ToString();
                            task.Description($"Downloading {speed}/s ({ByteSize.FromBytes(progress.DownloadedBytes).ToString()}/{ByteSize.FromBytes(progress.TotalBytes).ToString()})");
                            task.Increment(diff);
                        }).GetAwaiter().GetResult();
                        
                        task.StopTask();
                    });
            }
            else
                AnsiConsole.WriteLine("File alread in download directory");

            if (!Directory.Exists(OutFolder)) {
                Directory.CreateDirectory(OutFolder);
            }

            var fullOutput = Unzip(archiveFile: zipFilePath, outputDir: OutFolder);
            if (!options.Experimental)
                SymlinkLatest(extractedPath: fullOutput, latestLinkPath: Path.Join(OutFolder, LATEST_DIR_NAME));
            else
                SymlinkLatest(fullOutput, Path.Join(OutFolder, final.Tag));
            AnsiConsole.WriteLine("Finished!");

            if (options.RunClean) {
                AnsiConsole.WriteLine("Running clean post download...");
                return RunCleanAndReturnExitCode(new CleanOptions());
            }

            return 0;
        }
    }
}