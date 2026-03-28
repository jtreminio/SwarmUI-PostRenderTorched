using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.IO;

namespace PostRenderTorched;

internal sealed class LutFeature
{
    public const string NodeName = "ProPostApplyLUT";
    private List<string> LutModels = [];
    private List<string> LocalLutModels = [];
    private List<string> BackendLutModels = [];
    private string ExtensionLutPath;
    private string ModelLutPath;
    private T2IRegisteredParam<float> Strength;
    private T2IRegisteredParam<bool> LogSpace;
    private T2IRegisteredParam<string> Name;

    public LutFeature()
    {
        Program.ModelRefreshEvent += RefreshLocalModels;
        ComfyUIBackendExtension.RawObjectInfoParsers.Add(RefreshModelsFromRawObjectInfo);
    }

    public void RegisterFeature(T2IParamGroup group, ref int featurePriority)
    {
        ComfyUIBackendExtension.NodeToFeatureMap[NodeName] = PostRenderTorchedExtension.FeatureFlag;
        T2IParamTypes.ParameterRemaps["lutlutstrength"] = "lutstrength";
        InitializeModelSources();

        T2IParamGroup lutGroup = new(
            Name: "LUT",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: featurePriority,
            Parent: group
        );

        featurePriority += 1;
        int orderCounter = 0;

        Name = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "LUT Name",
            Description: "LUT to apply to the image.\nTo add new LUTs place them in SwarmUI/Models/luts",
            Default: "None",
            IgnoreIf: "None",
            GetValues: _ => LutModels,
            Group: lutGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        Strength = T2IParamTypes.Register<float>(new T2IParamType(
            Name: "LUT Strength",
            Description: "The strength of the LUT effect",
            Default: "1.0",
            Min: 0.0, Max: 1.0, Step: 0.01,
            ViewType: ParamViewType.SLIDER,
            Group: lutGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));

        LogSpace = T2IParamTypes.Register<bool>(new T2IParamType(
            Name: "LUT LOG Space",
            Description: "If true, the image is processed in LOG color space",
            Default: "false",
            ViewType: ParamViewType.NORMAL,
            Group: lutGroup,
            FeatureFlag: PostRenderTorchedExtension.FeatureFlag,
            OrderPriority: orderCounter++
        ));
    }

    private void InitializeModelSources()
    {
        ExtensionLutPath = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/SwarmUI-PostRenderTorched/luts");
        ModelLutPath = Utilities.CombinePathWithAbsolute(Program.ServerSettings.Paths.ActualModelRoot, "luts");
        Directory.CreateDirectory(ExtensionLutPath);
        Directory.CreateDirectory(ModelLutPath);
        ComfyUISelfStartBackend.FoldersToForwardInComfyPath.Add($"luts;{ExtensionLutPath}");
        RefreshLocalModels();
    }

    public void RegisterWorkflowStep(ref double stepPriorityCtr)
    {
        WorkflowGenerator.AddStep(g =>
        {
            if (g.UserInput.TryGet(Name, out string lutName))
            {
                string lutNode = g.CreateNode(NodeName, new JObject
                {
                    ["image"] = g.CurrentMedia.Path,
                    ["lut_name"] = lutName,
                    ["log"] = g.UserInput.Get(LogSpace),
                    ["strength"] = g.UserInput.Get(Strength),
                });
                g.CurrentMedia = g.CurrentMedia.WithPath([lutNode, 0]);
            }
        }, stepPriorityCtr);
        stepPriorityCtr += 0.01f;
    }

    private void RefreshLocalModels()
    {
        if (string.IsNullOrWhiteSpace(ExtensionLutPath) || string.IsNullOrWhiteSpace(ModelLutPath))
        {
            return;
        }
        LocalLutModels =
        [
            .. EnumerateLutFiles(ExtensionLutPath).OrderBy(f => f),
            .. EnumerateLutFiles(ModelLutPath).OrderBy(f => f)
        ];
        RebuildLutModels();
    }

    public void RefreshModelsFromRawObjectInfo(JObject rawObjectInfo)
    {
        rawObjectInfo.TryGetValue(NodeName, out JToken lutNode);
        JToken lutNameValues = lutNode?["input"]?["required"]?["lut_name"]?[0];
        if (lutNameValues is not JArray lutNameArray)
        {
            return;
        }
        BackendLutModels = [.. lutNameArray.Select(m => $"{m}")];
        RebuildLutModels();
    }

    private IEnumerable<string> EnumerateLutFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(f => string.Equals(Path.GetExtension(f), ".cube", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(rootPath, f));
    }

    public void RebuildLutModels()
    {
        List<string> lutModels = [];
        T2IParamTypes.ConcatDropdownValsClean(ref lutModels, LocalLutModels);
        T2IParamTypes.ConcatDropdownValsClean(ref lutModels, BackendLutModels);
        LutModels = lutModels;
    }
}
