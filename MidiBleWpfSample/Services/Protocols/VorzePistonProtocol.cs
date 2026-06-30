using System;

namespace MidiBleWpfSample.Services.Protocols
{
    public class VorzePistonProtocol : IBleProtocol
    {
        public byte[] BuildPacket(int ccValue)
        {
            // 例：Byte[0] = 0x10, Byte[1] = 位置(0x00~0xC8), Byte[2] = 速度(0x00~0x64)
            byte commandID = 0x10;
            byte position = (byte)((ccValue * 200) / 127); // 仮: 0~200
            byte speed = (byte)((ccValue * 100) / 127); // 仮: 0~100

            return new byte[] { commandID, position, speed };
        }
    }
}
