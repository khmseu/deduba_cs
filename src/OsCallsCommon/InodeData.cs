using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsCallsCommon;

/// <summary>
///     Represents complete filesystem metadata for a single inode.
///     Used by IHighLevelOsApi.CreateInodeDataFromPath() to return
///     all collected metadata (stat, ACLs, xattrs, content hashes).
/// </summary>
public sealed class InodeData
{
    /// <summary>
    ///     Platform-specific file identifier (device/inode or equivalent).
    ///     Serialized as a compact JSON element to preserve platform structure.
    /// </summary>
    [JsonPropertyName("fi")]
    public JsonElement? FileId { get; set; }

    /// <summary>File mode bits (POSIX st_mode or equivalent).</summary>
    [JsonPropertyName("md")]
    public long Mode { get; init; }

    /// <summary>Set of textual flags describing file type ("reg","dir","lnk",...).</summary>
    [JsonPropertyName("fl")]
    public required HashSet<string> Flags { get; init; }

    /// <summary>Number of hard links.</summary>
    [JsonPropertyName("nl")]
    public long NLink { get; init; }

    /// <summary>Numeric user id owning the inode.</summary>
    [JsonPropertyName("ui")]
    public long Uid { get; init; }

    /// <summary>Resolved user name for the owner, or UID as string when unavailable.</summary>
    [JsonPropertyName("un")]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Numeric group id owning the inode.</summary>
    [JsonPropertyName("gi")]
    public long Gid { get; init; }

    /// <summary>Resolved group name for the owner, or GID as string when unavailable.</summary>
    [JsonPropertyName("gn")]
    public string GroupName { get; init; } = string.Empty;

    /// <summary>Raw device id for character/block devices.</summary>
    [JsonPropertyName("rd")]
    public long RDev { get; init; }

    /// <summary>Size of the file in bytes.</summary>
    [JsonPropertyName("sz")]
    public long Size { get; init; }

    /// <summary>Modification time as seconds since the epoch (floating point).</summary>
    [JsonPropertyName("mt")]
    public double MTime { get; init; }

    /// <summary>Change time as seconds since the epoch (floating point).</summary>
    [JsonPropertyName("ct")]
    public double CTime { get; init; }

    /// <summary>List of saved content hashes (one or more algorithms).</summary>
    [JsonPropertyName("hs")]
    public IEnumerable<string> Hashes { get; set; } = [];

    /// <summary>Serialized ACL data hashes (if any).</summary>
    [JsonPropertyName("ac")]
    public IEnumerable<string> Acl { get; set; } = [];

    /// <summary>Map of extended attribute name → saved-hash-list.</summary>
    [JsonPropertyName("xa")]
    public Dictionary<string, IEnumerable<string>> Xattr { get; set; } = [];

    /// <summary>
    ///     Returns a compact string representation for diagnostics.
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
