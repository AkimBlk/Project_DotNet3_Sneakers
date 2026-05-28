using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace MyProjectBase.Models;

public partial class Shoe : ObservableObject
{
    // Modele collectionne par l'application : chaque sneaker est identifiee par Id.
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _group = "Sneakers";
    [ObservableProperty] private int _stock;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private string _imagePath = string.Empty;

    // Image chargee uniquement pour l'interface, donc elle n'est pas sauvegardee dans le JSON distant.
    [property: JsonIgnore]
    [ObservableProperty]
    private IImage? _picture;
}
