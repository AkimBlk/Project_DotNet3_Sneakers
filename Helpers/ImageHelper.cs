using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyProjectBase.Models;

namespace MyProjectBase.Helpers
{
    public static class ImageHelper
    {
        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static IImage? LoadShoePicture(Shoe shoe)
        {
            foreach (var path in GetCandidateImagePaths(shoe))
            {
                try
                {
                    return LoadFromResource(new Uri(path));
                }
                catch
                {
                }
            }

            return null;
        }

        private static string[] GetCandidateImagePaths(Shoe shoe)
        {
            var paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(shoe.ImagePath))
            {
                paths.Add(shoe.ImagePath);

                var fileName = Path.GetFileName(shoe.ImagePath);
                if (!string.IsNullOrWhiteSpace(fileName))
                    paths.Add($"avares://MyProjectBase/Assets/{fileName}");
            }

            var brand = shoe.Brand.ToLowerInvariant();
            var model = shoe.Model.ToLowerInvariant();

            if (brand.Contains("new balance") || brand == "nb")
            {
                paths.Add(model.Contains("1906")
                    ? "avares://MyProjectBase/Assets/NB_1906.png"
                    : "avares://MyProjectBase/Assets/NB_PP.png");
            }
            else if (brand.Contains("nike"))
            {
                paths.Add(model.Contains("p6000") || model.Contains("p-6000")
                    ? "avares://MyProjectBase/Assets/nike_p6000.png"
                    : "avares://MyProjectBase/Assets/nike_air1.png");
            }
            else if (brand.Contains("adidas"))
            {
                paths.Add("avares://MyProjectBase/Assets/adidas_superstar.png");
            }
            else if (brand.Contains("asics"))
            {
                paths.Add("avares://MyProjectBase/Assets/asics.png");
            }

            return paths
                .Where(path => Uri.TryCreate(path, UriKind.Absolute, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }
}
