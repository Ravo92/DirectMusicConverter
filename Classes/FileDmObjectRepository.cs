using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class FileDmObjectRepository : IDmObjectRepository
    {
        private readonly string? _rootPath;

        internal FileDmObjectRepository(string? rootPath)
        {
            _rootPath = rootPath;
        }

        public IDmObject? LoadObject(int type, int variant, string segmentName)
        {
            string? resolvedPath = ResolveSegmentPath(_rootPath, segmentName);
            return new DmObject(type, variant, segmentName, resolvedPath);
        }

        internal static string? ResolveSegmentPath(string? rootPath, string segmentName)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return null;
            }

            if (Path.IsPathRooted(segmentName) && File.Exists(segmentName))
            {
                return segmentName;
            }

            string directPath = Path.Combine(rootPath, segmentName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            string[] commonDirectories =
            [
                rootPath,
                Path.Combine(rootPath, "music"),
                Path.Combine(rootPath, "sound"),
                Path.Combine(rootPath, "audio"),
                Path.Combine(rootPath, "data"),
                Path.Combine(rootPath, "data", "music"),
                Path.Combine(rootPath, "data", "sound"),
                Path.Combine(rootPath, "data", "audio"),
            ];

            for (int i = 0; i < commonDirectories.Length; i++)
            {
                string directory = commonDirectories[i];
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, segmentName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            try
            {
                string[] files = Directory.GetFiles(rootPath, segmentName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
