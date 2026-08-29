using System.Globalization;

namespace App.GitHealth.Api.Hosting;

internal static class LauncherOptionsParser
{
    private const string DataDirectoryOption = "--data-dir";
    private const string HelpOption = "--help";
    private const string NoBrowserOption = "--no-browser";
    private const string PortOption = "--port";
    private const string RepositoryOption = "--repo";

    private static readonly Dictionary<string, ValueOption> ValueOptions =
        new Dictionary<string, ValueOption>(StringComparer.Ordinal)
        {
            [RepositoryOption] = ValueOption.Repository,
            [PortOption] = ValueOption.Port,
            [DataDirectoryOption] = ValueOption.DataDirectory,
        };

    public static LauncherParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var state = new ParserState();
        try
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                index += ParseArgument(arguments, index, state);
            }

            return LauncherParseResult.Success(state.Build());
        }
        catch (LauncherArgumentException exception)
        {
            return LauncherParseResult.Failure(exception.Message);
        }
    }

    private static int ParseArgument(
        IReadOnlyList<string> arguments,
        int index,
        ParserState state)
    {
        var argument = arguments[index];
        if (TryApplyFlag(argument, state))
        {
            return 0;
        }

        if (!TryGetValueOption(argument, out var option, out var inlineValue))
        {
            state.HostArguments.Add(argument);
            return 0;
        }

        var value = inlineValue ?? ReadFollowingValue(arguments, index, OptionName(option));
        state.Apply(option, value);
        return inlineValue is null ? 1 : 0;
    }

    private static bool TryApplyFlag(string argument, ParserState state)
    {
        if (argument == NoBrowserOption)
        {
            state.DisableBrowser();
            return true;
        }

        if (argument is HelpOption or "-h")
        {
            state.ShowHelp = true;
            return true;
        }

        RejectFlagValue(argument, NoBrowserOption);
        RejectFlagValue(argument, HelpOption);
        return false;
    }

    private static bool TryGetValueOption(
        string argument,
        out ValueOption option,
        out string? inlineValue)
    {
        if (ValueOptions.TryGetValue(argument, out option))
        {
            inlineValue = null;
            return true;
        }

        foreach (var candidate in ValueOptions)
        {
            var prefix = $"{candidate.Key}=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                option = candidate.Value;
                inlineValue = argument[prefix.Length..];
                return true;
            }
        }

        inlineValue = null;
        return false;
    }

    private static string ReadFollowingValue(
        IReadOnlyList<string> arguments,
        int index,
        string optionName)
    {
        var hasNoValue = index + 1 >= arguments.Count;
        if (hasNoValue
            || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new LauncherArgumentException($"L’option {optionName} attend une valeur.");
        }

        return arguments[index + 1];
    }

    private static void RejectFlagValue(string argument, string optionName)
    {
        if (argument.StartsWith($"{optionName}=", StringComparison.Ordinal))
        {
            throw new LauncherArgumentException(
                $"L’option {optionName} n’accepte aucune valeur.");
        }
    }

    private static string OptionName(ValueOption option) => option switch
    {
        ValueOption.Repository => RepositoryOption,
        ValueOption.Port => PortOption,
        ValueOption.DataDirectory => DataDirectoryOption,
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    private enum ValueOption
    {
        Repository,
        Port,
        DataDirectory,
    }

    private sealed class ParserState
    {
        private readonly HashSet<ValueOption> _seenOptions = [];
        private bool _noBrowserSeen;

        public string? RepositoryPath { get; private set; }

        public int Port { get; private set; } = LauncherOptions.AutomaticPort;

        public string? DataDirectory { get; private set; }

        public bool ShouldOpenBrowser { get; private set; } = true;

        public bool ShowHelp { get; set; }

        public List<string> HostArguments { get; } = [];

        public void Apply(ValueOption option, string value)
        {
            if (!_seenOptions.Add(option))
            {
                throw new LauncherArgumentException(
                    $"L’option {OptionName(option)} ne peut être fournie qu’une fois.");
            }

            switch (option)
            {
                case ValueOption.Repository:
                    RepositoryPath = RequiredValue(value, RepositoryOption);
                    break;
                case ValueOption.Port:
                    Port = ParsePort(value);
                    break;
                case ValueOption.DataDirectory:
                    DataDirectory = RequiredValue(value, DataDirectoryOption);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
        }

        public void DisableBrowser()
        {
            if (_noBrowserSeen)
            {
                throw new LauncherArgumentException(
                    $"L’option {NoBrowserOption} ne peut être fournie qu’une fois.");
            }

            _noBrowserSeen = true;
            ShouldOpenBrowser = false;
        }

        public LauncherOptions Build() => new()
        {
            RepositoryPath = RepositoryPath,
            Port = Port,
            DataDirectory = DataDirectory,
            ShouldOpenBrowser = ShouldOpenBrowser,
            ShowHelp = ShowHelp,
            HostArguments = HostArguments.ToArray(),
        };

        private static int ParsePort(string value)
        {
            var isNumber = int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var port);
            if (!isNumber || port is < 1 or > ushort.MaxValue)
            {
                throw new LauncherArgumentException(
                    $"L’option {PortOption} doit être comprise entre 1 et 65535.");
            }

            return port;
        }

        private static string RequiredValue(string value, string optionName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new LauncherArgumentException($"L’option {optionName} attend une valeur.");
            }

            return value;
        }
    }

    private sealed class LauncherArgumentException(string message) : Exception(message);
}
