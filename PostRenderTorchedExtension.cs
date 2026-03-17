using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.IO;

namespace PostRenderTorched;

public class PostRenderTorchedExtension : Extension
{
    public double StepPriority = 9.9f;
    public const string FeatureFlagPostRender = "feature_flag_post_render_torched";
    public static T2IParamGroup PostRenderGroup;
    public const string FilmGrainPresetNone = "None";
    public const string FilmGrainPresetSubtleModernCamera = "Subtle Modern Camera (light, best)";
    public const string FilmGrainPresetClassic35mmFilm = "Classic 35mm Film (medium, visible grain, warm color variation)";
    public const string FilmGrainPresetGrittyHighIso = "Gritty / High ISO (strong, moody, documentary, low-light aesthetics)";
    public const string FilmGrainPresetCleanDigitalSensor = "Clean Digital Sensor (ultra light, polished, almost invisible)";
    public const string FilmGrainPresetVintagePrintStock = "Vintage Print Stock (medium, soft clumping, less clinical)";
    public const string FilmGrainPreset16mmDocumentary = "16mm Documentary (medium-strong, textured, analog reportage)";
    public const string FilmGrainPresetPushedNightStock = "Pushed Night Stock (strong, colorful shadow noise, low-light cinema)";
    public const string FilmGrainPresetCrunchyIndieFilm = "Crunchy Indie Film (strong, chunky, stylized texture)";
    public const string FilmGrainPresetUniformCmosNoise = "Uniform CMOS Noise (medium, cleaner pattern, digital-video feel)";
    public const string VignettePresetNone = "None";
    public const string VignettePresetCenteredNatural = "Centered Natural (light, photographic edge darkening)";
    public const string VignettePresetClassicPortrait = "Classic Portrait (medium, flattering centered focus)";
    public const string VignettePresetCinematicSpotlight = "Cinematic Spotlight (strong, moody subject isolation)";
    public const string VignettePresetOffCenterDrama = "Off-Center Drama (medium, asymmetric framing emphasis)";
    public const string DepthBlurPresetNone = "None";
    public const string DepthBlurPresetPortraitSeparation = "Portrait Separation (natural, forgiving subject focus)";
    public const string DepthBlurPresetMacroProduct = "Macro Product (strong, close-up subject isolation)";
    public const string DepthBlurPresetCinematicRackFocus = "Cinematic Rack Focus (selective, pronounced focal plane)";
    public const string DepthBlurPresetMiniatureTiltShift = "Miniature / Tilt-Shift Feel (stylized, fake scale blur)";
    public const string DepthBlurPresetSoftAtmospheric = "Soft Atmospheric (gentle, dreamy depth falloff)";
    public const string RadialBlurPresetNone = "None";
    public const string RadialBlurPresetSubtleZoom = "Subtle Zoom (light, centered motion energy)";
    public const string RadialBlurPresetHeroPushIn = "Hero Push-In (medium, cinematic center emphasis)";
    public const string RadialBlurPresetImpactBurst = "Impact Burst (strong, stylized action streaking)";
    public const string RadialBlurPresetOffAxisWhip = "Off-Axis Whip (medium-strong, directional motion feel)";

    #region FilmGrain
    public const string FILM_GRAIN_PREFIX = "[Grain Torched]";
    public const string NodeNameFilmGrainTorched = "ProPostFilmGrainTorched";
    public T2IRegisteredParam<string> FGPreset;
    public T2IRegisteredParam<bool> FGGrayScale;
    public T2IRegisteredParam<string> FGGrainType;
    public T2IRegisteredParam<float> FGGrainSat;
    public T2IRegisteredParam<float> FGGrainPower;
    public T2IRegisteredParam<float> FGShadows;
    public T2IRegisteredParam<float> FGHighs;
    public T2IRegisteredParam<float> FGScale;
    public T2IRegisteredParam<int> FGSharpen;
    public T2IRegisteredParam<float> FGSrcGamma;
    public T2IRegisteredParam<long> FGSeed;
    #endregion

