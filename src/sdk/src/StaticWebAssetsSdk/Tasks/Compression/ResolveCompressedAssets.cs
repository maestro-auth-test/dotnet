// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveCompressedAssets : Task
{
    private static readonly char[] PatternSeparator = [';'];

    private const string GzipAssetTraitValue = "gzip";
    private const string BrotliAssetTraitValue = "br";

    private const string GzipFormatName = "gzip";
    private const string BrotliFormatName = "brotli";

    public ITaskItem[] CandidateAssets { get; set; }

    public string Formats { get; set; }

    public string IncludePatterns { get; set; }

    public string ExcludePatterns { get; set; }

    public ITaskItem[] ExplicitAssets { get; set; }

    [Required]
    public string OutputPath { get; set; }

    [Output]
    public ITaskItem[] AssetsToCompress { get; set; }

    public override bool Execute()
    {
        if (CandidateAssets is null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no candidate assets for compression were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        if (string.IsNullOrEmpty(Formats))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no compression formats were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        var candidates = StaticWebAsset.FromTaskItemGroup(CandidateAssets).ToArray();
        var explicitAssets = ExplicitAssets == null ? [] : StaticWebAsset.FromTaskItemGroup(ExplicitAssets);
        var existingCompressionFormatsByAssetItemSpec = CollectCompressedAssets(candidates);

        var includePatterns = SplitPattern(IncludePatterns);
        var excludePatterns = SplitPattern(ExcludePatterns);

        var matcher = new StaticWebAssetGlobMatcherBuilder()
            .AddIncludePatterns(includePatterns)
            .AddExcludePatterns(excludePatterns)
            .Build();

        var matchingCandidateAssets = new List<StaticWebAsset>(CandidateAssets.Length);

        var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();

        // Add each candidate asset to each compression configuration with a matching pattern.
        foreach (var asset in candidates)
        {
            if (IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Ignoring asset '{0}' for compression because it is already compressed asset for '{1}'.",
                    asset.Identity,
                    asset.RelatedAsset);
                continue;
            }

            var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
            matchContext.SetPathAndReinitialize(relativePath.AsSpan());
            var match = matcher.Match(matchContext);

            if (!match.IsMatch)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset '{0}' with relative path '{1}' did not match include pattern '{2}' or matched exclude pattern '{3}'.",
                    asset.Identity,
                    relativePath,
                    IncludePatterns,
                    ExcludePatterns);
                continue;
            }

            Log.LogMessage(
                MessageImportance.Low,
                "Asset '{0}' with relative path '{1}' matched include pattern '{2}' and did not match exclude pattern '{3}'.",
                asset.Identity,
                relativePath,
                IncludePatterns,
                ExcludePatterns);
            matchingCandidateAssets.Add(asset);
        }

        // Consider each explicitly-provided asset to be a matching asset.
        matchingCandidateAssets.AddRange(explicitAssets);

        // Process the final set of candidate assets, deduplicating assets to be compressed in the same format multiple times and
        // generating new a static web asset definition for each compressed item.
        var formats = SplitPattern(Formats);
        var assetsToCompress = new ITaskItem[matchingCandidateAssets.Count * formats.Length];
        var outputPath = Path.GetFullPath(OutputPath);
        var assetCounter = 0;
        foreach (var asset in matchingCandidateAssets)
        {
            // Reset common properties
            StaticWebAsset previousAsset = null;
            string pathTemplate = null;
            string relativePath = null;

            foreach (var format in formats)
            {
                var itemSpec = asset.Identity;
                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(itemSpec, out var existingFormats))
                {
                    existingFormats = new HashSet<string>(2);
                    existingCompressionFormatsByAssetItemSpec.Add(itemSpec, existingFormats);
                }

                if (existingFormats.Contains(format))
                {
                    Log.LogMessage(
                        "Ignoring asset '{0}' because it was already resolved with format '{1}'.",
                        itemSpec,
                        format);
                    continue;
                }

                pathTemplate ??= CreatePathTemplate(asset, outputPath);
                relativePath ??= asset.EmbedTokens(asset.RelativePath);

                if (TryCreateCompressedAsset(
                    asset,
                    outputPath,
                    format,
                    pathTemplate,
                    relativePath,
                    ref previousAsset,
                    out var compressedAsset))
                {
                    var result = compressedAsset.ToTaskItem();
                    result.SetMetadata("RelatedAssetOriginalItemSpec", asset.OriginalItemSpec);

                    assetsToCompress[assetCounter++] = result;
                    existingFormats.Add(format);

                    Log.LogMessage(
                        "Accepted compressed asset '{0}' for '{1}'.",
                        result.ItemSpec,
                        itemSpec);
                }
                else
                {
                    Log.LogError(
                        "Could not create compressed asset for original asset '{0}'.",
                        itemSpec);
                }
            }
        }

        Log.LogMessage(
            "Resolved {0} compressed assets for {1} candidate assets.",
            assetCounter,
            matchingCandidateAssets.Count);

        AssetsToCompress = assetsToCompress;

        return !Log.HasLoggedErrors;
    }

    private static string CreatePathTemplate(StaticWebAsset asset, string outputPath)
    {
        var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
        var pathHash = FileHasher.HashString(asset.SourceId + asset.BasePath + asset.AssetKind + relativePath);
        return Path.Combine(outputPath, $"{pathHash}-{{0}}-{asset.Fingerprint}");
    }

    private Dictionary<string, HashSet<string>> CollectCompressedAssets(StaticWebAsset[] candidates)
    {
        // Scan the provided candidate assets and determine which ones have already been detected for compression and in which formats.
        var existingCompressionFormatsByAssetItemSpec = new Dictionary<string, HashSet<string>>();

        foreach (var asset in candidates)
        {
            if (!IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset '{0}' is not compressed.",
                    asset.Identity);
                continue;
            }
            var relatedAssetItemSpec = asset.RelatedAsset;

            if (string.IsNullOrEmpty(relatedAssetItemSpec))
            {
                Log.LogError(
                    "The asset '{0}' was detected as compressed but didn't specify a related asset.",
                    asset.Identity);
                continue;
            }

            if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(relatedAssetItemSpec, out var existingFormats))
            {
                existingFormats = [];
                existingCompressionFormatsByAssetItemSpec.Add(relatedAssetItemSpec, existingFormats);
            }

            string assetFormat;

            if (string.Equals(asset.AssetTraitValue, GzipAssetTraitValue, StringComparison.OrdinalIgnoreCase))
            {
                assetFormat = GzipFormatName;
            }
            else if (string.Equals(asset.AssetTraitValue, BrotliAssetTraitValue, StringComparison.OrdinalIgnoreCase))
            {
                assetFormat = BrotliFormatName;
            }
            else
            {
                Log.LogError(
                    "The asset '{0}' has an unknown compression format '{1}'.",
                    asset.Identity,
                    asset.AssetTraitValue);
                continue;
            }

            Log.LogMessage(
                "The asset '{0}' with related asset '{1}' was detected as already compressed with format '{2}'.",
                asset.Identity,
                relatedAssetItemSpec,
                assetFormat);
            existingFormats.Add(assetFormat);
        }

        return existingCompressionFormatsByAssetItemSpec;
    }

    private static bool IsCompressedAsset(StaticWebAsset asset)
        => string.Equals("Content-Encoding", asset.AssetTraitName, StringComparison.Ordinal);

    private static string[] SplitPattern(string pattern)
        => string.IsNullOrEmpty(pattern) ? [] : pattern
            .Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

    private bool TryCreateCompressedAsset(
        StaticWebAsset asset,
        string outputPath,
        string format,
        string pathTemplate,
        string relativePath,
        ref StaticWebAsset previousAsset,
        out StaticWebAsset result)
    {
        result = null;

        string fileExtension;
        string assetTraitValue;

        if (string.Equals(GzipFormatName, format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".gz";
            assetTraitValue = GzipAssetTraitValue;
        }
        else if (string.Equals(BrotliFormatName, format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".br";
            assetTraitValue = BrotliAssetTraitValue;
        }
        else
        {
            Log.LogError(
                "Unknown compression format '{0}' for '{1}'.",
                format,
                asset.Identity);
            return false;
        }

        // Make the hash name more unique by including source id, base path, asset kind and relative path.
        // This combination must be unique across all assets, so this will avoid collisions when two files on
        // the same project have the same contents, when it happens across different projects or between Build/Publish
        // assets.
        var fileName = $"{pathTemplate}-{asset.Fingerprint}{fileExtension}";
        var itemSpec = Path.GetFullPath(Path.Combine(OutputPath, fileName));

        if (previousAsset != null)
        {
            result = new StaticWebAsset(previousAsset)
            {
                Identity = itemSpec,
                RelativePath = $"{relativePath}{fileExtension}",
                AssetTraitValue = assetTraitValue,
            };
        }
        else
        {
            result = new StaticWebAsset(asset)
            {
                Identity = itemSpec,
                RelativePath = $"{relativePath}{fileExtension}",
                OriginalItemSpec = asset.Identity,
                RelatedAsset = asset.Identity,
                AssetRole = "Alternative",
                AssetTraitName = "Content-Encoding",
                AssetTraitValue = assetTraitValue,
                ContentRoot = outputPath,
                // Set integrity and fingerprint to null so that they get recalculated for the compressed asset.
                Fingerprint = null,
                Integrity = null,
            };

            previousAsset = result;
        }

        return true;
    }
}
