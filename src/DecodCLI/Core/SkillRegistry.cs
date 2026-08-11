namespace DecodCLI.Core;

public class SkillInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}

public class SkillRegistry
{
    public List<SkillInfo> Skills { get; private set; } = new();

    public SkillRegistry(string workspaceRoot)
    {
        LoadSkills(Path.Combine(workspaceRoot, ".decod", "skills"));
        LoadSkills(Path.Combine(workspaceRoot, ".agents", "skills"));
    }

    private void LoadSkills(string skillsDir)
    {
        if (!Directory.Exists(skillsDir)) return;

        foreach (var dir in Directory.GetDirectories(skillsDir))
        {
            var skillFile = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillFile))
            {
                var content = File.ReadAllText(skillFile);
                var skillName = Path.GetFileName(dir);
                Skills.Add(new SkillInfo
                {
                    Name = skillName,
                    Description = ExtractDescription(content),
                    FilePath = skillFile,
                    Instructions = content
                });
            }
        }
    }

    private string ExtractDescription(string content)
    {
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(12).Trim().Trim('"');
            }
        }
        return "Custom workspace skill instructions.";
    }
}
