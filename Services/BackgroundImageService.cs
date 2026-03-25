using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MinecraftLuanch
{
    public sealed class BackgroundImportResult
    {
        public int SuccessCount { get; init; }
        public int FailedCount { get; init; }
    }

    public sealed class BackgroundImageService
    {
        private static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".webp"
        };

        public bool HasBuiltInBackgrounds()
        {
            return HasImages(GetBuiltInPhotosDirectory());
        }

        public bool HasCustomBackgrounds()
        {
            return HasImages(GetCustomPhotosDirectory());
        }

        public List<string> LoadBackgroundImages()
        {
            var images = new List<string>();
            AddImagesFromDirectory(images, GetBuiltInPhotosDirectory());
            AddImagesFromDirectory(images, GetCustomPhotosDirectory());

            return images
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public BackgroundImportResult AddBackgrounds(IEnumerable<string> sourceFiles)
        {
            var photosPath = GetCustomPhotosDirectory();
            Directory.CreateDirectory(photosPath);

            var nextIndex = Directory.Exists(photosPath)
                ? Directory.EnumerateFiles(photosPath, "*", SearchOption.TopDirectoryOnly).Count(IsSupportedImage) + 1
                : 1;

            var successCount = 0;
            var failedCount = 0;

            foreach (var file in sourceFiles)
            {
                try
                {
                    if (!IsSupportedImage(file))
                    {
                        failedCount++;
                        continue;
                    }

                    var extension = Path.GetExtension(file);
                    var destFile = Path.Combine(photosPath, $"bg{nextIndex}{extension}");
                    File.Copy(file, destFile, true);
                    nextIndex++;
                    successCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            return new BackgroundImportResult
            {
                SuccessCount = successCount,
                FailedCount = failedCount
            };
        }

        public string? PickNextBackground(IReadOnlyList<string> backgroundImages, string? lastBackground, Random random)
        {
            if (backgroundImages.Count == 0)
            {
                return null;
            }

            var available = backgroundImages
                .Where(path => !string.Equals(path, lastBackground, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (available.Count == 0)
            {
                available = backgroundImages.ToList();
            }

            return available[random.Next(available.Count)];
        }

        public int ClearCustomBackgrounds()
        {
            var photosPath = GetCustomPhotosDirectory();
            if (!Directory.Exists(photosPath))
            {
                return 0;
            }

            var removedCount = Directory.EnumerateFiles(photosPath, "*", SearchOption.AllDirectories)
                .Count(IsSupportedImage);

            foreach (var file in Directory.EnumerateFiles(photosPath, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            foreach (var directory in Directory.EnumerateDirectories(photosPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }

            return removedCount;
        }

        private static bool IsSupportedImage(string path)
        {
            return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }

        private static void AddImagesFromDirectory(List<string> images, string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            images.AddRange(
                Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Where(IsSupportedImage));
        }

        private static string GetBuiltInPhotosDirectory()
        {
            var outputPhotosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos");
            if (HasImages(outputPhotosPath))
            {
                return outputPhotosPath;
            }

            return FindSourcePhotosDirectory() ?? outputPhotosPath;
        }

        private static string GetCustomPhotosDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JCLauncher",
                "photos");
        }

        private static bool HasImages(string? directoryPath)
        {
            return !string.IsNullOrWhiteSpace(directoryPath) &&
                   Directory.Exists(directoryPath) &&
                   Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories).Any(IsSupportedImage);
        }

        private static string? FindSourcePhotosDirectory()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "Assets", "Photos");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
