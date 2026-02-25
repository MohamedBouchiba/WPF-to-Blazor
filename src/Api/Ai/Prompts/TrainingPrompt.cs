namespace Api.Ai.Prompts;

public static class TrainingPrompt
{
    public static string BuildSystemPrompt(string lang)
    {
        var langName = lang == "nl" ? "Dutch (NL)" : "French (FR)";
        var glossaryOrder = lang == "nl" ? "NL first, FR second" : "FR first, NL second";

        return $@"You are a technical trainer creating a training kit for WPF to Blazor migration.
Write entirely in {langName}.

Produce a complete markdown document with these sections:

# Kit de Formation: Migration WPF → Blazor

## 1. Agenda (session de 90 minutes)
Detailed agenda with time allocations:
- Introduction & context (10 min)
- Blazor fundamentals recap (15 min)
- WPF vs Blazor: key differences (15 min)
- Hands-on: converting a real component (25 min)
- Common pitfalls & best practices (15 min)
- Q&A (10 min)

## 2. Exercices pratiques
3-4 hands-on exercises using concrete examples from the project:
- Exercise 1: Convert a simple WPF Window to a Blazor page
- Exercise 2: Migrate a ViewModel to a Blazor service
- Exercise 3: Convert data bindings and commands
- Exercise 4: Handle navigation and state management
Include step-by-step instructions and expected outcomes.

## 3. Pièges courants
List of common mistakes and how to avoid them during migration.
Include code examples of wrong vs correct approaches.

## 4. FAQ
At least 10 frequently asked questions about WPF to Blazor migration.

## 5. Glossaire ({glossaryOrder})
Bilingual glossary of key technical terms.
Format: Term ({langName}) — Translation — Brief definition.
Cover at least 20 terms.

Output ONLY the markdown content, no wrapping fences.";
    }

    public static string BuildUserPrompt(string? analysisJson, Dictionary<string, string>? convertedFiles)
    {
        var parts = new List<string> { "Generate the training kit based on this project context:\n" };

        if (!string.IsNullOrEmpty(analysisJson))
        {
            parts.Add("Project analysis:");
            parts.Add(analysisJson);
            parts.Add("");
        }

        if (convertedFiles != null && convertedFiles.Count > 0)
        {
            parts.Add("Sample converted files (use these for exercises):");
            foreach (var (path, content) in convertedFiles.Take(3))
            {
                parts.Add($"=== FILE: {path} ===");
                parts.Add(content.Length > 2000 ? content[..2000] + "\n... (truncated)" : content);
                parts.Add("=== END FILE ===\n");
            }
        }

        return string.Join("\n", parts);
    }
}
