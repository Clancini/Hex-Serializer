using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Clancini.HexSerialization
{
    public sealed class HexPropertyInfo
    {
        public PropertyInfo PropertyInfo { get; private set; }
        public HexOrder AttributeInfo { get; private set; }

        public HexPropertyInfo(PropertyInfo propertyInfo, HexOrder hexSchema)
        {
            PropertyInfo = propertyInfo;
            AttributeInfo = hexSchema;
        }
    }

    public class HexParser<T> where T : class
    {
        const int NO_KNOWN_SIZE = -1;   // Result of TryGetKnownSizeOfType when a type with a non-fixed size is passed.

        bool _cached;

        HexPropertyInfo[]? _cachedProperties;
        public HexPropertyInfo[]? CachedProperties
        {
            get
            {
                if (!_cached)
                {
                    throw new InvalidOperationException("Attempted to access cached properties before caching them.");
                }
                return _cachedProperties;
            }

            private set
            {
                _cachedProperties = value;
            }
        }

        void CacheIfNeeded()
        {
            if (_cached)
            {
                return;
            }

            _cachedProperties = typeof(T).GetProperties()
            .Select(p => new HexPropertyInfo(p, p.GetCustomAttribute<HexOrder>()))
            .Where(p => p.AttributeInfo != null)
            .OrderBy(p => p.AttributeInfo.Order)
            .ToArray();

            _cached = true;
        }

        public async Task<T> DeserializeFromFile(string path)
        {
            CacheIfNeeded();

            if (_cachedProperties is null)
            {
                throw new InvalidOperationException("Attempted to access non-existent cached properties.");
            }

            byte[] fileContents = await File.ReadAllBytesAsync(path);

            T instance = Activator.CreateInstance<T>();

            int offset = 0;

            foreach (HexPropertyInfo property in _cachedProperties)
            {
                Type type = property.PropertyInfo.PropertyType;

                int length = 0;

                length = TryGetKnownSizeOfType(type);
                if (length == NO_KNOWN_SIZE)
                {
                    if (type != typeof(string))
                    {
                        throw new InvalidOperationException("Attempted to deserialize an unsupported type.");
                    }

                    Memory<byte> viewOfLength = fileContents.AsMemory(offset, sizeof(ushort));
                    length = BitConverter.ToUInt16(viewOfLength.Span);
                    offset += sizeof(ushort);
                }

                Memory<byte> bytesSlice = fileContents.AsMemory(offset, length);

                SetAppropriateValue(instance, property, bytesSlice.Span, ref offset);
            }

            return instance;
        }

        void SetAppropriateValue(T instance, HexPropertyInfo property, Span<byte> bytes, ref int offset)
        {
            Type type = property.PropertyInfo.PropertyType;

            if (type == typeof(string))
            {
                string value = Encoding.UTF8.GetString(bytes);
                property.PropertyInfo.SetValue(instance, value);

                offset += Encoding.UTF8.GetByteCount(value);

                return;
            }

            if (type == typeof(int))
            {
                int value = BitConverter.ToInt32(bytes);
                property.PropertyInfo.SetValue(instance, value);

                offset += sizeof(int);

                return;
            }

            if (type == typeof(float))
            {
                float value = BitConverter.ToSingle(bytes);
                property.PropertyInfo.SetValue(instance, value);

                offset += sizeof(float);

                return;
            }

            if (type == typeof(bool))
            {
                bool value = BitConverter.ToBoolean(bytes);
                property.PropertyInfo.SetValue(instance, value);

                offset += sizeof(bool);

                return;
            }

            if (type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int))
            {
                int value = BitConverter.ToInt32(bytes);
                property.PropertyInfo.SetValue(instance, Enum.ToObject(type, value));

                offset += sizeof(int);

                return;
            }
        }

        public async Task SerializeToFile(string path, T instance)
        {
            CacheIfNeeded();

            if (_cachedProperties is null)
            {
                throw new InvalidOperationException("Attempted to access non-existent cached properties.");
            }

            Memory<byte> buffer = new byte[GetTotalInstanceSize(instance)];

            int offset = 0;

            foreach (HexPropertyInfo property in _cachedProperties)
            {
                GetBytesFromValue(instance, property, buffer.Span, ref offset);
            }

            await File.WriteAllBytesAsync(path, buffer.ToArray());
        }

        int GetTotalInstanceSize(T instance)
        {
            if (!_cached)
            {
                throw new InvalidOperationException("Tried accessing cached property info before caching them.");
            }

            if (_cachedProperties is null)
            {
                throw new InvalidOperationException("Attempted to access non-existent cached properties.");
            }

            int size = 0;

            foreach (HexPropertyInfo property in _cachedProperties)
            {
                Type type = property.PropertyInfo.PropertyType;

                if (type.IsEnum)
                {
                    type = Enum.GetUnderlyingType(type);
                }

                int increment = TryGetKnownSizeOfType(type);
                if (increment == NO_KNOWN_SIZE)
                {
                    if (type != typeof(string))
                    {
                        throw new InvalidOperationException("Attempted to get size of an unsupported type.");
                    }

                    string value = (string)property.PropertyInfo.GetValue(instance);

                    if (!string.IsNullOrEmpty(value))
                    {
                        increment = Encoding.UTF8.GetByteCount(value);
                    }
                    increment += sizeof(ushort);
                }

                size += increment;
            }

            return size;
        }

        void GetBytesFromValue(T instance, HexPropertyInfo property, Span<byte> buffer, ref int offset)
        {
            Type type = property.PropertyInfo.PropertyType;

            if (type == typeof(string))
            {
                string value = (string)property.PropertyInfo.GetValue(instance);

                if (value is null)
                {
                    value = string.Empty;
                }

                int stringBytesCount = Encoding.UTF8.GetByteCount(value);

                Span<byte> lengthPrefixSlice = buffer.Slice(offset, sizeof(ushort));
                BitConverter.TryWriteBytes(lengthPrefixSlice, (ushort)stringBytesCount);

                offset += sizeof(ushort);

                Span<byte> bufferSlice = buffer.Slice(offset, stringBytesCount);
                Encoding.UTF8.GetBytes(value, bufferSlice);

                offset += stringBytesCount;

                return;
            }

            if (type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int))
            {
                type = typeof(int);
            }

            if (type == typeof(int))
            {
                int value = (int)property.PropertyInfo.GetValue(instance);
                Span<byte> bufferSlice = buffer.Slice(offset, sizeof(int));
                BitConverter.TryWriteBytes(bufferSlice, value);

                offset += sizeof(int);

                return;
            }

            if (type == typeof(float))
            {
                float value = (float)property.PropertyInfo.GetValue(instance);
                Span<byte> bufferSlice = buffer.Slice(offset, sizeof(float));
                BitConverter.TryWriteBytes(bufferSlice, value);

                offset += sizeof(float);

                return;
            }

            if (type == typeof(bool))
            {
                bool value = (bool)property.PropertyInfo.GetValue(instance);
                Span<byte> bufferSlice = buffer.Slice(offset, sizeof(bool));
                BitConverter.TryWriteBytes(bufferSlice, value);

                offset += sizeof(bool);

                return;
            }
        }

        /// <summary>
        /// Attempts to get the size of a given type in bytes.<br></br>
        /// Only supports primitive types.
        /// </summary>
        /// <returns>The size in bytes if it's a supported type, 0 otherwise.</returns>
        static int TryGetKnownSizeOfType(Type type)
        {
            if (type.IsEnum)
            {
                type = Enum.GetUnderlyingType(type);
            }

            if (type == typeof(bool)) return sizeof(bool);
            else if (type == typeof(int)) return sizeof(int);
            else if (type == typeof(float)) return sizeof(float); 
            return NO_KNOWN_SIZE;
        }
    }
}