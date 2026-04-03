using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MongoDB.Bson;

namespace MyProjectBase.Models;

public class Shoe
{
    public Shoe()
    {}

    public string Id { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    
    internal IImage? Picture { get; set; } 
}