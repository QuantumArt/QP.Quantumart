using System;

namespace Quantumart.IntegrationTests.Infrastructure
{
    internal class DynamicImageCopyFile : IEquatable<DynamicImageCopyFile>
    {
        private string From { get; }

        private string To { get; }

        public DynamicImageCopyFile(string from, string to)
        {
            From = from;
            To = to;
        }

        public bool Equals(DynamicImageCopyFile other) => From == other?.From && To == other?.To;

        public override bool Equals(object other) => other is DynamicImageCopyFile && Equals(other);

        public override int GetHashCode() => From.GetHashCode() + To.GetHashCode();

        public override string ToString() => $"From: {From}, To: {To}";
    }
}
