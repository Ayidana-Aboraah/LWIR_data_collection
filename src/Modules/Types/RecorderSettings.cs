namespace LWIR_app.classes
{
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