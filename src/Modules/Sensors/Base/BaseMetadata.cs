using System.IO;

namespace ThermalCamerApp.Camera;

public class BaseMetadata
{
    public virtual void WriteHeader(BinaryWriter writer){}
    public virtual void WriteMetadataHeader(StreamWriter writer){}
    public virtual void WriteMetadata(StreamWriter writer, int frameIdx, float min, float max, double mean){}
}