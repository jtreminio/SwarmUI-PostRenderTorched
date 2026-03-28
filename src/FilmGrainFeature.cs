using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace PostRenderTorched;

internal sealed class FilmGrainFeature
{
    public const string NodeName = "ProPostFilmGrain";
    private const string PresetNone = "None";
    private const string PresetSubtleModernCamera = "Subtle Modern Camera (light, best)";
    private const string PresetClassic35mmFilm = "Classic 35mm Film (medium, visible grain, warm color variation)";
    private const string PresetGrittyHighIso = "Gritty / High ISO (strong, moody, documentary, low-light aesthetics)";
    private const string PresetCleanDigitalSensor = "Clean Digital Sensor (ultra light, polished, almost invisible)";
    private const string PresetVintagePrintStock = "Vintage Print Stock (medium, soft clumping, less clinical)";
    private const string Preset16mmDocumentary = "16mm Documentary (medium-strong, textured, analog reportage)";
    private const string PresetPushedNightStock = "Pushed Night Stock (strong, colorful shadow noise, low-light cinema)";
    private const string PresetCrunchyIndieFilm = "Crunchy Indie Film (strong, chunky, stylized texture)";
    private const string PresetUniformCmosNoise = "Uniform CMOS Noise (medium, cleaner pattern, digital-video feel)";
    private T2IRegisteredParam<string> Preset;
    private T2IRegisteredParam<bool> GrayScale;
    private T2IRegisteredParam<string> GrainType;
    private T2IRegisteredParam<float> GrainSaturation;
    private T2IRegisteredParam<float> GrainPower;
    private T2IRegisteredParam<float> Shadows;
    private T2IRegisteredParam<float> Highlights;
    private T2IRegisteredParam<float> Scale;
    private T2IRegisteredParam<int> Sharpen;
    private T2IRegisteredParam<float> SourceGamma;
    private T2IRegisteredParam<long> Seed;

