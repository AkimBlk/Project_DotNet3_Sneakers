using Avalonia.Media;
using System.Text.Json.Serialization;

namespace MyProjectBase.Models;

public class Shoe
{
    public string Id { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    [JsonIgnore]
    public IImage? Picture { get; set; }
}
