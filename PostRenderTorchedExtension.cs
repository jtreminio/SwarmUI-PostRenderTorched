using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.IO;

namespace PostRenderTorched;

public class PostRenderTorchedExtension : Extension
{
    public double StepPriority = 9.9f;
    public const string FeatureFlag = "feature_flag_post_render";
    public static T2IParamGroup PostRenderGroup;
    private readonly FilmGrainFeature FilmGrain = new();
    private readonly VignetteFeature Vignette = new();
    private readonly LutFeature Lut = new();
    private readonly RadialBlurFeature RadialBlur = new();
    private readonly DepthMapBlurFeature DepthMapBlur = new();

    public override void OnInit()
    {
        Logs.Info("PostRender Torched Extension initializing...");

        var nodeFolder = Path.GetFullPath(Path.Join(FilePath, "comfy_node"));
        ComfyUISelfStartBackend.CustomNodePaths.Add(nodeFolder);
        Logs.Init($"PostRender Torched: added {nodeFolder} to ComfyUI CustomNodePaths");
        ComfyUIBackendExtension.FeaturesSupported.UnionWith([FeatureFlag]);
        ComfyUIBackendExtension.FeaturesDiscardIfNotFound.UnionWith([FeatureFlag]);

        PostRenderGroup = new(
            Name: "Post Render",
            Toggles: false,
            Open: false,
            IsAdvanced: false,
            OrderPriority: 9.1
        );

        int featurePriority = 1;

        FilmGrain.RegisterFeature(PostRenderGroup, ref featurePriority);
        Vignette.RegisterFeature(PostRenderGroup, ref featurePriority);
        DepthMapBlur.RegisterFeature(PostRenderGroup, ref featurePriority);
        RadialBlur.RegisterFeature(PostRenderGroup, ref featurePriority);
        Lut.RegisterFeature(PostRenderGroup, ref featurePriority);

        StepPriority = 9.9f;
        FilmGrain.RegisterWorkflowStep(ref StepPriority);
        Vignette.RegisterWorkflowStep(ref StepPriority);
        DepthMapBlur.RegisterWorkflowStep(ref StepPriority);
        RadialBlur.RegisterWorkflowStep(ref StepPriority);
        Lut.RegisterWorkflowStep(ref StepPriority);
    }
}
