using System;

namespace NesEmulator.Core
{
    public class Bus
    {
        public Cpu Cpu { get; set; }
        public Ppu Ppu { get; set; }
        public Apu Apu { get; set; }
        public Cartridge Cartridge { get; set; }
        public Controller Controller1 { get; set; }
        public Controller Controller2 { get; set; }

        // 2KB internal RAM
        public readonly byte[] CpuRam = new byte[2048];

        // Open bus holds the last value floating on the data bus
        public byte OpenBus { get; set; }

        public Bus()
        {
            Controller1 = new Controller();
            Controller2 = new Controller();
            OpenBus = 0;
        }

        public byte CpuRead(ushort addr)
        {
            byte data = 0;

            if (Cartridge != null && Cartridge.CpuRead(addr, ref data))
            {
                OpenBus = data;
                return data;
            }

            if (addr <= 0x1FFF)
            {
                // 2KB RAM mirrored 4 times
                OpenBus = CpuRam[addr & 0x07FF];
                return OpenBus;
            }

            if (addr >= 0x2000 && addr <= 0x3FFF)
            {
                // PPU registers mirrored every 8 bytes
                if (Ppu != null)
                {
                    OpenBus = Ppu.CpuRead((ushort)(0x2000 + (addr & 0x0007)));
                    return OpenBus;
                }
            }

            if (addr == 0x4015)
            {
                if (Apu != null)
                {
                    byte res = Apu.Read(addr);
                    // Bit 5 is open bus
                    OpenBus = (byte)((res & 0xDF) | (OpenBus & 0x20));
                    return OpenBus;
                }
            }

            if (addr == 0x4016)
            {
                if (Controller1 != null)
                {
                    byte res = Controller1.Read();
                    // Upper bits 5-7 are open bus
                    OpenBus = (byte)((OpenBus & 0xE0) | (res & 0x1F));
                    return OpenBus;
                }
            }

            if (addr == 0x4017)
            {
                if (Controller2 != null)
                {
                    byte res = Controller2.Read();
                    // Upper bits 5-7 are open bus
                    OpenBus = (byte)((OpenBus & 0xE0) | (res & 0x1F));
                    return OpenBus;
                }
            }

            // Unmapped memory returns floating Open Bus
            return OpenBus;
        }

        public void CpuWrite(ushort addr, byte data)
        {
            OpenBus = data;

            if (Cartridge != null && Cartridge.CpuWrite(addr, data))
            {
                return;
            }

            if (addr <= 0x1FFF)
            {
                CpuRam[addr & 0x07FF] = data;
                return;
            }

            if (addr >= 0x2000 && addr <= 0x3FFF)
            {
                if (Ppu != null)
                    Ppu.CpuWrite((ushort)(0x2000 + (addr & 0x0007)), data);
                return;
            }

            if (addr >= 0x4000 && addr <= 0x4013)
            {
                if (Apu != null)
                    Apu.Write(addr, data);
                return;
            }

            if (addr == 0x4014)
            {
                // OAM DMA Transfer
                ushort page = (ushort)(data << 8);
                if (Ppu != null)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        byte b = CpuRead((ushort)(page + i));
                        Ppu.Oam[i] = b;
                    }
                }
                if (Cpu != null)
                {
                    Cpu.StallCycles += (Cpu.Cycles % 2 == 1) ? 514 : 513;
                }
                return;
            }

            if (addr == 0x4015)
            {
                if (Apu != null)
                    Apu.Write(addr, data);
                return;
            }

            if (addr == 0x4016)
            {
                if (Controller1 != null)
                    Controller1.Write(data);
                if (Controller2 != null)
                    Controller2.Write(data);
                return;
            }

            if (addr == 0x4017)
            {
                if (Apu != null)
                    Apu.Write(addr, data);
                return;
            }
        }

        public ushort CpuRead16(ushort addr)
        {
            ushort lo = CpuRead(addr);
            ushort hi = CpuRead((ushort)(addr + 1));
            return (ushort)((hi << 8) | lo);
        }

        public void Reset()
        {
            Array.Clear(CpuRam, 0, CpuRam.Length);
            OpenBus = 0;
            if (Ppu != null) Ppu.Reset();
            if (Apu != null) Apu.Reset();
            if (Controller1 != null) Controller1.Reset();
            if (Controller2 != null) Controller2.Reset();
            if (Cartridge != null) Cartridge.Reset();
        }
    }
}
