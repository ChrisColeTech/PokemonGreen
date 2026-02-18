using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PokemonGreen.SwitchToolboxCli.Core.Serialization;

public sealed class DeterministicJsonFileWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public void Write<TValue>(string outputPath, TValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(value);

        var fullPath = Path.GetFullPath(outputPath);
        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        File.WriteAllText(fullPath, json + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var sorted = typeInfo.Properties
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToList();

            typeInfo.Properties.Clear();
            foreach (var property in sorted)
            {
                typeInfo.Properties.Add(property);
            }
        });

        return new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = resolver,
        };
    }
}
