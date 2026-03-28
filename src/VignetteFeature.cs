using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace PostRenderTorched;

internal sealed class VignetteFeature
{
    public const string NodeName = "ProPostVignette";
    private const string PresetNone = "None";
    private const string PresetCenteredNatural = "Centered Natural (light, photographic edge darkening)";
    private const string PresetClassicPortrait = "Classic Portrait (medium, flattering centered focus)";
    private const string PresetCinematicSpotlight = "Cinematic Spotlight (strong, moody subject isolation)";
    private const string PresetOffCenterDrama = "Off-Center Drama (medium, asymmetric framing emphasis)";
    private T2IRegisteredParam<string> Preset;
    private T2IRegisteredParam<float> Strength;
    private T2IRegisteredParam<float> PositionX;
    private T2IRegisteredParam<float> PositionY;

    public void RegisterFeature(T2IParamGroup group, ref int featurePriority)
    {
        ComfyUIBackendExtension.NodeToFeatureMap[NodeName] = PostRenderTorchedExtension.FeatureFlag;
        T2IParamTypes.ParameterRemaps["vigvignettestrength"] = "vignettestrength";
        T2IParamTypes.ParameterRemaps["vigxposition"] = "vignettexposition";
        T2IParamTypes.ParameterRemaps["vigyposition"] = "vignetteyposition";

        T2IParamGroup vignetteGroup = new(
            Name: "Vignette",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: featurePriority,
            Parent: group
        );

        featurePriority += 1;
        int orderCounter = 0;

        Preset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Vignette Preset",
            Description: "Quick starting points for vignette. Set to None to use the manual controls below.",
            Default: PresetNone,
            IgnoreIf: PresetNone,
            GetValues: _ => [
                PresetNone,
                PresetCenteredNatural,
                PresetClassicPortrait,
                PresetCinematicSpotlight,
                PresetOffCenterDrama
            ],
            Group: vignetteGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Strength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Vignette Strength",
            Description: "Vignette strength, lower is weaker",
            Default: "0.2",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: vignetteGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PositionX = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Vignette X Position",
            Description: "Vignette X position, 0 is left, 0.5 is center, 1 is right",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: vignetteGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PositionY = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Vignette Y Position",
            Description: "Vignette Y position, 0 is top, 0.5 is center, 1 is bottom",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: vignetteGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));
    }

    public void RegisterWorkflowStep(ref double stepPriorityCtr)
    {
        WorkflowGenerator.AddStep(g =>
        {
            if (!g.UserInput.TryGet(Strength, out float strength))
            {
                return;
            }

            ApplyPreset(g);
            string vignetteNode = g.CreateNode(NodeName, new JObject
            {
                ["image"] = g.CurrentMedia.Path,
                ["intensity"] = strength,
                ["center_x"] = g.UserInput.Get(PositionX),
                ["center_y"] = g.UserInput.Get(PositionY),
            });
            g.CurrentMedia = g.CurrentMedia.WithPath([vignetteNode, 0]);
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
            PresetCenteredNatural => new(0.22f, 0.50f, 0.50f),
            PresetClassicPortrait => new(0.42f, 0.50f, 0.50f),
            PresetCinematicSpotlight => new(0.72f, 0.50f, 0.46f),
            PresetOffCenterDrama => new(0.55f, 0.42f, 0.44f),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(Strength, config.Intensity);
        g.UserInput.Set(PositionX, config.CenterX);
        g.UserInput.Set(PositionY, config.CenterY);
    }

    private sealed record PresetConfig(
        float Intensity,
        float CenterX,
        float CenterY
    );
}
