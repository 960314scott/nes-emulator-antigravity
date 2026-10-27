using System;
using NesEmulator.Audio;

namespace NesEmulator.Core
{
    public class Apu
    {
        private static readonly byte[][] DutyTable = new byte[][]
        {
            new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, // 12.5%
            new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 }, // 25%
            new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 }, // 50%
            new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 }  // 75% inverted
        };

        private static readonly byte[] TriangleTable = new byte[]
        {
            15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

        private static readonly ushort[] NoisePeriodTable = new ushort[]
        {
            4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
        };

        private static readonly byte[] LengthTable = new byte[]
        {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        // Pulse 1
        private bool _pulse1Enabled;
        private byte _pulse1Duty;
        private byte _pulse1DutyPos;
        private ushort _pulse1Timer;
        private ushort _pulse1Period;
        private byte _pulse1Length;
        private byte _pulse1Volume;
        private bool _pulse1Halt;
        private bool _pulse1ConstantVol;

        // Pulse 2
        private bool _pulse2Enabled;
        private byte _pulse2Duty;
        private byte _pulse2DutyPos;
        private ushort _pulse2Timer;
        private ushort _pulse2Period;
        private byte _pulse2Length;
        private byte _pulse2Volume;
        private bool _pulse2Halt;
        private bool _pulse2ConstantVol;

        // Triangle
        private bool _triEnabled;
        private byte _triStep;
        private ushort _triTimer;
        private ushort _triPeriod;
        private byte _triLength;
        private bool _triHalt;

        // Noise
        private bool _noiseEnabled;
        private ushort _noiseShift = 1;
        private ushort _noiseTimer;
        private ushort _noisePeriod;
        private byte _noiseLength;
        private byte _noiseVolume;
        private bool _noiseHalt;
        private bool _noiseConstantVol;
        private bool _noiseMode;

        // Frame sequencer & Downsampler
        private int _frameSeqCycles;
        private int _sampleAccumulator;
        private const int CpuClock = 1789773;
        private const int SampleRate = 44100;

        public WaveOutPlayer Player { get; set; }

        public void Write(ushort addr, byte val)
        {
            switch (addr)
            {
                // Pulse 1
                case 0x4000:
                    _pulse1Duty = (byte)((val >> 6) & 3);
                    _pulse1Halt = (val & 0x20) != 0;
                    _pulse1ConstantVol = (val & 0x10) != 0;
                    _pulse1Volume = (byte)(val & 0x0F);
                    break;
                case 0x4002:
                    _pulse1Period = (ushort)((_pulse1Period & 0x0700) | val);
                    break;
                case 0x4003:
                    _pulse1Period = (ushort)((_pulse1Period & 0x00FF) | ((val & 0x07) << 8));
                    if (_pulse1Enabled)
                        _pulse1Length = LengthTable[(val >> 3) & 0x1F];
                    _pulse1DutyPos = 0;
                    break;

                // Pulse 2
                case 0x4004:
                    _pulse2Duty = (byte)((val >> 6) & 3);
                    _pulse2Halt = (val & 0x20) != 0;
                    _pulse2ConstantVol = (val & 0x10) != 0;
                    _pulse2Volume = (byte)(val & 0x0F);
                    break;
                case 0x4006:
                    _pulse2Period = (ushort)((_pulse2Period & 0x0700) | val);
                    break;
                case 0x4007:
                    _pulse2Period = (ushort)((_pulse2Period & 0x00FF) | ((val & 0x07) << 8));
                    if (_pulse2Enabled)
                        _pulse2Length = LengthTable[(val >> 3) & 0x1F];
                    _pulse2DutyPos = 0;
                    break;

                // Triangle
                case 0x4008:
                    _triHalt = (val & 0x80) != 0;
                    break;
                case 0x400A:
                    _triPeriod = (ushort)((_triPeriod & 0x0700) | val);
                    break;
                case 0x400B:
                    _triPeriod = (ushort)((_triPeriod & 0x00FF) | ((val & 0x07) << 8));
                    if (_triEnabled)
                        _triLength = LengthTable[(val >> 3) & 0x1F];
                    break;

                // Noise
                case 0x400C:
                    _noiseHalt = (val & 0x20) != 0;
                    _noiseConstantVol = (val & 0x10) != 0;
                    _noiseVolume = (byte)(val & 0x0F);
                    break;
                case 0x400E:
                    _noiseMode = (val & 0x80) != 0;
                    _noisePeriod = NoisePeriodTable[val & 0x0F];
                    break;
                case 0x400F:
                    if (_noiseEnabled)
                        _noiseLength = LengthTable[(val >> 3) & 0x1F];
                    break;

                // Status
                case 0x4015:
                    _pulse1Enabled = (val & 1) != 0;
                    if (!_pulse1Enabled) _pulse1Length = 0;

                    _pulse2Enabled = (val & 2) != 0;
                    if (!_pulse2Enabled) _pulse2Length = 0;

                    _triEnabled = (val & 4) != 0;
                    if (!_triEnabled) _triLength = 0;

                    _noiseEnabled = (val & 8) != 0;
                    if (!_noiseEnabled) _noiseLength = 0;
                    break;
            }
        }

        public byte Read(ushort addr)
        {
            if (addr == 0x4015)
            {
                byte res = 0;
                if (_pulse1Length > 0) res |= 1;
                if (_pulse2Length > 0) res |= 2;
                if (_triLength > 0) res |= 4;
                if (_noiseLength > 0) res |= 8;
                return res;
            }
            return 0;
        }

        public void Clock(int cpuCycles)
        {
            // Frame counter clocking ~240 Hz
            _frameSeqCycles += cpuCycles;
            if (_frameSeqCycles >= 7457)
            {
                _frameSeqCycles -= 7457;
                ClockHalfFrame();
            }

            // Pulse 1
            if (_pulse1Timer < cpuCycles)
            {
                _pulse1DutyPos = (byte)((_pulse1DutyPos + 1) & 7);
                _pulse1Timer = (ushort)(_pulse1Period * 2 + 1);
            }
            else
            {
                _pulse1Timer -= (ushort)cpuCycles;
            }

            // Pulse 2
            if (_pulse2Timer < cpuCycles)
            {
                _pulse2DutyPos = (byte)((_pulse2DutyPos + 1) & 7);
                _pulse2Timer = (ushort)(_pulse2Period * 2 + 1);
            }
            else
            {
                _pulse2Timer -= (ushort)cpuCycles;
            }

            // Triangle
            if (_triTimer < cpuCycles)
            {
                if (_triLength > 0 && _triPeriod > 2)
                    _triStep = (byte)((_triStep + 1) & 31);
                _triTimer = _triPeriod;
            }
            else
            {
                _triTimer -= (ushort)cpuCycles;
            }

            // Noise
            if (_noiseTimer < cpuCycles)
            {
                ushort feedback = (ushort)((_noiseShift & 1) ^ ((_noiseShift >> (_noiseMode ? 6 : 1)) & 1));
                _noiseShift = (ushort)((_noiseShift >> 1) | (feedback << 14));
                _noiseTimer = _noisePeriod;
            }
            else
            {
                _noiseTimer -= (ushort)cpuCycles;
            }

            // Audio sampling
            _sampleAccumulator += cpuCycles * SampleRate;
            if (_sampleAccumulator >= CpuClock)
            {
                _sampleAccumulator -= CpuClock;

                if (Player != null)
                {
                    short mixed = MixSample();
                    Player.PlaySample(mixed);
                }
            }
        }

        private void ClockHalfFrame()
        {
            // Clock length counters
            if (!_pulse1Halt && _pulse1Length > 0) _pulse1Length--;
            if (!_pulse2Halt && _pulse2Length > 0) _pulse2Length--;
            if (!_triHalt && _triLength > 0) _triLength--;
            if (!_noiseHalt && _noiseLength > 0) _noiseLength--;
        }

        private short MixSample()
        {
            int p1 = 0;
            if (_pulse1Enabled && _pulse1Length > 0 && _pulse1Period > 7)
            {
                if (DutyTable[_pulse1Duty][_pulse1DutyPos] == 1)
                    p1 = _pulse1Volume;
            }

            int p2 = 0;
            if (_pulse2Enabled && _pulse2Length > 0 && _pulse2Period > 7)
            {
                if (DutyTable[_pulse2Duty][_pulse2DutyPos] == 1)
                    p2 = _pulse2Volume;
            }

            int tri = 0;
            if (_triEnabled && _triLength > 0 && _triPeriod > 2)
            {
                tri = TriangleTable[_triStep];
            }

            int noise = 0;
            if (_noiseEnabled && _noiseLength > 0 && (_noiseShift & 1) == 0)
            {
                noise = _noiseVolume;
            }

            // Quick linear approximation mix scaled to 16-bit PCM
            int pulseOut = (p1 + p2) * 550;
            int tndOut = (tri * 500) + (noise * 400);
            int total = pulseOut + tndOut - 8000;

            if (total > 32767) total = 32767;
            if (total < -32768) total = -32768;

            return (short)total;
        }

        public void Reset()
        {
            _pulse1Enabled = _pulse2Enabled = _triEnabled = _noiseEnabled = false;
            _pulse1Length = _pulse2Length = _triLength = _noiseLength = 0;
            _noiseShift = 1;
        }
    }
}
