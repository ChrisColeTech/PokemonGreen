namespace PokemonGreen.SwitchToolboxCli.App.Commands;

internal sealed class CliArguments
{
    private readonly Dictionary<string, string> _options;

    private CliArguments(string input, Dictionary<string, string> options)
    {
        Input = input;
        _options = options;
    }

    public string Input { get; }

    public static bool TryParse(IReadOnlyList<string> rawArgs, out CliArguments arguments, out string error)
    {
        arguments = null!;
        error = string.Empty;

        if (rawArgs.Count == 0)
        {
            error = "Missing input path.";
            return false;
        }

        var input = rawArgs[0];
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < rawArgs.Count; i += 2)
        {
            var key = rawArgs[i];
            if (!key.StartsWith('-'))
            {
                error = $"Unexpected argument: {key}";
                return false;
            }

            if (i + 1 >= rawArgs.Count)
            {
                error = $"Missing value for option: {key}";
                return false;
            }

            var value = rawArgs[i + 1];
            options[key] = value;
        }

        arguments = new CliArguments(input, options);
        return true;
    }

    public bool TryGetRequiredOption(string key, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (!_options.TryGetValue(key, out var optionValue) || string.IsNullOrWhiteSpace(optionValue))
        {
            error = $"Missing required option: {key}";
            return false;
        }

        value = optionValue;
        return true;
    }

    public bool TryGetOption(string key, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (!_options.TryGetValue(key, out var optionValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(optionValue))
        {
            error = $"Missing value for option: {key}";
            return false;
        }

        value = optionValue;
        return true;
    }

    public bool EnsureOnlyOptions(IReadOnlyList<string> allowedOptions, out string error)
    {
        error = string.Empty;
        var allowed = new HashSet<string>(allowedOptions, StringComparer.OrdinalIgnoreCase);

        foreach (var option in _options.Keys)
        {
            if (!allowed.Contains(option))
            {
                error = $"Unsupported option: {option}";
                return false;
            }
        }

        return true;
    }
}
