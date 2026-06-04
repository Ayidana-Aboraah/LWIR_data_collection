using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Optris.OtcSDK;

namespace LWIR_app.classes
{
    public class RecordedFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public required UInt16[] Temperatures { get; set; }

        public required FrameMetadata Metadata { get; set; }
    }
}
