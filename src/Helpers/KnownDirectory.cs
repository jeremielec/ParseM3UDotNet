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
                Directory.Delete(this.pathMovie, true);
            if (Directory.Exists(this.pathTvShow))
                Directory.Delete(this.pathTvShow, true);
            Directory.CreateDirectory(pathMovie);
            Directory.CreateDirectory(pathTvShow);
        }

    }
}