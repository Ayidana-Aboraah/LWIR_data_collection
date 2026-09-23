namespace RLE;

public static class OLD_LWIR_Parser
{
    public static CCAM_Header HeaderParser(BinaryReader reader)
    {
        CCAM_Header header = new CCAM_Header() { };
        header.dimensions[0] = (UInt128)reader.ReadInt32();
        header.dimensions[1] = (UInt128)reader.ReadInt32();
        return header;
    }

    public static float[] DataParser(BinaryReader reader, CCAM_Header header)
    {
        int pixelCount = (int)(header.dimensions[0] * header.dimensions[1]);
        var temperatures = new float[pixelCount];

        Type T = BinaryLoader.InferDataType(header.ReadCompressionType()); // TODO: Take the CompressionType from the filename 

        if (T == typeof(float)) for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadSingle();
        else if (T == typeof(ushort)) for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadUInt16() * 0.01f;
        else if (T == typeof(RunLengthPair))
        {
            int count = 0;

            while (count < pixelCount)
            {
                if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
                // if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) throw new EndOfStreamException($"Unexpected end of RLE data while decoding {pixelCount} pixels.");

                ushort value = reader.ReadUInt16();
                ushort length = reader.ReadUInt16();

                if (length == 0) break;
                // if (length == 0) throw new InvalidDataException("Encountered an RLE pair with zero length.");
                // if (count + length > pixelCount)
                //     throw new InvalidDataException($"RLE run length {length} exceeds remaining pixel count ({pixelCount - count}).");

                float temperature = value * 0.01f;
                for (int i = 0; i < length && count < pixelCount; i++) temperatures[count++] = temperature;
            }
        }
        return temperatures;
    }

    public static CCAM_BaseTag TagParser(BinaryReader reader)
    {
        CCAM_BaseTag tag = new CCAM_BaseTag() { };
        tag.timestamp = DateTime.FromFileTime(reader.ReadInt64());
        tag.metadata = reader.ReadBytes(8);
        return tag;
    }

    // TODO: Use Metadata parser
    // public static CCAM_BaseTag[] MetadataParser(BinaryReader reader)
    // {
    //     // TODO: Parse the metadata line by line
    // }

    public static uint ReadCounter(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.Counter));
    public static uint ReadHardwareCounter(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.HardwareCounter));
    public static float ReadBoxTemp(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.BoxTemp));
    public static float ReadChiptemp(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.ChipTemp));
    public static float ReadMinTemp(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.MinTemp));
    public static float ReadMaxTemp(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.MaxTemp));
    public static double ReadMeanTemp(byte[] metadata) => uint.Parse(metadata.AsSpan().Slice((int)Old_LWIR_Metadata_Positions.MeanTemp));

    public enum Old_LWIR_Metadata_Positions
    {
        Timestamp = 0,
        Counter = 8,
        HardwareCounter = 12,
        BoxTemp = 16,
        ChipTemp = 20,
        MinTemp = 24,
        MaxTemp = 28,
        MeanTemp = 32,
    }
}