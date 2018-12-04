using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommandLine;
using Terminal.Gui;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;

namespace BlenderUpdater {

    [Verb("list", HelpText = "Lists available versions")]
    class ListOptions {
        [Option('a', "arch", Required = false, HelpText = "Filter on architecture")]
        public string Architecture { get; set; }

        [Option('b', "branch", Required = false, HelpText = "Filter on branch ")]
        public string Branch { get; set; }

        [Option('o', "os", Required = false)]
        public string OperatingSystem { get; set; }
    }

    [Verb("download", HelpText = "Downloads a blender version/build")]
    class DownloadOptions {
        [Option('a', "arch", Required = false)]
        public string Architecture { get; set; }

        [Option('b', "branch", Required = true)]
        public string Branch { get; set; }

        [Option('o', "os", Required = false)]
        public string OperatingSystem { get; set; }
    }
    class Program {

        static int Main(string[] args) {
            return CommandLine.Parser.Default.ParseArguments<ListOptions, DownloadOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(opts),
                    (DownloadOptions opts) => RunDownloadAndReturnExitCode(opts),
                    err => 1
                );
        }

        static int RunListAndReturnExitCode(ListOptions options) {
            Console.WriteLine("Fetching versions from builder.blender.org...");
            Console.WriteLine();

            var client = new BlenderOrgClient();
            var result = client.GetAvailableVersions().GetAwaiter().GetResult().ToList();

            if (!string.IsNullOrEmpty(options.Branch)) {
                result = result.Where(x => x.Tag == options.Branch).ToList();
            }

            if (!string.IsNullOrEmpty(options.OperatingSystem)) {
                result = result.Where(x => x.OperatingSystem == options.OperatingSystem).ToList();
            }

            PrintListHeader();
            foreach (var res in result) {
                PrintVersionLine(res);
            }

            return 0;
        }

        static void PrintListHeader() {
            Console.WriteLine("Available versions from blender buildbot");
            Console.WriteLine("");
            Console.Write($"OS\t");
            Console.Write($"Variation".PadRight(20));
            Console.Write($"Name".PadRight(50));
            Console.Write($"Arch\t");
            Console.Write($"Size".PadRight(10));
            Console.Write($"Built On\t");
            Console.WriteLine();
            Console.WriteLine("===================================================================================================================");
        }

        static void PrintVersionLine(BlenderVersion version) {
            //$"{OperatingSystem} {Tag} {Version} {Architecture} {Size} {BuildDate.ToString("yyyy-MM-dd hh:mm")}";
            var defaultColor = ConsoleColor.Gray;
            Console.Write($"{version.OperatingSystem}\t");
            if (version.Tag == "Blender 2.8") {
                Console.ForegroundColor = ConsoleColor.Red;
            } else {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.Write($"{version.Tag}".PadRight(20));
            Console.ForegroundColor = defaultColor;
            Console.Write($"{version.Version}".PadRight(50));
            Console.Write($"{version.Architecture}\t");
            Console.Write($"{version.Size}".PadRight(10));
            Console.Write($"{version.BuildDate.ToString("yyyy-MM-dd hh:mm")}\t");

            Console.WriteLine();
        }

        static int RunDownloadAndReturnExitCode(DownloadOptions options) {
            var client = new BlenderOrgClient();
            var result = client.GetAvailableVersions().GetAwaiter().GetResult();

            var filtered = result
                .Where(x => x.Tag == options.Branch);

            if (!string.IsNullOrEmpty(options.Architecture)) {
                filtered = filtered.Where(x => x.Architecture == options.Architecture);
            } else {
                if (string.IsNullOrEmpty(options.OperatingSystem)) {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        filtered = filtered.Where(x => x.OperatingSystem == "macos");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        filtered = filtered.Where(x => x.OperatingSystem == "windows");
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        filtered = filtered.Where(x => x.OperatingSystem == "linux");
                } else {
                    filtered = filtered.Where(x => x.OperatingSystem == options.OperatingSystem);
                }
            }

            if (filtered.Count() > 1) {
                Console.WriteLine("Multiple options, specify arch");
                foreach (var v in filtered) {
                    PrintVersionLine(v);
                }
                return 1;
            }

            var final = filtered.ToArray().FirstOrDefault();
            if (final == null) {
                Console.WriteLine("Your choices yielded no results, further specify which version you want.");
                return 1;
            }


            Console.WriteLine("Downloading: ");
            PrintVersionLine(final);

            if (!Directory.Exists("download"))
                Directory.CreateDirectory("download");
            var outputPath = Path.Combine("download/", Path.GetFileName(final.DownloadUrl.ToString()));

            if (!File.Exists(outputPath)) {
                client.DownloadVersion(final, outputPath).GetAwaiter().GetResult();
            } else
                Console.WriteLine("File alread in download directory");

            if (!Directory.Exists("out")) {
                Directory.CreateDirectory("out");
            }

            var fullOutput = Unzip(outputPath, "out/");
            SymlinkLatest(fullOutput);
            Console.WriteLine("Finished!");
            return 0;
        }

        static void SymlinkLatest(string extractedPath) {
            Console.WriteLine("Updating \"latest\" symlink");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                var latestPath = Path.GetFullPath("out/latest");
                if (Directory.Exists(latestPath)) {
                    Directory.Delete(latestPath);
                }

                var process = new ProcessStartInfo("ln", $"-s {extractedPath} {latestPath}");

                Process.Start(process);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var latestPath = Path.GetFullPath("out/latest");                
                JunctionPoint.Create(latestPath, extractedPath, true);
            }
        }
        static string Unzip(string archiveFile, string outputDir) {
            Console.WriteLine("Unpacking file");
            var zipFile = new ZipFile(archiveFile);
            var dir = Path.GetDirectoryName(zipFile[0].Name);
            dir = dir.Split(Path.DirectorySeparatorChar,StringSplitOptions.RemoveEmptyEntries).First();


            var fz = new FastZip();
            fz.ExtractZip(archiveFile, outputDir, "");
            return Path.GetFullPath($"out/{dir}");
        }
    }
}
