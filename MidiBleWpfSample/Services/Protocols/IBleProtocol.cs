namespace MidiBleWpfSample.Services.Protocols
{
    public interface IBleProtocol
    {
        /// <summary>
        /// MIDI CC値などの入力を受け取り、BLEに送るバイト配列を生成する。
        /// </summary>
        byte[] BuildPacket(int ccValue);
    }
}
