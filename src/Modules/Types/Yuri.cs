using LWIR_app.Sensor;
using Optris.OtcSdk;
using RLE;

namespace LWIR_app.classes;

public struct YuriFrame
{
    UInt16[] headers = new UInt16[128];
    UInt16[] data;

    UInt16[] TimeTag = new UInt16[16];

    enum HeaderPositions
    {
        Length = 0,
        Revision = 1,
        Tag = 2,
        Nx = 5,
        Ny = 6,
        Nt = 7,
        FrameRate = 8,
        Threshold = 9,
        DivisionCoeffient = 10,
        OffsetX = 11,
        OffsetY = 12,
        OffsetZ = 13,
        DAQ_Year = 14,
        DAQ_Day = 15,
        DAQ_Hour = 16,
        DAQ_Min = 17,
        DAQ_Sec = 18,
        DAQ_MS = 19,
        Nx_Ext = 25,
        Ny_Ext = 26,
        Nt_Ext = 27,
        samplingUnits = 30,
        Ch1 = 31,
        Ch2 = 32,
        Ch3 = 33,
        Ch4 = 34,
        Ch5 = 35,
        Ch6 = 36,
        Ch7 = 37,
        Ch8 = 38,
        Ch9 = 39,
        Ch10 = 40,
        Nxx = 41,
        Nyy = 42,
        Ntt = 43,
    }

    enum TimeTagPositions
    {
        TimeTag_milisecond = 0,
        TimeTag_second = 1,
        TimeTag_minute = 2,
        TimeTag_hour = 3,
        TimeTag_day = 4,

        TimeTag_month = 5,
        TimeTag_year = 6,
        Layer = 7,
        platform_position_x = 8,
        platform_position_y = 9,

        platform_position_z = 10,
        TCP_position_x = 11,
        TCP_position_y = 12,
        TCP_position_z = 13,
        TimeTag_microsecond = 15,
    }

    public YuriFrame(FrameRecord originalFrame)
    {
        headers[(int)HeaderPositions.Nxx] = (UInt16)originalFrame.width;
        headers[(int)HeaderPositions.Nyy] = (UInt16)originalFrame.height;
        headers[(int)HeaderPositions.samplingUnits] = 0;

        data = DataConverter.FloatToInt(originalFrame.data);
        var x = ((FrameMetadata)originalFrame.metadata).getTimestamp();
        var y = DateTimeOffset.FromUnixTimeMilliseconds(x); // TODO: Divide some value to ensure its within range

        TimeTag[(int)TimeTagPositions.TimeTag_milisecond] = (UInt16)y.Millisecond;
        TimeTag[(int)TimeTagPositions.TimeTag_second] = (UInt16)y.Second;
        TimeTag[(int)TimeTagPositions.TimeTag_minute] = (UInt16)y.Minute;
        TimeTag[(int)TimeTagPositions.TimeTag_hour] = (UInt16)y.Hour;
        TimeTag[(int)TimeTagPositions.TimeTag_day] = (UInt16)y.Day;
        TimeTag[(int)TimeTagPositions.TimeTag_month] = (UInt16)y.Month;
        TimeTag[(int)TimeTagPositions.TimeTag_year] = (UInt16)y.Year;
    }
}