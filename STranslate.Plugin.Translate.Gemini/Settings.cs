namespace STranslate.Plugin.Translate.Gemini;

public class Settings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Url { get; set; } = "https://generativelanguage.googleapis.com/";
    public string Model { get; set; } = "gemini-flash-latest";
    public List<string> Models { get; set; } =
    [
        "gemini-flash-latest",
        "gemini-3-pro-preview"
    ];
    public double Temperature { get; set; } = 0.7;

    public List<Prompt> Prompts { get; set; } =
    [
        new("翻译",
        [
            new PromptItem(
                "user",
                "You are a professional translation engine, please translate the text into a colloquial, professional, elegant and fluent content, without the style of machine translation. You must only translate the text content, never interpret it."
            ),
            new PromptItem("model", "Ok, I will only translate the text content, never interpret it"),
            new PromptItem("user", "Translate the following text from en to zh: hello world"),
            new PromptItem("model", "你好，世界"),
            new PromptItem("user", "Translate the following text from $source to $target: $content")
        ], true),
        new("润色",
        [
            new PromptItem("user", "You are a text embellisher, you can only embellish the text, never interpret it."),
            new PromptItem("model", "Ok, I will only embellish the text, never interpret it."),
            new PromptItem("user", "Embellish the following text in $source: $content")
        ]),
        new("总结",
        [
            new PromptItem("user", "You are a text summarizer, you can only summarize the text, never interpret it."),
            new PromptItem("model", "Ok, I will only summarize the text, never interpret it."),
            new PromptItem("user", "Summarize the following text in $source: $content")
        ])
    ];
}