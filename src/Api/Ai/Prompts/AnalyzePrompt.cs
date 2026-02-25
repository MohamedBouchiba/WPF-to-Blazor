namespace Api.Ai.Prompts;

public static class AnalyzePrompt
{
    public const string System = @"You are a senior .NET migration architect specializing in WPF to Blazor conversions.
Analyze the provided WPF project files and produce a structured JSON analysis.

You MUST return ONLY valid JSON with this exact structure:
{
  ""detected_patterns"": [""MVVM"", ""code-behind heavy"", ""resource dictionaries"", ...],
  ""key_views"": [""MainWindow.xaml"", ...],
  ""key_viewmodels"": [""MainViewModel.cs"", ...],
  ""control_inventory"": {""DataGrid"": 3, ""ListView"": 2, ""Button"": 15, ...},
  ""estimated_risk"": ""low|med|high"",
  ""recommended_approach"": [""Phase 1: ..., Phase 2: ...""],
  ""suggested_work_breakdown"": [
    {""phase"": ""Phase 1"", ""description"": ""..."", ""effort_days"": 5},
    ...
  ]
}

Rules:
- detected_patterns: identify MVVM, code-behind heavy, resource dictionaries, custom controls, converters, behaviors, third-party controls
- key_views: list all XAML view files
- key_viewmodels: list all ViewModel classes
- control_inventory: count each WPF control type used across all XAML
- estimated_risk: based on complexity, custom controls, third-party dependencies
- recommended_approach: concrete actionable steps for the migration
- suggested_work_breakdown: realistic phases with effort estimates
- Output ONLY the JSON object, no markdown, no explanation";

    public static string BuildUserPrompt(Dictionary<string, string> files)
    {
        var parts = new List<string> { "Analyze these WPF project files:\n" };
        foreach (var (path, content) in files)
        {
            parts.Add($"=== FILE: {path} ===");
            parts.Add(content);
            parts.Add("=== END FILE ===\n");
        }
        return string.Join("\n", parts);
    }
}
