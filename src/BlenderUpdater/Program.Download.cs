using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace BlenderUpdater
{
    partial class Program
    {
         static int RunDownloadAndReturnExitCode(DownloadOptions options)
        {
            var client = new BlenderOrgClient();
            var result = client.GetAvailableVersions().GetAwaiter().GetResult();

            var filtered = result
                .Where(x => x.Tag == options.Branch);

            if (!string.IsNullOrEmpty(options.Architecture))
            {
                filtered = filtered.Where(x => x.Architecture == options.Architecture);
            }
            else
            {
                if (string.IsNullOrEmpty(options.OperatingSystem))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        filtered = filtered.Where(x => x.OperatingSystem == "macos");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        filtered = filtered.Where(x => x.OperatingSystem == "windows");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        filtered = filtered.Where(x => x.OperatingSystem == "linux");
                }
                else
                {
                    filtered = filtered.Where(x => x.OperatingSystem == options.OperatingSystem);
                }
            }

            if (filtered.Count() > 1)
            {
                AnsiConsole.WriteLine("Multiple options, specify arch");
                var t = new Table();
                t.AddColumns("OS", "Variation", "Name", "Arch", "Size", "Built On");
                foreach (var v in filtered)
                {
                    AddVersionLine(t, v);
                }

                AnsiConsole.Render(t);
                return 1;
            }

            var final = filtered.ToArray().FirstOrDefault();
            if (final == null)
            {
                Console.WriteLine("Your choices yielded no results, further specify which version you want.");
                return 1;
            }


            AnsiConsole.WriteLine("Downloading: ");
            var table = new Table();
            table.AddColumns("OS", "Variation", "Name", "Arch", "Size", "Built On");
            AddVersionLine(table, final);
            AnsiConsole.Render(table);

            if (!Directory.Exists(DownloadFolder))
                Directory.CreateDirectory(DownloadFolder);
            var zipFilePath = Path.Combine($"{DownloadFolder}", Path.GetFileName(final.DownloadUrl.ToString()));

            if (!File.Exists(zipFilePath))
            {
                client.DownloadVersion(final, zipFilePath).GetAwaiter().GetResult();
            }
            else
                AnsiConsole.WriteLine("File alread in download directory");

            if (!Directory.Exists(OutFolder))
            {
                Directory.CreateDirectory(OutFolder);
            }

            var fullOutput = Unzip(archiveFile: zipFilePath, outputDir: OutFolder);
            SymlinkLatest(extractedPath: fullOutput, latestLinkPath: Path.Join(OutFolder, LATEST_DIR_NAME));
            AnsiConsole.WriteLine("Finished!");

            if (options.RunClean)
            {
                AnsiConsole.WriteLine("Running clean post download...");
                return RunCleanAndReturnExitCode(new CleanOptions());
            }

            return 0;
        }
    }
}