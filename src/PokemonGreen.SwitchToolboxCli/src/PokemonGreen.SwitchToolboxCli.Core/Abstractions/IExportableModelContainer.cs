namespace PokemonGreen.SwitchToolboxCli.Core.Abstractions;

// Directly adapted from legacy:
// D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\Interfaces\ModelData\IExportableModelContainer.cs
public interface IExportableModelContainer
{
    IEnumerable<object> ExportableModels { get; }
    IEnumerable<object> ExportableTextures { get; }
}
