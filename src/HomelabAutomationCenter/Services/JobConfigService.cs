using HomelabAutomationCenter.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabAutomationCenter.Services;

public sealed class JobConfigService
{
    private const string ConfigPath = "/config/jobs.yml";

    private sealed class JobsFile
    {
        public List<JobConfig>? Jobs { get; set; }
    }

    public IReadOnlyList<JobConfig> ReadJobs()
    {
        if (!File.Exists(ConfigPath))
        {
            return [];
        }

        var yaml = File.ReadAllText(ConfigPath);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        try
        {
            var doc = deserializer.Deserialize<JobsFile>(yaml);
            if (doc?.Jobs is null)
            {
                return [];
            }

            return doc.Jobs
                .Where(j => !string.IsNullOrWhiteSpace(j.Id)
                    && !string.IsNullOrWhiteSpace(j.Name)
                    && !string.IsNullOrWhiteSpace(j.StatusPath))
                .Select(j => new JobConfig
                {
                    Id = j.Id.Trim(),
                    Name = j.Name.Trim(),
                    StatusPath = j.StatusPath.Trim(),
                    StaleAfterMinutes = j.StaleAfterMinutes <= 0 ? 60 : j.StaleAfterMinutes
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
