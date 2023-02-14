using System;
using System.Collections.Generic;
using System.Text;

namespace FeatBit.Sdk.Server.Transport
{
    internal static class ConnectionToken
    {
        private static readonly Dictionary<char, char> NumberMap = new Dictionary<char, char>()
        {
            { '0', 'Q' },
            { '1', 'B' },
            { '2', 'W' },
            { '3', 'S' },
            { '4', 'P' },
            { '5', 'H' },
            { '6', 'D' },
            { '7', 'X' },
            { '8', 'Z' },
            { '9', 'U' }
        };

        internal static string New(string envSecret)
        {
            var trimmed = envSecret.TrimEnd('=');
            var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            var timestampLength = timestamp.ToString().Length;
            var start = Math.Max((int)Math.Floor(new Random().NextDouble() * trimmed.Length), 2);

            var sb = new StringBuilder();

            sb.Append(EncodeNumber(start, 3));
            sb.Append(EncodeNumber(timestamp.ToString().Length, 2));
            sb.Append(trimmed.Substring(0, start));
            sb.Append(EncodeNumber(timestamp, timestampLength));
            sb.Append(trimmed.Substring(start));

            return sb.ToString();
        }

        private static string EncodeNumber(long number, int length)
        {
            var sb = new StringBuilder();

            var paddedNumber = number.ToString().PadLeft(length, '0');
            foreach (var n in paddedNumber.Substring(paddedNumber.Length - length))
            {
                sb.Append(NumberMap[n]);
            }

            return sb.ToString();
        }
    }
}