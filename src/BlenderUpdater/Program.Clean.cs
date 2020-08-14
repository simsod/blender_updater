using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace BlenderUpdater
{
    partial class Program
    {
          private static int RunCleanAndReturnExitCode(CleanOptions opts)
        {
            AnsiConsole.WriteLine($"Running cleanup, keeping {opts.Keep} versions");


            var zips = Directory.GetFiles(DownloadFolder).Select(f => new FileInfo(f)).OrderByDescending(x => x.LastWriteTime).ToList();
            var zipTotalCount = zips.Count();

            zips = zips.Skip(opts.Keep).ToList();



            var dirs = Directory.GetDirectories(OutFolder)
                .Select(x => new DirectoryInfo(x))
                .Where(x => x.Name != "latest")
                .OrderByDescending(x => x.LastWriteTime).ToList();

            var dirTotalCount = dirs.Count();
            dirs = dirs.Skip(opts.Keep).ToList();

            // var table = new Table();
            // table.AddColumns("Name", "Date");
            // foreach (var zip in zips)
            //     table.AddRow(zip.Name, zip.LastWriteTime.ToString());
            // foreach (var dir in dirs)
            //     table.AddRow(dir.Name, dir.LastWriteTime.ToString());
            // AnsiConsole.Render(table);

            if (zips.Count > 0 || dirs.Count > 0)
            {
                AnsiConsole.MarkupLine($"Cleaning up [red]{zips.Count}[/] zip-files and [red]{dirs.Count}[/] directories...");
            }
            else
            {
                AnsiConsole.WriteLine("Nothing to delete...");
            }

            foreach (var zip in zips)
            {
                zip.Delete();
            }

            foreach (var dir in dirs)
            {
                dir.Delete(true);
            }

            return 0;
        }
    }
}