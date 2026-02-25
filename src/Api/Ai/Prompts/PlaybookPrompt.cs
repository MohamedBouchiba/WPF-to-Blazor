namespace Api.Ai.Prompts;

public static class PlaybookPrompt
{
    public static string BuildSystemPrompt(string lang)
    {
        var langName = lang == "nl" ? "Dutch (NL)" : "French (FR)";
        return $@"You are a migration consultant producing an industrialized migration playbook.
Write entirely in {langName}.

Produce a complete markdown document with these sections:

# Playbook de Migration WPF → Blazor

## 1. Approche industrialisée
Describe the industrialized approach for migrating WPF to Blazor at scale.
Include: strategy, phases, team organization, risk management.

## 2. Outillage & garde-fous
List tools, IDE extensions, CI/CD checks, linters, and guardrails.
Include AI-assisted conversion tools and their limitations.

## 3. Patterns de migration (table de correspondance WPF → Blazor)
Provide a detailed mapping table:
| WPF Concept | Blazor Equivalent | Notes |
Cover: controls, bindings, commands, converters, styles, resources, navigation, DI, testing.

## 4. Checklist revue de code
Numbered checklist for code review of converted components.

## 5. Checklist QA
Testing checklist: unit, integration, UI, accessibility, performance.

## 6. Definition of Done
Clear criteria for considering a component migration complete.

## 7. Troubleshooting
Common issues encountered during WPF→Blazor migration and their solutions.

Output ONLY the markdown content, no wrapping fences.";
    }

    public static string BuildUserPrompt(string? analysisJson, Dictionary<string, string>? sampleFiles)
    {
        var parts = new List<string> { "Generate the playbook based on this project context:\n" };

        if (!string.IsNullOrEmpty(analysisJson))
        {
            parts.Add("Project analysis:");
            parts.Add(analysisJson);
            parts.Add("");
        }

        if (sampleFiles != null)
        {
            parts.Add("Sample source files:");
            foreach (var (path, _) in sampleFiles.Take(5))
            {
                parts.Add($"- {path}");
            }
        }

        return string.Join("\n", parts);
    }
}
