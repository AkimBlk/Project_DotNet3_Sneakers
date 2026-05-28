using System.Collections.ObjectModel;
using MyProjectBase.Models;

namespace MyProjectBase.Repositories;

public interface IShoeRepository
{
    ObservableCollection<Shoe> Shoes { get; }
    void ReplaceAll(IEnumerable<Shoe> shoes);
    void Add(Shoe shoe);
    bool Remove(string id);
    Shoe? FindById(string id);
}

public sealed class ShoeRepository : IShoeRepository
{
    // ObservableCollection permet a l'interface Avalonia de se mettre a jour automatiquement.
    public ObservableCollection<Shoe> Shoes { get; } = [];

    public void ReplaceAll(IEnumerable<Shoe> shoes)
    {
        // Remplace toute la collection, par exemple apres un chargement JSON distant.
        Shoes.Clear();
        foreach (var shoe in shoes)
            Shoes.Add(shoe);
    }

    public void Add(Shoe shoe)
    {
        // Si aucun ID n'est fourni, on cree un identifiant pour garder chaque sneaker unique.
        if (string.IsNullOrWhiteSpace(shoe.Id))
            shoe.Id = Guid.NewGuid().ToString();

        // Un ID existe une seule fois dans la collection.
        if (Shoes.Any(existing => existing.Id.Equals(shoe.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        Shoes.Add(shoe);
    }

    public bool Remove(string id)
    {
        // Supprime par ID pour que l'UI et les services n'aient pas besoin de garder la reference exacte.
        var shoe = FindById(id);
        return shoe != null && Shoes.Remove(shoe);
    }

    public Shoe? FindById(string id)
    {
        return Shoes.FirstOrDefault(shoe => shoe.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
