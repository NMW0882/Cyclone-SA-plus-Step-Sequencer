using System;

namespace MidiBleWpfSample.Services.Protocols
{
    public class VorzeCycloneProtocol : IBleProtocol
    {
        private readonly byte _identifier; // 0x01 (CysSA) or 0x02 (UFOSA)

        public VorzeCycloneProtocol(byte identifier)
        {
            _identifier = identifier;
        }

        public byte[] BuildPacket(int ccValue)
        {
            // Byte[0] = _identifier
            byte baseCommand = _identifier;
            // Byte[1] = 0x01 (固定)
            byte reserved = 0x01;
            // Byte[2] = 回転速度/方向 (例：CC#10の既存ロジック)
            byte speed;

            if (ccValue == 64)
            {
                speed = 0x80;     // 停止
            }
            else if (ccValue < 64)
            {
                speed = (byte)(0x64 - ((ccValue * 0x64) / 63)); // 0x64..0x01(逆)
            }
            else
            {
                speed = (byte)(0x80 + (((ccValue - 64) * 0x64) / 63)); // 0x81..0xE4(正)
            }

            return new byte[] { baseCommand, reserved, speed };
        }
    }
}
