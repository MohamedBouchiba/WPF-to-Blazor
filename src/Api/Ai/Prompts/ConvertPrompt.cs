namespace Api.Ai.Prompts;

public static class ConvertPrompt
{
    public static string BuildSystemPrompt(string target)
    {
        return $@"You are a senior .NET migration engineer converting WPF applications to {target}.

Your task is to convert WPF XAML and code-behind files into Blazor components.

OUTPUT FORMAT - you MUST use this exact format for each file:
FILE: <relative_path>
<file content>
END_FILE

Rules:
- Convert XAML to Razor markup (.razor files)
- Convert code-behind to Blazor component code (@code blocks or .razor.cs partial classes)
- Convert ViewModels to injectable services registered via DI
- Use standard Blazor patterns: @inject, [Parameter], EventCallback, etc.
- Prefer {target} patterns and conventions
- Preserve business logic where possible
- Create placeholder implementations for complex third-party controls (name them clearly, e.g. PlaceholderDataGrid)
- Convert WPF bindings ({{Binding}}) to Blazor @bind or property access
- Convert WPF commands to Blazor event handlers
- Convert WPF converters to Blazor utility methods
- Convert resource dictionaries to CSS or shared Razor components
- Output ONLY FILE blocks, no extra text, no markdown fences, no explanation
- Every file must start with FILE: and end with END_FILE";
    }

    public static string BuildUserPrompt(Dictionary<string, string> files, string? analysisJson)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(analysisJson))
        {
            parts.Add("Project analysis context:");
            parts.Add(analysisJson);
            parts.Add("");
        }

        parts.Add("Convert these WPF files to Blazor:\n");
        foreach (var (path, content) in files)
        {
            parts.Add($"=== FILE: {path} ===");
            parts.Add(content);
            parts.Add("=== END FILE ===\n");
        }

        return string.Join("\n", parts);
    }
}