    #region Vignette
    public const string VIGNETTE_PREFIX = "[Vig Torched]";
    public const string NodeNameVignetteTorched = "ProPostVignetteTorched";
    public T2IRegisteredParam<string> VPreset;
    public T2IRegisteredParam<float> VStrength;
    public T2IRegisteredParam<float> VPosX;
    public T2IRegisteredParam<float> VPosY;
    #endregion

    #region Lut
    public const string LUT_PREFIX = "[LUT Torched]";
    public const string NodeNameLutTorched = "ProPostApplyLUTTorched";
    public List<string> LutModels = [];
    public T2IRegisteredParam<float> LutStrength;
    public T2IRegisteredParam<bool> LutLogSpace;
    public T2IRegisteredParam<string> LutName;
    #endregion

    #region RadialBlur
    public const string R_BLUR_PREFIX = "[R. Blur Torched]";
    public const string NodeNameRadialBlurTorched = "ProPostRadialBlurTorched";
    public T2IRegisteredParam<string> RBPreset;
    public T2IRegisteredParam<float> RBStrength;
    public T2IRegisteredParam<float> RBPosX;
    public T2IRegisteredParam<float> RBPosY;
    public T2IRegisteredParam<float> RBFocusSpread;
    public T2IRegisteredParam<int> RBSteps;
    #endregion

    #region DMBlur
    public const string DM_BLUR_PREFIX = "[DM Blur Torched]";
    public const string NodeNameDMBlurTorched = "ProPostDepthMapBlurTorched";
    public const string NodeNameDepthMap = "DepthAnythingPreprocessor";
    public List<string> DepthModels = ["depth_anything_vitl14.pth", "depth_anything_vitb14.pth", "depth_anything_vits14.pth"];
    public T2IRegisteredParam<string> DMPreset;
    public T2IRegisteredParam<string> DMPreProcessorResolution;
    public T2IRegisteredParam<string> DMPreProcessorModelName;
    public T2IRegisteredParam<float> DMBlurStrength;
    public T2IRegisteredParam<float> DMFocalDepth;
    public T2IRegisteredParam<float> DMFocusSpread;
    public T2IRegisteredParam<int> DMSteps;
    public T2IRegisteredParam<float> DMFocalRange;
    public T2IRegisteredParam<int> DMMaskBlur;
    #endregion

    public override void OnPreInit()
    {
        ScriptFiles.Add("assets/pro_post.js");
    }

