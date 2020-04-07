using System.IO;

namespace osu_rewrite
{
    public class Patches
    {
        public static string FullPath() => Base.OsuPath;
        public static string Filename() => Path.GetFileName(Base.OsuPath);
        public static void initializePrivate() { }
        public void checkCertificate() { }
    }
}
