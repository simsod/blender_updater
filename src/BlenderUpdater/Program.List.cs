using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommandLine;
using ICSharpCode.SharpZipLib.Zip;
using static System.Environment;
using Spectre.Console;

namespace BlenderUpdater
{
    partial class Program
    {
        

        static int RunListAndReturnExitCode(ListOptions options)
        {
            AnsiConsole.WriteLine("Fetching versions from builder.blender.org...");
            AnsiConsole.WriteLine();

            var client = new BlenderOrgClient();
            var result = client.GetAvailableVersions( experimentalBranches: options.Experimental)
                .GetAwaiter().GetResult()
                .OrderByDescending(x=>x.BuildDate)
                .ToList();

            if (!string.IsNullOrEmpty(options.Branch))
            {
                result = result.Where(x => x.Tag == options.Branch).ToList();
            }

            if (!string.IsNullOrEmpty(options.OperatingSystem))
            {
                result = result.Where(x => string.Equals(x.OperatingSystem, options.OperatingSystem, StringComparison.CurrentCultureIgnoreCase)).ToList();
            }

            var table = new Table();
            table.AddColumns("OS", "Variation", "Version", "Arch", "Size", "Built On");

            foreach (var res in result)
            {
                AddVersionLine(table, res);
            }

            AnsiConsole.Write(table);

            return 0;
        }

        static void AddVersionLine(Table table, BlenderVersion version)
        {
            var tag = version.Tag == "Alpha" ? $"[red]{version.Tag}[/]" : $"[yellow]{version.Tag}[/]";
            table.AddRow(version.OperatingSystem, tag, version.Version.Replace("blender-", ""), version.Architecture, version.Size, version.BuildDate.ToString("yyyy-MM-dd hh:mm"));
        }
    }
}