    public override void OnInit()
    {
        Logs.Info("PostRender Torched Extension initializing...");

        InstallComfyUINodes();
        RegisterParameters();

        # region Film Grain
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(FGGrayScale, out bool grayScale))
            {
                RequireTorchedNodes(g);
                ApplyFilmGrainPreset(g);
                string filmNode = g.CreateNode(NodeNameFilmGrainTorched, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["gray_scale"] = grayScale,
                    ["grain_type"] = g.UserInput.Get(FGGrainType),
                    ["grain_sat"] = g.UserInput.Get(FGGrainSat),
                    ["grain_power"] = g.UserInput.Get(FGGrainPower),
                    ["shadows"] = g.UserInput.Get(FGShadows),
                    ["highs"] = g.UserInput.Get(FGHighs),
                    ["scale"] = g.UserInput.Get(FGScale),
                    ["sharpen"] = g.UserInput.Get(FGSharpen),
                    ["src_gamma"] = g.UserInput.Get(FGSrcGamma),
                    ["seed"] = g.UserInput.Get(FGSeed),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([filmNode, 0]);
            }
        }, StepPriority);
        StepPriority += 0.01f;
        #endregion

        #region Vignette
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(VStrength, out float vStr))
            {
                RequireTorchedNodes(g);
                ApplyVignettePreset(g);
                string vigNode = g.CreateNode(NodeNameVignetteTorched, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["intensity"] = g.UserInput.Get(VStrength),
                    ["center_x"] = g.UserInput.Get(VPosX),
                    ["center_y"] = g.UserInput.Get(VPosY),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([vigNode, 0]);
            }
        }, StepPriority);
        StepPriority += 0.01f;
        #endregion

        #region Depth Map Blur
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(DMBlurStrength, out float bStr))
            {
                RequireTorchedNodes(g);
                ApplyDepthMapBlurPreset(g);
                string depthAnything = g.CreateNode(NodeNameDepthMap, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["resolution"] = Int32.Parse(g.UserInput.Get(DMPreProcessorResolution)),
                    ["ckpt_name"] = g.UserInput.Get(DMPreProcessorModelName),
                });
                JArray map = [depthAnything, 0];
                string blurNode = g.CreateNode(NodeNameDMBlurTorched, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["depth_map"] = map,
                    ["blur_strength"] = g.UserInput.Get(DMBlurStrength),
                    ["focal_depth"] = g.UserInput.Get(DMFocalDepth),
                    ["focus_spread"] = g.UserInput.Get(DMFocusSpread),
                    ["steps"] = g.UserInput.Get(DMSteps),
                    ["focal_range"] = g.UserInput.Get(DMFocalRange),
                    ["mask_blur"] = g.UserInput.Get(DMMaskBlur),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([blurNode, 0]);
            }
        }, StepPriority);
        StepPriority += 0.01f;
        #endregion

        #region Radial Blur
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(RBStrength, out float bStr))
            {
                RequireTorchedNodes(g);
                ApplyRadialBlurPreset(g);
                string blurNode = g.CreateNode(NodeNameRadialBlurTorched, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["blur_strength"] = g.UserInput.Get(RBStrength),
                    ["center_x"] = g.UserInput.Get(RBPosX),
                    ["center_y"] = g.UserInput.Get(RBPosY),
                    ["focus_spread"] = g.UserInput.Get(RBFocusSpread),
                    ["steps"] = g.UserInput.Get(RBSteps),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([blurNode, 0]);
            }
        }, StepPriority);
        StepPriority += 0.01f;
        #endregion

        #region Lut
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(LutName, out string lName))
            {
                RequireTorchedNodes(g);
                string lutNode = g.CreateNode(NodeNameLutTorched, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["lut_name"] = lName,
                    ["log"] = g.UserInput.Get(LutLogSpace),
                    ["strength"] = g.UserInput.Get(LutStrength),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([lutNode, 0]);
            }
        }, StepPriority);
        StepPriority += 0.01f;
        #endregion
    }

    private void InstallComfyUINodes()
    {
        string extensionLutPath = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/SwarmUI-PostRenderTorched/luts");
        string modelLutPath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ActualModelRoot, "luts");
        Directory.CreateDirectory(extensionLutPath);
        Directory.CreateDirectory(modelLutPath);
        ComfyUISelfStartBackend.FoldersToForwardInComfyPath.Add($"{extensionLutPath};luts");

        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameFilmGrainTorched] = FeatureFlagPostRender;
        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameVignetteTorched] = FeatureFlagPostRender;
        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameDMBlurTorched] = FeatureFlagPostRender;
        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameRadialBlurTorched] = FeatureFlagPostRender;
        ComfyUIBackendExtension.NodeToFeatureMap[NodeNameLutTorched] = FeatureFlagPostRender;

        var nodeFolder = Path.GetFullPath(Path.Join(FilePath, "comfy_node"));
        ComfyUISelfStartBackend.CustomNodePaths.Add(nodeFolder);
        Logs.Init($"PostRender Torched: added {nodeFolder} to ComfyUI CustomNodePaths");
        ComfyUIBackendExtension.FeaturesSupported.UnionWith([FeatureFlagPostRender]);
        ComfyUIBackendExtension.FeaturesDiscardIfNotFound.UnionWith([FeatureFlagPostRender]);

        T2IParamTypes.ConcatDropdownValsClean(ref LutModels,
            [.. Directory.EnumerateFiles(extensionLutPath, "*.cube", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(extensionLutPath, f)).OrderBy(f => f)]
        );
        T2IParamTypes.ConcatDropdownValsClean(ref LutModels,
            [.. Directory.EnumerateFiles(modelLutPath, "*.cube", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(modelLutPath, f)).OrderBy(f => f)]
        );

        ComfyUIBackendExtension.RawObjectInfoParsers.Add(rawObjectInfo =>
        {
            rawObjectInfo.TryGetValue(NodeNameLutTorched, out JToken lutNode);
            if (lutNode != null)
            {
                T2IParamTypes.ConcatDropdownValsClean(ref LutModels, lutNode["input"]["required"]["lut_name"][0].Select(m => $"{m}"));
            }
        });
    }

    private void RegisterParameters()
    {
        double orderPriorityCtr = 9.1;

        PostRenderGroup = new(
            Name: "Post Render Torched",
            Toggles: false,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr
        );

        orderPriorityCtr += 0.1f;

        RegisterParametersFilmGrain(ref orderPriorityCtr);
        RegisterParametersVignette(ref orderPriorityCtr);
        RegisterParametersDepthMapBlur(ref orderPriorityCtr);
        RegisterParametersRadialBlur(ref orderPriorityCtr);
        RegisterParametersLUT(ref orderPriorityCtr);
    }

    private void RegisterParametersFilmGrain(ref double orderPriorityCtr)
    {
        T2IParamGroup GrainGroup = new(
            Name: "Film Grain Torched",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr,
            Parent: PostRenderGroup
        );

        orderPriorityCtr += 0.1f;
        int orderCounter = 0;

        FGPreset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Film Grain Preset",
            Description: "Quick starting points for film grain. Set to None to use the manual controls below.",
            Default: FilmGrainPresetNone,
            IgnoreIf: FilmGrainPresetNone,
            GetValues: _ => [
                FilmGrainPresetNone,
                FilmGrainPresetCleanDigitalSensor,
                FilmGrainPresetSubtleModernCamera,
                FilmGrainPresetClassic35mmFilm,
                FilmGrainPresetVintagePrintStock,
                FilmGrainPreset16mmDocumentary,
                FilmGrainPresetPushedNightStock,
                FilmGrainPresetGrittyHighIso,
                FilmGrainPresetCrunchyIndieFilm,
                FilmGrainPresetUniformCmosNoise
            ],
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGGrayScale = T2IParamTypes.Register<bool>(new T2IParamType(
            Name: "[Torched] Film Grain Gray Scale",
            Description: "Enables grayscale mode. If true, the output will be in grayscale",
            Default: "false",
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGGrainType = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Film Grain Type",
            Description: "Sets the grain type",
            Default: "Fine Simple",
            GetValues: _ => ["Fine", "Fine Simple", "Coarse", "Coarser"],
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGGrainSat = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Saturation",
            Description: "Grain color saturation",
            Default: "0.5",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGGrainPower = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Power",
            Description: "Overall intensity of the grain effect",
            Default: "0.7",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGShadows = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Shadows",
            Description: "Intensity of grain in the shadows",
            Default: "0.2",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGHighs = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Highlights",
            Description: "Intensity of the grain in the highlights",
            Default: "0.2",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGScale = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Scale",
            Description: "Image scaling ratio. Scales the image before applying grain and scales back afterwards",
            Default: "1.0",
            Min: 0.0, Max: 10.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGSharpen = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "[Torched] Film Grain Sharpen",
            Description: "Number of sharpening passes",
            Default: "0",
            Min: 0, Max: 10,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGSrcGamma = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Film Grain Source Gamma",
            Description: "Gamma compensation applied to the input",
            Default: "1.0",
            Min: 0.0, Max: 10.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        FGSeed = T2IParamTypes.Register<long>(new T2IParamType(
            Name: "[Torched] Film Grain Seed",
            Description: "Random seed used for the film grain pattern",
            Default: "-1",
            Min: -1, Max: 1000, Step: 1,
            ViewType: ParamViewType.SEED,
            Group: GrainGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));
    }

    private void ApplyFilmGrainPreset(WorkflowGenerator g)
    {
        if (!g.UserInput.TryGet(FGPreset, out string preset) || preset == FilmGrainPresetNone)
        {
            return;
        }

        FilmGrainPresetConfig config = preset switch
        {
            FilmGrainPresetCleanDigitalSensor => new("Fine", 0.18f, 0.18f, 0.24f, 0.06f, 1.0f, 0, 1.0f),
            FilmGrainPresetSubtleModernCamera => new("Fine", 0.30f, 0.30f, 0.35f, 0.10f, 1.0f, 0, 1.0f),
            FilmGrainPresetClassic35mmFilm => new("Fine", 0.50f, 0.45f, 0.40f, 0.20f, 1.2f, 0, 1.0f),
            FilmGrainPresetVintagePrintStock => new("Fine", 0.42f, 0.38f, 0.34f, 0.18f, 1.45f, 0, 1.0f),
            FilmGrainPreset16mmDocumentary => new("Coarse", 0.46f, 0.50f, 0.42f, 0.18f, 1.2f, 0, 1.0f),
            FilmGrainPresetPushedNightStock => new("Coarse", 0.68f, 0.58f, 0.50f, 0.24f, 1.28f, 1, 1.0f),
            FilmGrainPresetGrittyHighIso => new("Coarse", 0.60f, 0.60f, 0.45f, 0.30f, 1.3f, 1, 1.0f),
            FilmGrainPresetCrunchyIndieFilm => new("Coarser", 0.52f, 0.66f, 0.48f, 0.28f, 1.4f, 1, 1.0f),
            FilmGrainPresetUniformCmosNoise => new("Fine Simple", 0.58f, 0.44f, 0.36f, 0.22f, 1.0f, 0, 1.0f),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(FGGrainType, config.GrainType);
        g.UserInput.Set(FGGrainSat, config.GrainSaturation);
        g.UserInput.Set(FGGrainPower, config.GrainPower);
        g.UserInput.Set(FGShadows, config.Shadows);
        g.UserInput.Set(FGHighs, config.Highlights);
        g.UserInput.Set(FGScale, config.Scale);
        g.UserInput.Set(FGSharpen, config.Sharpen);
        g.UserInput.Set(FGSrcGamma, config.SourceGamma);
    }

    private sealed record FilmGrainPresetConfig(
        string GrainType,
        float GrainSaturation,
        float GrainPower,
        float Shadows,
        float Highlights,
        float Scale,
        int Sharpen,
        float SourceGamma
    );

    private void ApplyVignettePreset(WorkflowGenerator g)
    {
        if (!g.UserInput.TryGet(VPreset, out string preset) || preset == VignettePresetNone)
        {
            return;
        }

        VignettePresetConfig config = preset switch
        {
            VignettePresetCenteredNatural => new(0.22f, 0.50f, 0.50f),
            VignettePresetClassicPortrait => new(0.42f, 0.50f, 0.50f),
            VignettePresetCinematicSpotlight => new(0.72f, 0.50f, 0.46f),
            VignettePresetOffCenterDrama => new(0.55f, 0.42f, 0.44f),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(VStrength, config.Intensity);
        g.UserInput.Set(VPosX, config.CenterX);
        g.UserInput.Set(VPosY, config.CenterY);
    }

    private sealed record VignettePresetConfig(
        float Intensity,
        float CenterX,
        float CenterY
    );

    private void RegisterParametersVignette(ref double orderPriorityCtr)
    {
        T2IParamGroup VigGroup = new(
            Name: "Vignette Torched",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr,
            Parent: PostRenderGroup
        );

        orderPriorityCtr += 0.1f;
        int orderCounter = 0;

        VPreset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Vignette Preset",
            Description: "Quick starting points for vignette. Set to None to use the manual controls below.",
            Default: VignettePresetNone,
            IgnoreIf: VignettePresetNone,
            GetValues: _ => [
                VignettePresetNone,
                VignettePresetCenteredNatural,
                VignettePresetClassicPortrait,
                VignettePresetCinematicSpotlight,
                VignettePresetOffCenterDrama
            ],
            Group: VigGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        VStrength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Vignette Strength",
            Description: "Vignette strength, lower is weaker",
            Default: "0.2",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: VigGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        VPosX = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Vignette X Position",
            Description: "Vignette X position, 0 is left, 0.5 is center, 1 is right",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: VigGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        VPosY = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Vignette Y Position",
            Description: "Vignette Y position, 0 is top, 0.5 is center, 1 is bottom",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: VigGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));
    }

    private void RegisterParametersDepthMapBlur(ref double orderPriorityCtr)
    {
        T2IParamGroup DMBlurGroup = new(
            Name: "Depth Map Blur Torched",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr,
            Parent: PostRenderGroup
        );

        orderPriorityCtr += 0.1f;
        int orderCounter = 0;

        DMPreset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Preset",
            Description: "Quick starting points for depth-based blur. Set to None to use the manual controls below.",
            Default: DepthBlurPresetNone,
            IgnoreIf: DepthBlurPresetNone,
            GetValues: _ => [
                DepthBlurPresetNone,
                DepthBlurPresetPortraitSeparation,
                DepthBlurPresetMacroProduct,
                DepthBlurPresetCinematicRackFocus,
                DepthBlurPresetMiniatureTiltShift,
                DepthBlurPresetSoftAtmospheric
            ],
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMPreProcessorResolution = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Resolution",
            Description: "The resolution of the depth map (1024 suggested)",
            Default: "1024",
            GetValues: _ => ["256", "512", "1024", "2048"],
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMPreProcessorModelName = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Model",
            Description: "The model used for the depth map image\nModels will download automatically as needed",
            Default: DepthModels[0],
            GetValues: _ => DepthModels,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMBlurStrength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Strength",
            Description: "The intensity of the blur",
            Default: "64.0",
            Min: 0.0, Max: 256.0, Step: 1.0,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMFocalDepth = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Focal Depth",
            Description: "The focal depth of the blur. 1.0 is the closest, 0.0 is the farthest",
            Default: "1.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMFocusSpread = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Focus Spread",
            Description: "The spread of the area of focus. A larger value makes more of the image sharp",
            Default: "1.0",
            Min: 1.0, Max: 8.0, Step: 0.1,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMSteps = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Steps",
            Description: "The number of steps to use when blurring the image. Higher numbers are slower",
            Default: "5",
            Min: 1, Max: 32, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMFocalRange = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Focal Range",
            Description: "1.0 means all areas clear, 0.0 means only focal point is clear",
            Default: "0.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        DMMaskBlur = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "[Torched] Depth Map Blur Mask Blur",
            Description: "Mask blur strength (1 to 127).1 means no blurring",
            Default: "1",
            Min: 1, Max: 127, Step: 2,
            ViewType: ParamViewType.SLIDER,
            Group: DMBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));
    }

    private void ApplyDepthMapBlurPreset(WorkflowGenerator g)
    {
        if (!g.UserInput.TryGet(DMPreset, out string preset) || preset == DepthBlurPresetNone)
        {
            return;
        }

        DepthMapBlurPresetConfig config = preset switch
        {
            DepthBlurPresetPortraitSeparation => new(56.0f, 0.85f, 2.4f, 6, 0.22f, 11),
            DepthBlurPresetMacroProduct => new(80.0f, 0.92f, 2.0f, 7, 0.12f, 9),
            DepthBlurPresetCinematicRackFocus => new(96.0f, 0.82f, 1.7f, 8, 0.08f, 7),
            DepthBlurPresetMiniatureTiltShift => new(110.0f, 0.50f, 1.4f, 8, 0.04f, 5),
            DepthBlurPresetSoftAtmospheric => new(44.0f, 0.80f, 3.0f, 6, 0.28f, 15),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(DMBlurStrength, config.BlurStrength);
        g.UserInput.Set(DMFocalDepth, config.FocalDepth);
        g.UserInput.Set(DMFocusSpread, config.FocusSpread);
        g.UserInput.Set(DMSteps, config.Steps);
        g.UserInput.Set(DMFocalRange, config.FocalRange);
        g.UserInput.Set(DMMaskBlur, config.MaskBlur);
    }

    private sealed record DepthMapBlurPresetConfig(
        float BlurStrength,
        float FocalDepth,
        float FocusSpread,
        int Steps,
        float FocalRange,
        int MaskBlur
    );

    private void RegisterParametersRadialBlur(ref double orderPriorityCtr)
    {
        T2IParamGroup rBlurGroup = new(
            Name: "Radial Blur Torched",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr,
            Parent: PostRenderGroup
        );
        
        orderPriorityCtr += 0.1f;
        int orderCounter = 0;

        RBPreset = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] Radial Blur Preset",
            Description: "Quick starting points for radial blur. Set to None to use the manual controls below.",
            Default: RadialBlurPresetNone,
            IgnoreIf: RadialBlurPresetNone,
            GetValues: _ => [
                RadialBlurPresetNone,
                RadialBlurPresetSubtleZoom,
                RadialBlurPresetHeroPushIn,
                RadialBlurPresetImpactBurst,
                RadialBlurPresetOffAxisWhip
            ],
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        RBStrength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Radial Blur Strength",
            Description: "Blur Strength, lower is weaker",
            Default: "64",
            Min: 0, Max: 256, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        RBPosX = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Radial Blur X Position",
            Description: "Blur X position, 0 is left, 0.5 is center, 1 is right",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        RBPosY = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Radial Blur Y Position",
            Description: "Blur Y position, 0 is top, 0.5 is center, 1 is bottom",
            Default: "0.5",
            Min: 0, Max: 1, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        RBFocusSpread = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] Radial Blur Focus Spread",
            Description: "Spread of the area of focus, higher is sharper",
            Default: "1",
            Min: 0.1, Max: 8.0, Step: 0.1,
            ViewType: ParamViewType.SLIDER,
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        RBSteps = T2IParamTypes.Register<int>(new T2IParamType(
            Name: "[Torched] Radial Blur Steps",
            Description: "Number of steps to use when bluring image, higher is slower",
            Default: "5",
            Min: 1, Max: 32, Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: rBlurGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));
    }

    private void ApplyRadialBlurPreset(WorkflowGenerator g)
    {
        if (!g.UserInput.TryGet(RBPreset, out string preset) || preset == RadialBlurPresetNone)
        {
            return;
        }

        RadialBlurPresetConfig config = preset switch
        {
            RadialBlurPresetSubtleZoom => new(48.0f, 0.50f, 0.50f, 1.5f, 6),
            RadialBlurPresetHeroPushIn => new(84.0f, 0.50f, 0.50f, 1.2f, 7),
            RadialBlurPresetImpactBurst => new(128.0f, 0.50f, 0.50f, 1.0f, 8),
            RadialBlurPresetOffAxisWhip => new(104.0f, 0.38f, 0.42f, 1.0f, 8),
            _ => null
        };
        if (config is null)
        {
            return;
        }
        g.UserInput.Set(RBStrength, config.BlurStrength);
        g.UserInput.Set(RBPosX, config.CenterX);
        g.UserInput.Set(RBPosY, config.CenterY);
        g.UserInput.Set(RBFocusSpread, config.FocusSpread);
        g.UserInput.Set(RBSteps, config.Steps);
    }

    private sealed record RadialBlurPresetConfig(
        float BlurStrength,
        float CenterX,
        float CenterY,
        float FocusSpread,
        int Steps
    );

    private void RegisterParametersLUT(ref double orderPriorityCtr)
    {
        T2IParamGroup lutGroup = new(
            Name: "LUT Torched",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: orderPriorityCtr,
            Parent: PostRenderGroup
        );

        orderPriorityCtr += 0.1f;
        int orderCounter = 0;

        LutName = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "[Torched] LUT Name",
            Description: "LUT to apply to the image.\nTo add new LUTs place them in SwarmUI/Models/luts",
            Default: "None",
            IgnoreIf: "None",
            GetValues: _ => LutModels,
            Group: lutGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        LutStrength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "[Torched] LUT Strength",
            Description: "The strength of the LUT effect",
            Default: "1.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: lutGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));

        LutLogSpace = T2IParamTypes.Register<bool>(new T2IParamType(
            Name: "[Torched] LUT LOG Space",
            Description: "If true, the image is processed in LOG color space",
            Default: "false",
            ViewType: ParamViewType.NORMAL,
            Group: lutGroup,
            FeatureFlag: FeatureFlagPostRender,
            OrderPriority: orderCounter++
        ));
    }

    private static void RequireTorchedNodes(WorkflowGenerator g)
    {
        if (!g.Features.Contains(FeatureFlagPostRender))
        {
            throw new SwarmUserErrorException("Post Render (Torched) nodes are not installed");
        }
    }
}
