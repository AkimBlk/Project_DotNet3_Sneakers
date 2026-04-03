using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using MyProjectBase.Models;

namespace MyProjectBase.Services
{
    public class JSONServices
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });
        
        //private const string BaseUrl = "http://185.157.245.38:8080/json";
        private const string BaseUrl = "http://localhost:5226/json";//POUR TEST MOI
        

        internal async Task<List<Shoe>> GetShoesAsync()
        {
            //const string url = $"{BaseUrl}?FileName=MyShoess.json";
            const string url = $"{BaseUrl}?fileName=MyShoess.json";//POUR TEST MOI

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<Shoe>();
            
            var jsonString = await response.Content.ReadAsStringAsync();
            
            //verifie si json vide 
            if (string.IsNullOrWhiteSpace(jsonString)) 
            {
                return new List<Shoe>();
            }
            
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<List<Shoe>>(contentStream) ?? new List<Shoe>();
        }

        internal async Task SetShoesAsync(List<Shoe> shoes)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            
            var url = BaseUrl;

            using var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, shoes, options);
            memoryStream.Position = 0;

            var fileContent = new StreamContent(memoryStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };

            var content = new MultipartFormDataContent
            {
                { fileContent, "file", "MyShoess.json" }
            };

            using var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                
            }
        }
    }
}