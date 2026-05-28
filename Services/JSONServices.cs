using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MyProjectBase.Models;
using MyProjectBase.Utilities;

namespace MyProjectBase.Services;

public interface IJsonShoeService
{
    Task<ServiceResult<List<Shoe>>> GetShoesAsync(string collectionKey, CancellationToken cancellationToken = default);
    Task<ServiceResult> SetShoesAsync(string collectionKey, IEnumerable<Shoe> shoes, CancellationToken cancellationToken = default);
}

public sealed class JsonShoeService : IJsonShoeService
{
    // Options communes pour lire et ecrire le JSON distant de la collection.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;
    private readonly string _baseUrl;

    public JsonShoeService(IAppLogger logger)
        : this(logger, new AppConfiguration())
    {
    }

    public JsonShoeService(IAppLogger logger, AppConfiguration configuration)
    {
        _logger = logger;
        _baseUrl = configuration.JsonBaseUrl;
        // Timeout court pour ne pas bloquer l'interface si le serveur distant ne repond pas.
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<ServiceResult<List<Shoe>>> GetShoesAsync(string collectionKey, CancellationToken cancellationToken = default)
    {
        // Chaque utilisateur a un nom de fichier separe pour garder sa collection privee.
        var fileName = GetFileName(collectionKey);
        var url = $"{_baseUrl}?FileName={Uri.EscapeDataString(fileName)}";

        try
        {
            using var response = await SendWithRetryAsync(
                () => _httpClient.GetAsync(url, cancellationToken),
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return ServiceResult<List<Shoe>>.NotFound("No remote JSON collection exists yet for this user.");

            // Une erreur serveur ou reseau ne doit pas creer/ecraser silencieusement une collection.
            if (!response.IsSuccessStatusCode)
                return ServiceResult<List<Shoe>>.Unavailable($"Remote JSON unavailable ({(int)response.StatusCode}).");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var shoes = await JsonSerializer.DeserializeAsync<List<Shoe>>(contentStream, JsonOptions, cancellationToken) ?? [];
            // On ignore les objets invalides pour eviter de casser l'affichage de toute la collection.
            var validShoes = shoes.Where(shoe => ShoeValidator.Validate(shoe).Count == 0).ToList();

            foreach (var shoe in validShoes.Where(shoe => string.IsNullOrWhiteSpace(shoe.Id)))
                shoe.Id = Guid.NewGuid().ToString();

            return ServiceResult<List<Shoe>>.Ok(validShoes, $"{validShoes.Count} sneakers loaded.");
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Invalid remote JSON.");
            return ServiceResult<List<Shoe>>.Fail("Remote JSON is invalid.");
        }
        catch (NotSupportedException ex)
        {
            _logger.Error(ex, "Unsupported JSON format.");
            return ServiceResult<List<Shoe>>.Fail($"Unsupported JSON format: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Error(ex, "Remote JSON timeout.");
            return ServiceResult<List<Shoe>>.Network("Network timeout while loading JSON.");
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "Remote JSON network error.");
            return ServiceResult<List<Shoe>>.Network("No network connection or JSON server unavailable.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected remote JSON error.");
            return ServiceResult<List<Shoe>>.Fail($"JSON loading error: {ex.Message}");
        }
    }

    public async Task<ServiceResult> SetShoesAsync(string collectionKey, IEnumerable<Shoe> shoes, CancellationToken cancellationToken = default)
    {
        try
        {
            // On sauvegarde uniquement les sneakers valides pour garder un JSON propre cote serveur.
            var validShoes = shoes.Where(shoe => ShoeValidator.Validate(shoe).Count == 0).ToList();

            await using var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, validShoes, JsonOptions, cancellationToken);
            memoryStream.Position = 0;

            using var fileContent = new StreamContent(memoryStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };

            using var content = new MultipartFormDataContent
            {
                { fileContent, "file", GetFileName(collectionKey) }
            };

            // Le service distant attend un upload multipart contenant le fichier JSON.
            using var response = await SendWithRetryAsync(
                () => _httpClient.PostAsync(_baseUrl, content, cancellationToken),
                cancellationToken);

            return response.IsSuccessStatusCode
                ? ServiceResult.Ok("Remote JSON updated.")
                : ServiceResult.Fail($"JSON save refused ({(int)response.StatusCode}).");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Error(ex, "JSON save timeout.");
            return ServiceResult.Network("Network timeout while saving JSON.");
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "JSON save network error.");
            return ServiceResult.Network("Save failed: JSON server unavailable.");
        }
        catch (NotSupportedException ex)
        {
            _logger.Error(ex, "Unsupported JSON format while saving.");
            return ServiceResult.Fail($"JSON save failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected JSON save error.");
            return ServiceResult.Fail($"JSON save error: {ex.Message}");
        }
    }

    private static string GetFileName(string collectionKey)
    {
        // Nettoie la cle utilisateur pour produire un nom de fichier utilisable sur le serveur.
        var normalized = string.Concat(collectionKey
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
            .Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? "sneakers-guest.json"
            : $"sneakers-{normalized}.json";
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> operation,
        CancellationToken cancellationToken)
    {
        // Petit retry pour absorber une coupure reseau tres courte sans masquer une vraie panne.
        const int attempts = 3;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch when (attempt < attempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
            }
        }

        return await operation();
    }
}
