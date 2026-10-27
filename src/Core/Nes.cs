using System;
using System.IO;

namespace NesEmulator.Core
{
    public class Nes
    {
        public Bus Bus { get; private set; }
        public Cpu Cpu { get; private set; }
        public Ppu Ppu { get; private set; }
        public Apu Apu { get; private set; }
        public Cartridge Cartridge { get; private set; }

        public bool IsRomLoaded { get { return Cartridge != null && Cartridge.IsValid; } }

        public Nes()
        {
            Bus = new Bus();
            Cpu = new Cpu(Bus);
            Ppu = new Ppu();
            Apu = new Apu();

            Bus.Cpu = Cpu;
            Bus.Ppu = Ppu;
            Bus.Apu = Apu;

            Ppu.Cpu = Cpu;
        }

        public bool LoadRom(string filePath)
        {
            Cartridge cart = new Cartridge(filePath);
            if (!cart.IsValid)
                return false;

            Cartridge = cart;
            Bus.Cartridge = cart;
            Ppu.Cartridge = cart;

            Reset();
            return true;
        }

        public void Reset()
        {
            Bus.Reset();
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset();
        }

        public void Step()
        {
            int cpuCycles = Cpu.Step();
            for (int i = 0; i < cpuCycles * 3; i++)
            {
                Ppu.Step();
            }
            Apu.Clock(cpuCycles);
        }

        public void StepFrame()
        {
            Ppu.FrameComplete = false;
            while (!Ppu.FrameComplete)
            {
                Step();
            }
        }

        public bool SaveState(string saveFilePath)
        {
            try
            {
                using (BinaryWriter bw = new BinaryWriter(File.Open(saveFilePath, FileMode.Create)))
                {
                    // Magic
                    bw.Write(new char[] { 'N', 'E', 'S', 'S' });

                    // CPU state
                    bw.Write(Cpu.A);
                    bw.Write(Cpu.X);
                    bw.Write(Cpu.Y);
                    bw.Write(Cpu.SP);
                    bw.Write(Cpu.PC);
                    bw.Write(Cpu.Status);
                    bw.Write(Cpu.Cycles);

                    // RAM
                    bw.Write(Bus.CpuRam);

                    // PPU OAM
                    bw.Write(Ppu.Oam);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool LoadState(string saveFilePath)
        {
            try
            {
                if (!File.Exists(saveFilePath))
                    return false;

                using (BinaryReader br = new BinaryReader(File.Open(saveFilePath, FileMode.Open)))
                {
                    char[] magic = br.ReadChars(4);
                    if (magic[0] != 'N' || magic[1] != 'E' || magic[2] != 'S' || magic[3] != 'S')
                        return false;

                    Cpu.A = br.ReadByte();
                    Cpu.X = br.ReadByte();
                    Cpu.Y = br.ReadByte();
                    Cpu.SP = br.ReadByte();
                    Cpu.PC = br.ReadUInt16();
                    Cpu.Status = br.ReadByte();
                    Cpu.Cycles = br.ReadInt64();

                    byte[] ram = br.ReadBytes(Bus.CpuRam.Length);
                    Array.Copy(ram, Bus.CpuRam, ram.Length);

                    byte[] oam = br.ReadBytes(Ppu.Oam.Length);
                    Array.Copy(oam, Ppu.Oam, oam.Length);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
