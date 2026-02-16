using System;
using System.Linq;

namespace PokemonGreen.Core.Save;

internal static class SqliteHelpers
{
    public static string ToCSV(int[] values)
    {
        return string.Join(',', values);
    }

    public static int[] FromCSV(string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return [];

        var parts = csv.Split(',');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = int.Parse(parts[i]);
        return result;
    }
}
