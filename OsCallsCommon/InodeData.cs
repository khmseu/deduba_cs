using System.Text.Json.Serialization;

namespace OsCallsCommon;

/// <summary>
/// Represents complete filesystem metadata for a single inode.
/// Used by IHighLevelOsApi.CreateInodeDataFromPath() to return
/// all collected metadata (stat, ACLs, xattrs, content hashes).
/// </summary>
public sealed class InodeData
{
    [JsonPropertyName("fi")]
    public required System.Text.Json.JsonElement? FileId { get; init; }

    [JsonPropertyName("md")]
    public long Mode { get; init; }

    [JsonPropertyName("fl")]
    public required HashSet<string> Flags { get; init; }

    [JsonPropertyName("nl")]
    public long NLink { get; init; }

    [JsonPropertyName("ui")]
    public long Uid { get; init; }

    [JsonPropertyName("un")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("gi")]
    public long Gid { get; init; }

    [JsonPropertyName("gn")]
    public string GroupName { get; init; } = string.Empty;

    [JsonPropertyName("rd")]
    public long RDev { get; init; }

    [JsonPropertyName("sz")]
    public long Size { get; init; }

    [JsonPropertyName("mt")]
    public double MTime { get; init; }

    [JsonPropertyName("ct")]
    public double CTime { get; init; }

    [JsonPropertyName("hs")]
    public IEnumerable<string> Hashes { get; set; } = [];

    [JsonPropertyName("ac")]
    public IEnumerable<string> Acl { get; set; } = [];

    [JsonPropertyName("xa")]
    public Dictionary<string, IEnumerable<string>> Xattr { get; set; } = [];

    /// <summary>
    /// Returns a compact string representation for diagnostics.
    /// </summary>
    public override string ToString()
    {
        var hashCount = Hashes.Count();
        var hashInfo = hashCount > 0 ? $"{hashCount} hash(es)" : "no hashes";
        var aclCount = Acl.Count();
        var aclInfo = aclCount > 0 ? $"{aclCount} acl hash(es)" : "";
        var xattrCount = Xattr.Count;
        var xattrInfo = xattrCount > 0 ? $"{xattrCount} xattr(s)" : "";
        var extras = string.Join(
            " ",
            new[] { aclInfo, xattrInfo }.Where(s => !string.IsNullOrEmpty(s))
        );
        return $"[mode=0{Mode:o} nlink={NLink} {UserName}({Uid}):{GroupName}({Gid}) rdev={RDev} size={Size} mtime={MTime} ctime={CTime} {hashInfo} {extras}]";
    }
}
