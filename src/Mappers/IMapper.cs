using System;

namespace NesEmulator.Mappers
{
    public enum MirroringMode
    {
        Horizontal,
        Vertical,
        SingleScreenLow,
        SingleScreenHigh,
        FourScreen
    }

    public abstract class Mapper
    {
        protected byte PrgBanks;
        protected byte ChrBanks;

        public MirroringMode Mirroring { get; protected set; }
        public virtual bool IrqState { get { return false; } }

        public Mapper(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
        {
            PrgBanks = prgBanks;
            ChrBanks = chrBanks;
            Mirroring = initialMirroring;
        }

        public abstract bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data);
        public abstract bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data);
        public abstract bool PpuMapRead(ushort addr, out uint mappedAddr);
        public abstract bool PpuMapWrite(ushort addr, out uint mappedAddr);

        public virtual void Scanline() { }
        public virtual void ClearIrq() { }
        public virtual void Reset() { }
    }
}
