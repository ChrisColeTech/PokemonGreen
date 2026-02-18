namespace PokemonGreen.SwitchToolboxCli.Core.Abstractions;

// Directly adapted from legacy:
// D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\Interfaces\ModelData\IExportableModel.cs
public interface IExportableModel
{
    IEnumerable<object> ExportableMeshes { get; }
    IEnumerable<object> ExportableMaterials { get; }
    IEnumerable<object> ExportableTextures { get; }
    object? ExportableSkeleton { get; }
}
