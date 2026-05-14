using MyProjectBase.Models;

namespace MyProjectBase.Services;

public static class DefaultShoeCatalog
{
    public static List<Shoe> Create()
    {
        return
        [
            new Shoe
            {
                Id = "DEFAULT-NIKE-AIR-001",
                Brand = "Nike",
                Model = "Air Max",
                Group = "Lifestyle",
                Stock = 12,
                Price = 129.99m,
                ImagePath = "avares://MyProjectBase/Assets/nike_air1.png"
            },
            new Shoe
            {
                Id = "DEFAULT-NIKE-P6000-002",
                Brand = "Nike",
                Model = "P-6000",
                Group = "Running",
                Stock = 8,
                Price = 109.99m,
                ImagePath = "avares://MyProjectBase/Assets/nike_p6000.png"
            },
            new Shoe
            {
                Id = "DEFAULT-ADIDAS-SUPERSTAR-003",
                Brand = "Adidas",
                Model = "Superstar",
                Group = "Classics",
                Stock = 15,
                Price = 99.99m,
                ImagePath = "avares://MyProjectBase/Assets/adidas_superstar.png"
            },
            new Shoe
            {
                Id = "DEFAULT-NB-1906-004",
                Brand = "New Balance",
                Model = "1906",
                Group = "Running",
                Stock = 6,
                Price = 159.99m,
                ImagePath = "avares://MyProjectBase/Assets/NB_1906.png"
            },
            new Shoe
            {
                Id = "DEFAULT-ASICS-005",
                Brand = "Asics",
                Model = "Gel Series",
                Group = "Performance",
                Stock = 10,
                Price = 139.99m,
                ImagePath = "avares://MyProjectBase/Assets/asics.png"
            }
        ];
    }
}
