using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MinecraftLuanch
{
    public sealed class BackgroundImportResult
    {
        public int SuccessCount { get; init; }
        public int FailedCount { get; init; }
    }

    public sealed class BackgroundImageService
    {
        private const string BuiltInResourcePrefix = "MinecraftLuanch.Assets.Photos.";

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
            var builtInPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JCLauncher",
                "BuiltInPhotos");

            Directory.CreateDirectory(builtInPath);
            ExtractBuiltInPhotos(builtInPath);
            return builtInPath;
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

        private static void ExtractBuiltInPhotos(string targetDirectory)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(BuiltInResourcePrefix, StringComparison.OrdinalIgnoreCase))
                .Where(name => SupportedExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var resourceName in resources)
            {
                var fileName = resourceName.Substring(BuiltInResourcePrefix.Length);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var targetPath = Path.Combine(targetDirectory, fileName);
                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    continue;
                }

                var shouldWrite = !File.Exists(targetPath);
                if (!shouldWrite)
                {
                    shouldWrite = new FileInfo(targetPath).Length != resourceStream.Length;
                    resourceStream.Position = 0;
                }

                if (!shouldWrite)
                {
                    continue;
                }

                using var fileStream = File.Create(targetPath);
                resourceStream.CopyTo(fileStream);
            }
        }
    }
}
