using System;

namespace BlenderUpdater {
    public class BlenderVersion {
        public string Architecture { get; set; }
        public string Tag { get; set; }
        public string OperatingSystem { get; set; }
        public string Version { get; set; }
        public Uri DownloadUrl { get; set; }
        public DateTime BuildDate { get; set; }
        public string Size { get; set; }

        public override string ToString() {            
            return $"{OperatingSystem} {Tag} {Version} {Architecture} {Size} {BuildDate.ToString("yyyy-MM-dd hh:mm")}";
        }
    }
}
