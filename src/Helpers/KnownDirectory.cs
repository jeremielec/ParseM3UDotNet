using Models;

namespace Helpers
{
    public class KnownDirectory
    {
        public readonly string pathCacheDir;
        public readonly string pathMovie;
        public readonly string pathTvShow;

        public KnownDirectory(SettingsModel settingsModel)
        {
            this.pathCacheDir = Path.Combine(settingsModel.Output.Folder, "cache");
            this.pathMovie = Path.Combine(settingsModel.Output.Folder, "movie");
            this.pathTvShow = Path.Combine(settingsModel.Output.Folder, "tvshow");
            Directory.CreateDirectory(pathCacheDir);
        }

        public void Clear()
        {
            if (Directory.Exists(this.pathMovie))
                EmptyDirectory(this.pathMovie);
            if (Directory.Exists(this.pathTvShow))
                EmptyDirectory(this.pathTvShow);
            Directory.CreateDirectory(pathMovie);
            Directory.CreateDirectory(pathTvShow);
        }

        public static void EmptyDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                // Delete all files
                foreach (var file in Directory.GetFiles(path))
                {
                    File.Delete(file);
                }

                // Delete all folders
                foreach (var directory in Directory.GetDirectories(path))
                {
                    Directory.Delete(directory, true);
                }
            }
        }


    }
}