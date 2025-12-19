namespace KBSL.Utilities;

public partial class GlobalHelpers
{
    // IMPL: Handle the dynamic loading of model data.
    /*public static Scene ImportModel(string[] paths)
    {
        var path = Path.Combine(ASSETS_MODEL_FOLDER, Path.Combine(paths) + ".fbx");

        AssimpContext context = new();
        Scene scene = context.ImportFile(path,
            PostProcessSteps.Triangulate |
            PostProcessSteps.FlipUVs |
            PostProcessSteps.CalculateTangentSpace);
        return scene;
    }*/
}