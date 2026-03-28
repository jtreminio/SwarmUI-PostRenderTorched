using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace PostRenderTorched;

internal sealed class RadialBlurFeature
{
    public const string NodeName = "ProPostRadialBlur";
    private const string PresetNone = "None";
    private const string PresetSubtleZoom = "Subtle Zoom (light, centered motion energy)";
    private const string PresetHeroPushIn = "Hero Push-In (medium, cinematic center emphasis)";
    private const string PresetImpactBurst = "Impact Burst (strong, stylized action streaking)";
    private const string PresetOffAxisWhip = "Off-Axis Whip (medium-strong, directional motion feel)";
    private T2IRegisteredParam<string> Preset;
    private T2IRegisteredParam<float> Strength;
    private T2IRegisteredParam<float> PositionX;
    private T2IRegisteredParam<float> PositionY;
    private T2IRegisteredParam<float> FocusSpread;
    private T2IRegisteredParam<int> Steps;

    public void RegisterFeature(T2IParamGroup group, ref int featurePriority)
    {
        ComfyUIBackendExtension.NodeToFeatureMap[NodeName] = PostRenderTorchedExtension.FeatureFlag;
        T2IParamTypes.ParameterRemaps["rblurstrength"] = "radialblurstrength";
        T2IParamTypes.ParameterRemaps["rblurxposition"] = "radialblurxposition";
        T2IParamTypes.ParameterRemaps["rbluryposition"] = "radialbluryposition";
        T2IParamTypes.ParameterRemaps["rblurfocusspread"] = "radialblurfocusspread";
        T2IParamTypes.ParameterRemaps["rblursteps"] = "radialblursteps";

        T2IParamGroup radialBlurGroup = new(
            Name: "Radial Blur",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: featurePriority,
            Parent: group
        );

        featurePriority += 1;
        int orderCounter = 0;

        Preset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Radial Blur Preset",
            Description: "Quick starting points for radial blur. Set to None to use the manual controls below.",
            Default: PresetNone,
            IgnoreIf: PresetNone,
            GetValues: _ => [
                PresetNone,
                PresetSubtleZoom,
                PresetHeroPushIn,
                PresetImpactBurst,
                PresetOffAxisWhip
            ],
            Group: radialBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Strength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Radial Blur Strength",
            Description: "Blur Strength, lower is weaker",
            Default: "64",
            Min: 0, Max: 256, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: radialBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PositionX = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Radial Blur X Position",
            Description: "Blur X position, 0 is left, 0.5 is center, 1 is right",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: radialBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        PositionY = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Radial Blur Y Position",
            Description: "Blur Y position, 0 is top, 0.5 is center, 1 is bottom",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: radialBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        FocusSpread = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Radial Blur Focus Spread",
            Description: "Spread of the area of focus, higher is sharper",
            Default: "1",
            Min: 0.1, Max: 8.0, Step: 0.1,
            ViewType: ParamViewType.SLIDER,
            Group: radialBlurGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Steps = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "Radial Blur Steps",
            Description: "Number of steps to use when bluring image, higher is slower",
            Default: "5",
            Min: 1, Max: 32, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: radialBlurGroup,
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
            string blurNode = g.CreateNode(NodeName, new JObject
            {
                ["image"] = g.CurrentMedia.Path,
                ["blur_strength"] = strength,
                ["center_x"] = g.UserInput.Get(PositionX),
                ["center_y"] = g.UserInput.Get(PositionY),
                ["focus_spread"] = g.UserInput.Get(FocusSpread),
                ["steps"] = g.UserInput.Get(Steps),
            });
            g.CurrentMedia = g.CurrentMedia.WithPath([blurNode, 0]);
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
            PresetSubtleZoom => new(48.0f, 0.50f, 0.50f, 1.5f, 6),
            PresetHeroPushIn => new(84.0f, 0.50f, 0.50f, 1.2f, 7),
            PresetImpactBurst => new(128.0f, 0.50f, 0.50f, 1.0f, 8),
            PresetOffAxisWhip => new(104.0f, 0.38f, 0.42f, 1.0f, 8),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(Strength, config.BlurStrength);
        g.UserInput.Set(PositionX, config.CenterX);
        g.UserInput.Set(PositionY, config.CenterY);
        g.UserInput.Set(FocusSpread, config.FocusSpread);
        g.UserInput.Set(Steps, config.Steps);
    }

    private sealed record PresetConfig(
        float BlurStrength,
        float CenterX,
        float CenterY,
        float FocusSpread,
        int Steps
    );
}
