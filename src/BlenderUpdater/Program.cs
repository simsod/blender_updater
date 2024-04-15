using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommandLine;
using ICSharpCode.SharpZipLib.Zip;
using static System.Environment;
using Spectre.Console;

namespace BlenderUpdater {
    [Verb("list", HelpText = "Lists available versions")]
    class ListOptions {
        [Option('a', "arch", Required = false, HelpText = "Filter on architecture")]
        public string Architecture { get; set; }

        [Option('b', "branch", Required = false, HelpText = "Filter on branch ")]
        public string Branch { get; set; }

        [Option('x', "experimental", Required = false, HelpText = "Fetches experimental branches")]
        public bool Experimental { get; set; }

        [Option('o', "os", Required = false)] public string OperatingSystem { get; set; }
    }

    [Verb("download", HelpText = "Downloads a blender version/build")]
    class DownloadOptions {
        [Option('n',"name", Required=false)]
        public string Name {get;set;}
        
        [Option('a', "arch", Required = false)]
        public string Architecture { get; set; }

        [Option('b', "branch", Required = true)]
        public string Branch { get; set; }

        [Option('o', "os", Required = false)] public string OperatingSystem { get; set; }

        [Option('x', "experimental", Required = false, HelpText = "Fetches experimental branches")]
        public bool Experimental { get; set; }

        [Option('c', "clean", Required = false)]
        public bool RunClean { get; set; } = false;
    }

    [Verb("clean", HelpText = "Cleans outdated versions")]
    class CleanOptions {
        [Option('k', Required = false)] public int Keep { get; set; } = 5;
    }

    public partial class Program {
        static int Main(string[] args) {
            return CommandLine.Parser.Default.ParseArguments<ListOptions, DownloadOptions, CleanOptions>(args)
                .MapResult(
                    (ListOptions opts) => RunListAndReturnExitCode(opts),
                    (DownloadOptions opts) => RunDownloadAndReturnExitCode(opts),
                    (CleanOptions opts) => RunCleanAndReturnExitCode(opts),
                    err => 1
                );
        }

        public const string LATEST_DIR_NAME = "latest";

        public static string DownloadFolder {
            get { return Path.Join(RootPath, "download"); }
        }

        public static string OutFolder {
            get { return Path.Join(RootPath, "out"); }
        }

        public static string RootPath {
            get { return Path.Join(Environment.GetFolderPath(SpecialFolder.ApplicationData), "BlenderUpdater"); }
        }

        static void SymlinkLatest(string extractedPath, string latestLinkPath) {
            AnsiConsole.WriteLine("Updating \"latest\" symlink");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                if (Directory.Exists(latestLinkPath)) {
                    Directory.Delete(latestLinkPath);
                }

                var process = new ProcessStartInfo("ln", $"-s {extractedPath} {latestLinkPath}");
                Process.Start(process);

                var executablePath = extractedPath + "/Blender/Blender.app/Contents/MacOS/blender";
                OsxUpdatePermissions(executablePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var latestPath = Path.GetFullPath(latestLinkPath);
                JunctionPoint.Create(latestPath, extractedPath, true);
            }
        }

        static void OsxUpdatePermissions(string executablePath) {
            var p = new ProcessStartInfo("chmod", $"+x {executablePath}");
            Process.Start(p);
        }

        static string Unzip(string archiveFile, string outputDir) {
            var zipFile = new ZipFile(archiveFile);
            var dir = Path.GetDirectoryName(zipFile[0].Name);
            dir = dir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).First();
            AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[] {
                    new TaskDescriptionColumn(),
                    new SpinnerColumn(),
                })
                .Start(context => {
                    var task = context.AddTask("Unpacking:");
                    var fz = new FastZip();
                    fz.ExtractZip(archiveFile, outputDir, "");
                    task.StopTask();
                });


            return Path.Join(outputDir, dir);
        }

        static string Un7zip(string archiveFile, string outputDir) {
            var dir = Path.GetFileNameWithoutExtension(archiveFile);
            dir = Path.Join(outputDir, dir);
            AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[] {
                    new TaskDescriptionColumn(),
                    new SpinnerColumn(),
                })
                .Start(context => {
                    var task = context.AddTask("Unpacking:");
                    var process = new ProcessStartInfo("7zz", $"x \"{archiveFile}\" -o\"{dir}\" -y")
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        };
                    var p = Process.Start(process);
                    p.WaitForExit();
                    task.StopTask();
                });
            
            return dir;
        }
    }
}