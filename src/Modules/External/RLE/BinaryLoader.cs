using System.Buffers.Binary;
using System.IO;

namespace RLE;

public class CCAM_Header
{
    byte revision;
    byte general_settings;

    byte compression_spec;

    byte dimensional_specs;

    UInt128[] dimensions = [0, 0, 0];
    UInt128[] dimensional_offsets = [0, 0, 0];

    public bool ReadSetting(byte set, byte location) => (set & ~(1 << location)) > 0;

    public int ReadMultiplier() => (int)Math.Pow(10f, (compression_spec & 0b11110000) >> 4);

    public CompressionType ReadCompressionType() => (CompressionType)(compression_spec & 0b00001111);

    public DimensionalType ReadDimensionalType() => (DimensionalType)(dimensional_specs & 0b00000011);

    private int parseDimensionalBits(byte mask, byte shift) => (int)Math.Pow(2, 1 + ((dimensional_specs & mask) >> shift));

    public int[] ReadDimensionalLength(DimensionalType dimensionType) => dimensionType switch
    {
        DimensionalType._1D => [],
        DimensionalType._2D => [parseDimensionalBits(0b11100000, 5), parseDimensionalBits(0b00011100, 2)],
        // DimensionalType._2D => ( (int) Math.Pow(2,1+((dimensional_specs & 0b11100000) >> 5)), (int)Math.Pow(2,1+((dimensional_specs & 0b00011100) >> 2)),0),
        DimensionalType._3D => [parseDimensionalBits(0b11000000, 6), parseDimensionalBits(0b00110000, 4), parseDimensionalBits(0b00001100, 2)],
    };

    public CCAM_Header(BinaryReader reader)
    {
        revision = reader.ReadByte();
        general_settings = reader.ReadByte();
        compression_spec = reader.ReadByte();
        dimensional_specs = reader.ReadByte();
        int[] sizes = ReadDimensionalLength(ReadDimensionalType());
        for (int i = 0; i < sizes.Length; i++) dimensions[i] = sizes[i] switch
        {
            2 => BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(sizes[i])),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(sizes[i])),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(reader.ReadBytes(sizes[i])),
            16 => BinaryPrimitives.ReadUInt128LittleEndian(reader.ReadBytes(sizes[i])),
            _ => throw new Exception(),
        };

        if (ReadSetting(general_settings, (byte)GeneralSettingsLocations.dimensionalOffsets))
        {
            for (int i = 0; i < sizes.Length; i++) dimensional_offsets[i] = sizes[i] switch
            {
                2 => BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(sizes[i])),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(sizes[i])),
                8 => BinaryPrimitives.ReadUInt64LittleEndian(reader.ReadBytes(sizes[i])),
                16 => BinaryPrimitives.ReadUInt128LittleEndian(reader.ReadBytes(sizes[i])),
                _ => throw new Exception(),
            };
        }
    }

    public enum GeneralSettingsLocations
    {
        EmbeddedMetadata = 0,
        signedData = 1,
        dimensionalOffsets = 2,
        RLE_Safety = 3, // Maybe ???
    }

    public enum CompressionType
    {
        f32,
        uint16,
        RunLengthPair,
        ShortRunLengthPair,
    }

    public enum DimensionalType
    {
        _1D = 0,
        _2D = 1,
        _3D = 2,
    }
}

public class CCAM_BaseTag
{
    Int64 timestamp;
    byte[] metadata;
}

public class CCAM_Frame<T>
{
    T data;
    CCAM_BaseTag tag;

    public CCAM_Frame(T data, CCAM_BaseTag tag)
    {
        this.data = data;
        this.tag = tag;
    }
}

public static class BinaryLoader
{
    public static (CCAM_Header, CCAM_Frame<O>[]) LoadData<O>(BinaryReader reader, Func<BinaryReader, CCAM_BaseTag> tagParser, Func<BinaryReader, CCAM_Header, O> dataParser)
    {
        CCAM_Header header = new CCAM_Header(reader);
        List<CCAM_Frame<O>> frames = [];
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var tag = tagParser(reader);
            var data = dataParser(reader, header);
            frames.Add(new CCAM_Frame<O>(data, tag));
        }
        return (header, frames.ToArray());
    }

    public static void DefaultDataParser(BinaryReader reader, CCAM_Header header)
    {
        switch (header.ReadCompressionType())
        {
            case CCAM_Header.CompressionType.f32:
                break;

            case CCAM_Header.CompressionType.uint16:
                break;

            case CCAM_Header.CompressionType.RunLengthPair:
                break;

        }
    }
}