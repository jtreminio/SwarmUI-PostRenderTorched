using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace PostRenderTorched;

internal sealed class DepthMapBlurFeature
{
    public const string NodeName = "ProPostDepthMapBlur";
    public const string NodeNameDepthMap = "DepthAnythingPreprocessor";
    private const string PresetNone = "None";
    private const string PresetPortraitSeparation = "Portrait Separation (natural, forgiving subject focus)";
    private const string PresetMacroProduct = "Macro Product (strong, close-up subject isolation)";
    private const string PresetCinematicRackFocus = "Cinematic Rack Focus (selective, pronounced focal plane)";
    private const string PresetMiniatureTiltShift = "Miniature / Tilt-Shift Feel (stylized, fake scale blur)";
    private const string PresetSoftAtmospheric = "Soft Atmospheric (gentle, dreamy depth falloff)";
    private readonly List<string> DepthModels = ["depth_anything_vitl14.pth", "depth_anything_vitb14.pth", "depth_anything_vits14.pth"];
    private T2IRegisteredParam<string> Preset;
    private T2IRegisteredParam<string> PreProcessorResolution;
    private T2IRegisteredParam<string> PreProcessorModelName;
    private T2IRegisteredParam<float> BlurStrength;
    private T2IRegisteredParam<float> FocalDepth;
    private T2IRegisteredParam<float> FocusSpread;
    private T2IRegisteredParam<int> Steps;
    private T2IRegisteredParam<float> FocalRange;
    private T2IRegisteredParam<int> MaskBlur;

    public void RegisterFeature(T2IParamGroup group, ref int featurePriority)
    {
        ComfyUIBackendExtension.NodeToFeatureMap[NodeName] = PostRenderTorchedExtension.FeatureFlag;
        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameDepthMap] = PostRenderTorchedExtension.FeatureFlag;

        T2IParamTypes.ParameterRemaps["dmblurdepthmapresolution"] = "depthmapblurresolution";
        T2IParamTypes.ParameterRemaps["dmblurdepthmodel"] = "depthmapblurmodel";
        T2IParamTypes.ParameterRemaps["dmblurblurstrength"] = "depthmapblurstrength";
        T2IParamTypes.ParameterRemaps["dmblurfocaldepth"] = "depthmapblurfocaldepth";
        T2IParamTypes.ParameterRemaps["dmblurfocusspread"] = "depthmapblurfocusspread";
        T2IParamTypes.ParameterRemaps["dmblursteps"] = "depthmapblursteps";
        T2IParamTypes.ParameterRemaps["dmblurfocalrange"] = "depthmapblurfocalrange";
        T2IParamTypes.ParameterRemaps["dmblurmaskblur"] = "depthmapblurmaskblur";

        T2IParamGroup depthMapBlurGroup = new(
            Name: "Depth Map Blur",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: featurePriority,
            Parent: group
        );

        featurePriority += 1;
        int orderCounter = 0;

        Preset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Depth Map Blur Preset",
            Description: "Quick starting points for depth-based blur. Set to None to use the manual controls below.",
            Default: PresetNone,
            IgnoreIf: PresetNone,
            GetValues: _ => [
                PresetNone,
                PresetPortraitSeparation,
                PresetMacroProduct,
                PresetCinematicRackFocus,
                PresetMiniatureTiltShift,
                PresetSoftAtmospheric
            ],
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PreProcessorResolution = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Depth Map Blur Resolution",
            Description: "The resolution of the depth map (1024 suggested)",
            Default: "1024",
            GetValues: _ => ["256", "512", "1024", "2048"],
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PreProcessorModelName = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Depth Map Blur Model",
            Description: "The model used for the depth map image\nModels will download automatically as needed",
            Default: DepthModels[0],
            GetValues: _ => DepthModels,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        BlurStrength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Depth Map Blur Strength",
            Description: "The intensity of the blur",
            Default: "64.0",
            Min: 0.0, Max: 256.0, Step: 1.0,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        FocalDepth = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Depth Map Blur Focal Depth",
            Description: "The focal depth of the blur. 1.0 is the closest, 0.0 is the farthest",
            Default: "1.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        FocusSpread = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Depth Map Blur Focus Spread",
            Description: "The spread of the area of focus. A larger value makes more of the image sharp",
            Default: "1.0",
            Min: 1.0, Max: 8.0, Step: 0.1,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Steps = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "Depth Map Blur Steps",
            Description: "The number of steps to use when blurring the image. Higher numbers are slower",
            Default: "5",
            Min: 1, Max: 32, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        FocalRange = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Depth Map Blur Focal Range",
            Description: "1.0 means all areas clear, 0.0 means only focal point is clear",
            Default: "0.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        MaskBlur = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "Depth Map Blur Mask Blur",
            Description: "Mask blur strength (1 to 127).1 means no blurring",
            Default: "1",
            Min: 1, Max: 127, Step: 2,
            ViewType: ParamViewType.SLIDER,
            Group: depthMapBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));
    }

    public void RegisterWorkflowStep(ref double stepPriorityCtr)
    {
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(BlurStrength, out float _))
            {
                ApplyPreset(g);
                string depthAnything = g.CreateNode(NodeNameDepthMap, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["resolution"] = int.Parse(g.UserInput.Get(PreProcessorResolution)),
                    ["ckpt_name"] = g.UserInput.Get(PreProcessorModelName),
                });
                JArray depthMap = [depthAnything, 0];
                string blurNode = g.CreateNode(NodeName, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["depth_map"] = depthMap,
                    ["blur_strength"] = g.UserInput.Get(BlurStrength),
                    ["focal_depth"] = g.UserInput.Get(FocalDepth),
                    ["focus_spread"] = g.UserInput.Get(FocusSpread),
                    ["steps"] = g.UserInput.Get(Steps),
                    ["focal_range"] = g.UserInput.Get(FocalRange),
                    ["mask_blur"] = g.UserInput.Get(MaskBlur),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([blurNode, 0]);
            }
        }, stepPriorityCtr);
        stepPriorityCtr += 0.01f;
    }

    private void ApplyPreset(WorkflowGenerator g)
    {
        if (!g.UserInput.TryGet(Preset, out string preset) || preset == PresetNone)
        {
            return;
        }

        PresetConfig config = preset switch
        {
            PresetPortraitSeparation => new(56.0f, 0.85f, 2.4f, 6, 0.22f, 11),
            PresetMacroProduct => new(80.0f, 0.92f, 2.0f, 7, 0.12f, 9),
            PresetCinematicRackFocus => new(96.0f, 0.82f, 1.7f, 8, 0.08f, 7),
            PresetMiniatureTiltShift => new(110.0f, 0.50f, 1.4f, 8, 0.04f, 5),
            PresetSoftAtmospheric => new(44.0f, 0.80f, 3.0f, 6, 0.28f, 15),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(BlurStrength, config.BlurStrength);
        g.UserInput.Set(FocalDepth, config.FocalDepth);
        g.UserInput.Set(FocusSpread, config.FocusSpread);
        g.UserInput.Set(Steps, config.Steps);
        g.UserInput.Set(FocalRange, config.FocalRange);
        g.UserInput.Set(MaskBlur, config.MaskBlur);
    }

    private sealed record PresetConfig(
        float BlurStrength,
        float FocalDepth,
        float FocusSpread,
        int Steps,
        float FocalRange,
        int MaskBlur
    );
}
