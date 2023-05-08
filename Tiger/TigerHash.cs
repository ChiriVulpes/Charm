﻿using System.Globalization;

namespace Tiger;

public interface IHash
{
    public bool IsValid();
}


/// <summary>
/// Represents string that has been hashed via the FNV1-32 algorithm.
/// </summary>
[SchemaType(0x04)]
public class StringHash : TigerHash
{
    public const uint InvalidHash32 = 0x811c9dc5;

    public StringHash(uint hash32) : base(hash32)
    {
        Hash32 = hash32;
    }

    public StringHash() : base(InvalidHash32)
    {
    }

    public StringHash(string hash) : base(hash)
    {
    }

    public override bool IsValid()
    {
        return Hash32 != InvalidHash32 && Hash32 != 0;
    }
}

/// <summary>
/// A TigerHash represents any hash that is used to identify something within Tiger.
/// These are all implemented as a 32-bit hash.
/// See "FileHash", "File64Hash", and "TagClassHash" for children of this class.
/// </summary>
[SchemaType(0x04)]
public class TigerHash : IHash, ITigerDeserialize, IComparable<TigerHash>, IEquatable<TigerHash>
{
    public uint Hash32;
    public const uint InvalidHash32 = 0xFFFFFFFF;

    public TigerHash(uint hash32)
    {
        Hash32 = hash32;
    }

    public TigerHash(string hash, bool bBigEndianString = true)
    {
        bool parsed = uint.TryParse(hash, NumberStyles.HexNumber, null, out Hash32);
        if (parsed)
        {
            if (hash.EndsWith("80") || hash.EndsWith("81") || bBigEndianString)
            {
                Hash32 = Endian.SwapU32(Hash32);
            }
        }
    }

    public int CompareTo(TigerHash? other)
    {
        if (other == null)
        {
            return 1;
        }

        return Hash32.CompareTo(other.Hash32);
    }

    public bool Equals(TigerHash? other)
    {
        if (other == null)
        {
            return false;
        }

        return Hash32 == other.Hash32;
    }

    public virtual bool IsValid()
    {
        return Hash32 != InvalidHash32 && Hash32 != 0;
    }

    public bool IsInvalid()
    {
        return !IsValid();
    }

    public override string ToString()
    {
        return Endian.U32ToString(Hash32);
    }

    public static implicit operator string(TigerHash hash) => hash.ToString();

    public virtual void Deserialize(TigerReader reader)
    {
        Hash32 = reader.ReadUInt32();
    }
}

/// <summary>
/// Represents a package hash, which is a combination of the EntryId and PkgId e.g. ABCD5680.
/// </summary>
[SchemaType(0x04)]
public class FileHash : TigerHash
{
    public FileHash(int packageId, uint fileIndex) : base(GetHash32(packageId, fileIndex))
    {
    }

    public FileHash(uint hash32) : base(hash32)
    {
    }

    public FileHash(string hash) : base(hash)
    {
    }

    public static uint GetHash32(int packageId, uint fileIndex)
    {
        return (uint)(0x80800000 + (packageId << 0xD) + fileIndex);
    }

    public ushort PackageId => (ushort)((Hash32 >> 0xd) & 0x3ff | (Hash32 & 0x1000000) >> 0x0E);

    public ushort FileIndex => (ushort)(Hash32 & 0x1fff);
}

public static class FileHashExtensions
{
    public static FileHash GetReferenceHash(this FileHash fileHash)
    {
        return new FileHash(fileHash.GetFileMetadata().Reference.Hash32);
    }

    public static FileMetadata GetFileMetadata(this FileHash fileHash)
    {
        return PackageResourcer.Get().GetFileMetadata(fileHash);
    }
}

/// <summary>
/// Same as FileHash, but represents a 64-bit version that is used as a hard reference to a tag. Helps to keep
/// files more similar as FileHash's can change, but the 64-bit version will always be the same.
/// </summary>
[SchemaType(0x10)]
public class File64Hash : FileHash
{
    private ulong Hash64 { get; set; }
    private bool IsHash32 { get; set; }

    public File64Hash(ulong hash64) : base(GetHash32(hash64))
    {
        Hash64 = hash64;
    }

    public override bool IsValid()
    {
        throw new NotImplementedException();
        return Hash32 != InvalidHash32 && Hash32 != 0;
    }

    private static uint GetHash32()
    {
        throw new NotImplementedException();
        return InvalidHash32;
    }

    public override void Deserialize(TigerReader reader)
    {
        Hash32 = reader.ReadUInt32();
        IsHash32 = reader.ReadUInt32() == 1;
        Hash64 = reader.ReadUInt64();
    }
}

/// <summary>
/// Represents the type a tag can be. These can exist as package references which stores the type of the tag file,
/// or within the tag file itself which represents the resource types and other tags inside e.g. ABCD8080.
/// </summary>
public class TagClassHash : TigerHash
{
    public TagClassHash(uint hash32) : base(hash32)
    {
    }
}