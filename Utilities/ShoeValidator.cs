using System;
using System.Collections.Generic;
using MyProjectBase.Models;

namespace MyProjectBase.Utilities;

public static class ShoeValidator
{
    public static IReadOnlyList<string> Validate(Shoe shoe)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(shoe.Brand))
            errors.Add("Brand is required.");

        if (string.IsNullOrWhiteSpace(shoe.Model))
            errors.Add("Model is required.");

        if (shoe.Stock < 0)
            errors.Add("Stock cannot be negative.");

        if (shoe.Price < 0)
            errors.Add("Price cannot be negative.");

        if (!string.IsNullOrWhiteSpace(shoe.ImagePath) &&
            !Uri.TryCreate(shoe.ImagePath, UriKind.Absolute, out _))
        {
            errors.Add("Image path is invalid.");
        }

        return errors;
    }
}