    public void RegisterFeature(T2IParamGroup group, ref int featurePriority)
    {
        ComfyUIBackendExtension.NodeToFeatureMap[NodeName] = PostRenderTorchedExtension.FeatureFlag;
        T2IParamTypes.ParameterRemaps["graingrayscale"] = "filmgraingrayscale";
        T2IParamTypes.ParameterRemaps["graingraintype"] = "filmgraintype";
        T2IParamTypes.ParameterRemaps["graingrainsaturation"] = "filmgrainsaturation";
        T2IParamTypes.ParameterRemaps["graingrainpower"] = "filmgrainpower";
        T2IParamTypes.ParameterRemaps["grainshadows"] = "filmgrainshadows";
        T2IParamTypes.ParameterRemaps["grainhighlights"] = "filmgrainhighlights";
        T2IParamTypes.ParameterRemaps["grainscale"] = "filmgrainscale";
        T2IParamTypes.ParameterRemaps["grainsharpen"] = "filmgrainsharpen";
        T2IParamTypes.ParameterRemaps["grainsourcegamma"] = "filmgrainsourcegamma";
        T2IParamTypes.ParameterRemaps["grainseed"] = "filmgrainseed";

        T2IParamGroup grainGroup = new(
            Name: "Film Grain",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: featurePriority,
            Parent: group
        );

        featurePriority += 1;
        int orderCounter = 0;

        Preset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Film Grain Preset",
            Description: "Quick starting points for film grain. Set to None to use the manual controls below.",
            Default: PresetNone,
            IgnoreIf: PresetNone,
            GetValues: _ => [
                PresetNone,
                PresetCleanDigitalSensor,
                PresetSubtleModernCamera,
                PresetClassic35mmFilm,
                PresetVintagePrintStock,
                Preset16mmDocumentary,
                PresetPushedNightStock,
                PresetGrittyHighIso,
                PresetCrunchyIndieFilm,
                PresetUniformCmosNoise
            ],
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        GrayScale = T2IParamTypes.Register<bool>(new T2IParamType(
            Name: "Film Grain Gray Scale",
            Description: "Enables grayscale mode. If true, the output will be in grayscale",
            Default: "false",
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        GrainType = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Film Grain Type",
            Description: "Sets the grain type",
            Default: "Fine Simple",
            GetValues: _ => ["Fine", "Fine Simple", "Coarse", "Coarser"],
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        GrainSaturation = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Saturation",
            Description: "Grain color saturation",
            Default: "0.5",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        GrainPower = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Power",
            Description: "Overall intensity of the grain effect",
            Default: "0.7",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Shadows = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Shadows",
            Description: "Intensity of grain in the shadows",
            Default: "0.2",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Highlights = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Highlights",
            Description: "Intensity of the grain in the highlights",
            Default: "0.2",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Scale = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Scale",
            Description: "Image scaling ratio. Scales the image before applying grain and scales back afterwards",
            Default: "1.0",
            Min: 0.0, Max: 10.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Sharpen = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "Film Grain Sharpen",
            Description: "Number of sharpening passes",
            Default: "0",
            Min: 0, Max: 10,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        SourceGamma = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "Film Grain Source Gamma",
            Description: "Gamma compensation applied to the input",
            Default: "1.0",
            Min: 0.0, Max: 10.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Seed = T2IParamTypes.Register<long>(new T2IParamType(
            Name: "Film Grain Seed",
            Description: "Random seed used for the film grain pattern",
            Default: "-1",
            Min: -1, Max: 1000, Step: 1,
            ViewType: ParamViewType.SEED,
            Group: grainGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));
    }

    public void RegisterWorkflowStep(ref double stepPriorityCtr)
    {
        WorkflowGenerator.AddStep(g =>
        {
            if (!g.UserInput.TryGet(GrainType, out string grainType))
            {
                return;
            }

            ApplyPreset(g);
            string filmNode = g.CreateNode(NodeName, new JObject
            {
                ["image"] = g.CurrentMedia.Path,
                ["gray_scale"] = g.UserInput.Get(GrayScale),
                ["grain_type"] = grainType,
                ["grain_sat"] = g.UserInput.Get(GrainSaturation),
                ["grain_power"] = g.UserInput.Get(GrainPower),
                ["shadows"] = g.UserInput.Get(Shadows),
                ["highs"] = g.UserInput.Get(Highlights),
                ["scale"] = g.UserInput.Get(Scale),
                ["sharpen"] = g.UserInput.Get(Sharpen),
                ["src_gamma"] = g.UserInput.Get(SourceGamma),
                ["seed"] = g.UserInput.Get(Seed),
            });
            g.CurrentMedia = g.CurrentMedia.WithPath([filmNode, 0]);
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
            PresetCleanDigitalSensor => new("Fine", 0.18f, 0.18f, 0.24f, 0.06f, 1.0f, 0, 1.0f),
            PresetSubtleModernCamera => new("Fine", 0.30f, 0.30f, 0.35f, 0.10f, 1.0f, 0, 1.0f),
            PresetClassic35mmFilm => new("Fine", 0.50f, 0.45f, 0.40f, 0.20f, 1.2f, 0, 1.0f),
            PresetVintagePrintStock => new("Fine", 0.42f, 0.38f, 0.34f, 0.18f, 1.45f, 0, 1.0f),
            Preset16mmDocumentary => new("Coarse", 0.46f, 0.50f, 0.42f, 0.18f, 1.2f, 0, 1.0f),
            PresetPushedNightStock => new("Coarse", 0.68f, 0.58f, 0.50f, 0.24f, 1.28f, 1, 1.0f),
            PresetGrittyHighIso => new("Coarse", 0.60f, 0.60f, 0.45f, 0.30f, 1.3f, 1, 1.0f),
            PresetCrunchyIndieFilm => new("Coarser", 0.52f, 0.66f, 0.48f, 0.28f, 1.4f, 1, 1.0f),
            PresetUniformCmosNoise => new("Fine Simple", 0.58f, 0.44f, 0.36f, 0.22f, 1.0f, 0, 1.0f),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(GrainType, config.GrainType);
        g.UserInput.Set(GrainSaturation, config.GrainSaturation);
        g.UserInput.Set(GrainPower, config.GrainPower);
        g.UserInput.Set(Shadows, config.Shadows);
        g.UserInput.Set(Highlights, config.Highlights);
        g.UserInput.Set(Scale, config.Scale);
        g.UserInput.Set(Sharpen, config.Sharpen);
        g.UserInput.Set(SourceGamma, config.SourceGamma);
    }

    private sealed record PresetConfig(
        string GrainType,
        float GrainSaturation,
        float GrainPower,
        float Shadows,
        float Highlights,
        float Scale,
        int Sharpen,
        float SourceGamma
    );
}
