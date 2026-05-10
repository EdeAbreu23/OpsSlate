using OpsSlate.Models;
using OpsSlate.Options;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpsSlate.Services;

public sealed class JobConfigService
{
    private readonly OpsSlatePathOptions _pathOptions;

    public JobConfigService(IOptions<OpsSlatePathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
    }

    private sealed class JobsFile
    {
        public List<JobConfig>? Jobs { get; set; }
    }

    public IReadOnlyList<JobConfig> ReadJobs()
    {
        if (!File.Exists(_pathOptions.ConfigPath))
        {
            return [];
        }

        var yaml = File.ReadAllText(_pathOptions.ConfigPath);
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
                    && !string.IsNullOrWhiteSpace(j.StatusPath)
                    && _pathOptions.TryResolveStatusPath(j.StatusPath, out _, out _))
                .Select(j => new JobConfig
                {
                    Id = j.Id.Trim(),
                    Name = j.Name.Trim(),
                    StatusPath = _pathOptions.ResolveStatusPath(j.StatusPath),
                    StaleAfterMinutes = j.StaleAfterMinutes <= 0 ? 60 : j.StaleAfterMinutes,
                    DependsOn = (j.DependsOn ?? [])
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Select(d => d.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
