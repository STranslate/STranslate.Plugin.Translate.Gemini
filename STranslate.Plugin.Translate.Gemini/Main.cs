using STranslate.Plugin.Translate.Gemini.View;
using STranslate.Plugin.Translate.Gemini.ViewModel;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Translate.Gemini;

public class Main : LlmTranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public override void SelectPrompt(Prompt? prompt)
    {
        base.SelectPrompt(prompt);

        // 保存到配置
        Settings.Prompts = [.. Prompts.Select(p => p.Clone())];
        Context.SaveSettingStorage<Settings>();
    }

    public override Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings, this);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "Requires you to identify automatically",
        LangEnum.ChineseSimplified => "Simplified Chinese",
        LangEnum.ChineseTraditional => "Traditional Chinese",
        LangEnum.Cantonese => "Cantonese",
        LangEnum.English => "English",
        LangEnum.Japanese => "Japanese",
        LangEnum.Korean => "Korean",
        LangEnum.French => "French",
        LangEnum.Spanish => "Spanish",
        LangEnum.Russian => "Russian",
        LangEnum.German => "German",
        LangEnum.Italian => "Italian",
        LangEnum.Turkish => "Turkish",
        LangEnum.PortuguesePortugal => "Portuguese",
        LangEnum.PortugueseBrazil => "Portuguese",
        LangEnum.Vietnamese => "Vietnamese",
        LangEnum.Indonesian => "Indonesian",
        LangEnum.Thai => "Thai",
        LangEnum.Malay => "Malay",
        LangEnum.Arabic => "Arabic",
        LangEnum.Hindi => "Hindi",
        LangEnum.MongolianCyrillic => "Mongolian",
        LangEnum.MongolianTraditional => "Mongolian",
        LangEnum.Khmer => "Central Khmer",
        LangEnum.NorwegianBokmal => "Norwegian Bokmål",
        LangEnum.NorwegianNynorsk => "Norwegian Nynorsk",
        LangEnum.Persian => "Persian",
        LangEnum.Swedish => "Swedish",
        LangEnum.Polish => "Polish",
        LangEnum.Dutch => "Dutch",
        LangEnum.Ukrainian => "Ukrainian",
        _ => "Requires you to identify automatically"
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "Requires you to identify automatically",
        LangEnum.ChineseSimplified => "Simplified Chinese",
        LangEnum.ChineseTraditional => "Traditional Chinese",
        LangEnum.Cantonese => "Cantonese",
        LangEnum.English => "English",
        LangEnum.Japanese => "Japanese",
        LangEnum.Korean => "Korean",
        LangEnum.French => "French",
        LangEnum.Spanish => "Spanish",
        LangEnum.Russian => "Russian",
        LangEnum.German => "German",
        LangEnum.Italian => "Italian",
        LangEnum.Turkish => "Turkish",
        LangEnum.PortuguesePortugal => "Portuguese",
        LangEnum.PortugueseBrazil => "Portuguese",
        LangEnum.Vietnamese => "Vietnamese",
        LangEnum.Indonesian => "Indonesian",
        LangEnum.Thai => "Thai",
        LangEnum.Malay => "Malay",
        LangEnum.Arabic => "Arabic",
        LangEnum.Hindi => "Hindi",
        LangEnum.MongolianCyrillic => "Mongolian",
        LangEnum.MongolianTraditional => "Mongolian",
        LangEnum.Khmer => "Central Khmer",
        LangEnum.NorwegianBokmal => "Norwegian Bokmål",
        LangEnum.NorwegianNynorsk => "Norwegian Nynorsk",
        LangEnum.Persian => "Persian",
        LangEnum.Swedish => "Swedish",
        LangEnum.Polish => "Polish",
        LangEnum.Dutch => "Dutch",
        LangEnum.Ukrainian => "Ukrainian",
        _ => "Requires you to identify automatically"
    };

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();

        Settings.Prompts.ForEach(Prompts.Add);
    }

    public override void Dispose() => _viewModel?.Dispose();

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        if (GetSourceLanguage(request.SourceLang) is not string sourceStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedSourceLang"));
            return;
        }
        if (GetTargetLanguage(request.TargetLang) is not string targetStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        // 选择模型
        var model = Settings.Model.Trim();
        model = string.IsNullOrEmpty(model) ? "gemini-flash-latest" : model;

        UriBuilder uriBuilder = new(Settings.Url);
        // 如果路径不是有效的API路径结尾，使用默认路径
        if (uriBuilder.Path == "/")
            uriBuilder.Path = $"/v1beta/models/{model}:streamGenerateContent";

        // 加上 alt=sse 才是每个流传输结果为完整json，方便解析判断是否为最后一条
        uriBuilder.Query = $"alt=sse&key={Settings.ApiKey}";

        // 替换Prompt关键字
        var messages = (Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"))
            .Clone()
            .Items;
        messages.ToList()
            .ForEach(item =>
                item.Content = item.Content
                .Replace("$source", sourceStr)
                .Replace("$target", targetStr)
                .Replace("$content", request.Text)
                );

        // 温度限定
        var temperature = Math.Clamp(Settings.Temperature, 0, 2);

        var thinkingBudget = (int)Math.Clamp(Settings.ThinkingBudget, -1, 24576);

        var content = new
        {
            contents = messages.Select(e => new { role = e.Role, parts = new[] { new { text = e.Content } } }),
            generationConfig = new
            {
                temperature,
                thinkingConfig = new { thinkingBudget }
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE"},         //骚扰内容。
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE"},        //仇恨言论和内容。
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE"},  //露骨色情内容。
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE"},  //危险内容。
            }
        };

        await Context.HttpService.StreamPostAsync(uriBuilder.Uri.ToString(), content, msg =>
        {
            if (string.IsNullOrEmpty(msg?.Trim()))
                return;

            var preprocessString = msg.Replace("data:", "").Trim();

            var parsedData = JsonNode.Parse(preprocessString);

            if (parsedData is null)
                return;

            // 替换原有的 FirstOrDefault 用法为 JsonArray 索引访问
            var candidatesNode = parsedData["candidates"] as JsonArray;
            var firstCandidate = candidatesNode is not null && candidatesNode.Count > 0 ? candidatesNode[0] : null;
            var contentNode = firstCandidate?["content"];
            var partsNode = contentNode?["parts"] as JsonArray;
            var firstPart = partsNode is not null && partsNode.Count > 0 ? partsNode[0] : null;
            var contentValue = firstPart?["text"]?.ToString();

            if (string.IsNullOrEmpty(contentValue))
                return;

            // 结束时处理最后多余的\n
            if ((firstCandidate?["finishReason"]?.ToString() ?? "") == "STOP" && contentValue.EndsWith('\n'))
                contentValue = contentValue.TrimEnd('\n');

            result.Text += contentValue;
        }, cancellationToken: cancellationToken);
    }
}
