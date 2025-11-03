using System;

namespace Clancini.HexSerialization
{
    /// <summary>
    /// Defines the order at which serialization/deserialization happens.<br></br>
    /// Lower numbers have priority over higher ones.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HexOrder : Attribute
    {
        public int Order { get; private set; }

        public HexOrder(int order)
        {
            Order = order;
        }
    }
}