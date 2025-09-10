using System;
using System.Collections.Generic;
using System.Text;

namespace BasisNetworkCore
{
    public static class BasisPacketUtil
    {
        public static bool ValidatePacket(byte New, byte Old)
        {
            if (IsNewer(New, Old) && New != Old)
            {
                return true;
            }
            return false;
        }
        // Returns true if seq1 is newer than seq2
        public static bool IsNewer(byte seq1, byte seq2)
        {
            return (byte)(seq1 - seq2) < 128;
        }
    }
}
