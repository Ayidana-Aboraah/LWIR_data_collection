namespace SensorInterface.classes
{
    public enum SaveDataType
    {
        Float = 0,
        U16 = 1,
        RLE = 2,
    }
    
    public struct RecorderSettings
    {
        public int camera_width;
        public int camera_height;
        public SaveDataType dataType;
        public bool singleBinary;
        public bool recordROIOnly;
        public string baseDirectory;
    }
}