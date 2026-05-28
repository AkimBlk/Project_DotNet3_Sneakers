namespace MyProjectBase.Services;

public sealed class AppConfiguration
{
    // Parametres lus depuis l'environnement pour eviter de disperser les URLs dans le code.
    public string MongoConnectionString { get; }
    public string MongoDatabaseName { get; }
    public string JsonBaseUrl { get; }

    public AppConfiguration()
    {
        MongoConnectionString = Read("SNEAKER_MONGODB_URI",
            "mongodb://Meeeee:IAmTheBest@185.157.245.38:443/?authSource=admin&tls=true");
        MongoDatabaseName = Read("SNEAKER_MONGODB_DATABASE", "SneakerCollection");
        JsonBaseUrl = Read("SNEAKER_JSON_BASE_URL", "http://185.157.245.38:8080/json").TrimEnd('/');
    }

    private static string Read(string key, string defaultValue)
    {
        // Si une variable d'environnement existe, elle remplace la valeur par defaut du cahier des charges.
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }
}
