using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace STranslate.Plugin.Translate.Gemini.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;
    private bool _isUpdating = false;
    public Main Main { get; }

    public SettingsViewModel(IPluginContext context, Settings settings, Main main)
    {
        _context = context;
        _settings = settings;
        Main = main;

        Url = _settings.Url;
        ApiKey = _settings.ApiKey;
        Model = _settings.Model;
        Models = new ObservableCollection<string>(_settings.Models);
        Temperature = _settings.Temperature;
        ThinkingBudget = _settings.ThinkingBudget;
        _thinkingBudgetIndex = Array.IndexOf(_thinkingBudgets, ThinkingBudget);
        if (_thinkingBudgetIndex == -1) _thinkingBudgetIndex = 0;

        PropertyChanged += OnPropertyChanged;
        Models.CollectionChanged += OnModelsCollectionChanged;
    }

    private void OnModelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or
                       NotifyCollectionChangedAction.Remove or
                       NotifyCollectionChangedAction.Replace)
        {
            _settings.Models = [.. Models];
            _context.SaveSettingStorage<Settings>();
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            case nameof(Url):
                _settings.Url = Url;
                break;
            case nameof(Model):
                _settings.Model = Model ?? string.Empty;
                break;
            case nameof(Temperature):
                // 舍入到一位小数，避免浮点精度问题
                _settings.Temperature = Math.Round(Temperature, 1);
                break;
            case nameof(ThinkingBudget):
                _settings.ThinkingBudget = Math.Round(ThinkingBudget, 0);
                break;

            case nameof(ThinkingBudgetIndex):
                ThinkingBudget = _thinkingBudgets[ThinkingBudgetIndex];
                break;

            default:
                return;
        }
        _context.SaveSettingStorage<Settings>();
    }

    [ObservableProperty] public partial string ValidateResult { get; set; } = string.Empty;
    [ObservableProperty] public partial string Url { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial string? Model { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> Models { get; set; }
    [ObservableProperty] public partial double Temperature { get; set; }
    [ObservableProperty] public partial double ThinkingBudget { get; set; }
    [ObservableProperty] private int _thinkingBudgetIndex;
    private readonly double[] _thinkingBudgets = [-1, 0, 1024, 5000, 10000, 20000];

    [RelayCommand]
    private void AddModel(string model)
    {
        if (_isUpdating || string.IsNullOrWhiteSpace(model) || Models.Contains(model))
            return;

        using var _ = new UpdateGuard(this);

        Models.Add(model);
        Model = model;
    }

    [RelayCommand]
    private void DeleteModel(string model)
    {
        if (_isUpdating || !Models.Contains(model))
            return;

        using var _ = new UpdateGuard(this);

        if (Model == model)
            Model = Models.Count > 1 ? Models.First(m => m != model) : string.Empty;

        Models.Remove(model);
    }

    [RelayCommand]
    private void EditPrompt()
    {
        var dialog = _context.GetPromptEditWindow(Main.Prompts, ["user", "model"]);

        if (dialog.ShowDialog() == true)
        {
            // 保存更新后的 Prompts
            _settings.Prompts = [.. Main.Prompts.Select(p => p.Clone())];
            _context.SaveSettingStorage<Settings>();

            // 更新选中项
            Main.SelectedPrompt = Main.Prompts.FirstOrDefault(p => p.IsEnabled);
        }
    }

    [RelayCommand]
    public async Task ValidateAsync()
    {
        try
        {
            // 选择模型
            var model = _settings.Model.Trim();
            model = string.IsNullOrEmpty(model) ? "gemini-flash-latest" : model;

            UriBuilder uriBuilder = new(_settings.Url);
            // 如果路径不是有效的API路径结尾，使用默认路径
            if (uriBuilder.Path == "/")
                uriBuilder.Path = $"/v1beta/models/{model}:streamGenerateContent";

            // 加上 alt=sse 才是每个流传输结果为完整json，方便解析判断是否为最后一条
            uriBuilder.Query = $"alt=sse&key={_settings.ApiKey}";

            // 替换Prompt关键字
            var messages = (Main.Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"))
                .Clone()
                .Items;
            messages.ToList()
                .ForEach(item =>
                    item.Content = item.Content
                    .Replace("$source", "en-US")
                    .Replace("$target", "zh-CN")
                    .Replace("$content", "Hello world")
                    );

            // 温度限定
            var temperature = Math.Clamp(_settings.Temperature, 0, 2);

            var thinkingBudget = (int)Math.Clamp(_settings.ThinkingBudget, -1, 24576);

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

            await _context.HttpService.StreamPostAsync(uriBuilder.Uri.ToString(), content, (x) => { });

            ValidateResult = _context.GetTranslation("ValidationSuccess");
        }
        catch (Exception ex)
        {
            ValidateResult = _context.GetTranslation("ValidationFailure");
            _context.Logger.LogError(ex, _context.GetTranslation("ValidationFailure"));
        }
    }


    public void Dispose()
    {
        PropertyChanged -= OnPropertyChanged;
        Models.CollectionChanged -= OnModelsCollectionChanged;
    }

    // 辅助类和记录
    private readonly struct UpdateGuard : IDisposable
    {
        private readonly SettingsViewModel _viewModel;

        public UpdateGuard(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel._isUpdating = true;
        }

        public void Dispose() => _viewModel._isUpdating = false;
    }
}
