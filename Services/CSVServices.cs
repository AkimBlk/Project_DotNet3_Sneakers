using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MongoDB.Bson;
using MyProjectBase.Models;

namespace MyProjectBase.Services;

public class CsvServices
{
    private readonly TopLevel _topLevel;

    public CsvServices(TopLevel topLevel) 
    {
        _topLevel = topLevel; 
    }
    public async Task<List<Shoe>> LoadDataAsync()
    {
        var list = new List<Shoe>();

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select CSV file",
            AllowMultiple = false
        });

        if (files.Count <= 0) return list;
    
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string?>();

        while (!reader.EndOfStream) lines.Add(await reader.ReadLineAsync());

        if (lines.Count == 0) return list; 

        var headers = lines[0].Split(';');
        var properties = typeof(Shoe).GetProperties();

        for (var i = 1; i < lines.Count; i++)
        {
            var obj = new Shoe();
            var values = lines[i]?.Split(';');

            if (values != null)
            {
                for (var j = 0; j < headers.Length && j < values.Length; j++)
                {
                    var property = properties.FirstOrDefault(p =>
                        p.Name.Equals(headers[j], StringComparison.OrdinalIgnoreCase));
                    if (property == null || string.IsNullOrWhiteSpace(values[j])) continue;

                    //modif le try/catch pcq id est en string mtn 
                    try
                    {
                        var value = Convert.ChangeType(values[j], property.PropertyType);
                        property.SetValue(obj, value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(ex.Message);
                    }
                    
                }
            }

            list.Add(obj);
        }
        return list;
    }
    public async Task SaveDataAsync<T>(List<T> data)
    {
        var csv = new StringBuilder();
        var properties = typeof(T).GetProperties();
        csv.AppendLine(string.Join(";", properties.Select(p => p.Name)));

        foreach (var item in data)
        {
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? string.Empty);
            csv.AppendLine(string.Join(";", values));
        }

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Enregistrer le fichier CSV",
            SuggestedFileName = "data.csv" 
        });

        if (file != null)
        {
            await using (var stream = await file.OpenWriteAsync())
            {
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(csv.ToString());
            }
        }
    }
}


