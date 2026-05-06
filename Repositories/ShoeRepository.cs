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
    public ObservableCollection<Shoe> Shoes { get; } = [];

    public void ReplaceAll(IEnumerable<Shoe> shoes)
    {
        Shoes.Clear();
        foreach (var shoe in shoes)
            Shoes.Add(shoe);
    }

    public void Add(Shoe shoe)
    {
        if (string.IsNullOrWhiteSpace(shoe.Id))
            shoe.Id = Guid.NewGuid().ToString();

        if (Shoes.Any(existing => existing.Id.Equals(shoe.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        Shoes.Add(shoe);
    }

    public bool Remove(string id)
    {
        var shoe = FindById(id);
        return shoe != null && Shoes.Remove(shoe);
    }

    public Shoe? FindById(string id)
    {
        return Shoes.FirstOrDefault(shoe => shoe.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
